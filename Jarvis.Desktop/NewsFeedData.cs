using System.Globalization;
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;

namespace Jarvis.Desktop;

internal sealed record NewsHeadline(string Title, string Url, DateTimeOffset PublishedAt);

internal static class NewsFeedData
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly Uri FeedUri = new("https://yle.fi/rss/news");

    public static async Task<IReadOnlyList<NewsHeadline>> GetHeadlinesAsync(CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(FeedUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 1_000_000
        });
        XDocument document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        var items = document.Root?.Element("channel")?.Elements("item") ?? Enumerable.Empty<XElement>();
        var headlines = new List<NewsHeadline>();
        foreach (XElement item in items)
        {
            string title = item.Element("title")?.Value.Trim() ?? string.Empty;
            string urlText = item.Element("link")?.Value.Trim() ?? string.Empty;
            string publishedText = item.Element("pubDate")?.Value.Trim() ?? string.Empty;
            if (title.Length == 0 || !Uri.TryCreate(urlText, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !(uri.Host.Equals("yle.fi", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".yle.fi", StringComparison.OrdinalIgnoreCase)))
                continue;
            if (!DateTimeOffset.TryParse(publishedText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset publishedAt))
                publishedAt = DateTimeOffset.MinValue;
            headlines.Add(new NewsHeadline(title, uri.AbsoluteUri, publishedAt));
            if (headlines.Count == 4) break;
        }
        return headlines;
    }
}
