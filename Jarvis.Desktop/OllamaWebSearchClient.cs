using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using Jarvis.Core;

namespace Jarvis.Desktop;

internal sealed class OllamaWebSearchClient : IDisposable
{
    private static readonly Uri Endpoint = new("https://ollama.com/api/web_search");
    private const int MaximumResponseBytes = 1_000_000;
    private readonly HttpClient client;
    private readonly bool ownsClient;

    public OllamaWebSearchClient(HttpClient? httpClient = null)
    {
        client = httpClient ?? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
        ownsClient = httpClient is null;
        if (ownsClient) client.Timeout = TimeSpan.FromSeconds(25);
    }

    public void Dispose() { if (ownsClient) client.Dispose(); }

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new { query, max_results = 5 });
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException("La clave de búsqueda web no es válida. Revísela en Configurar búsqueda web.");
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("Se alcanzó el límite temporal de búsquedas de Ollama. Inténtelo más tarde.");
        if ((int)response.StatusCode is >= 300 and < 400)
            throw new InvalidOperationException("El servicio de búsqueda devolvió una redirección inesperada. No se envió la clave a otro sitio.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumResponseBytes)
            throw new InvalidDataException("La respuesta de búsqueda excede el tamaño permitido.");
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var bounded = new MemoryStream();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (bounded.Length + read > MaximumResponseBytes)
                throw new InvalidDataException("La respuesta de búsqueda excede el tamaño permitido.");
            await bounded.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        bounded.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(bounded, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("El servicio de búsqueda devolvió un formato que J.A.R.V.I.S. no reconoce.");

        var output = new List<WebSearchResult>(5);
        foreach (JsonElement result in results.EnumerateArray())
        {
            string title = ReadText(result, "title", 240);
            string url = ReadText(result, "url", 2048);
            string content = ReadText(result, "content", 1800);
            if (title.Length == 0 || content.Length == 0 ||
                !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(uri.UserInfo)) continue;
            output.Add(new WebSearchResult(title, uri.AbsoluteUri, content));
            if (output.Count == 5) break;
        }
        return output;
    }

    private static string ReadText(JsonElement element, string name, int maxLength)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String) return string.Empty;
        string text = value.GetString()?.Trim() ?? string.Empty;
        return text[..Math.Min(text.Length, maxLength)];
    }
}
