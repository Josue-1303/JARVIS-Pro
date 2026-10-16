using Jarvis.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

try
{

var fixedFriday = new DateTimeOffset(2026, 10, 16, 23, 59, 0, TimeSpan.FromHours(3));
if (!JarvisAI.LocalClockReply("¿Qué día es hoy?", null, fixedFriday)!.Text.Contains("viernes") ||
    !JarvisAI.LocalClockReply("What day is it today?", null, fixedFriday.AddMinutes(2))!.Text.Contains("Saturday") ||
    JarvisAI.LocalClockReply("What day is Christmas?", null, fixedFriday) != null ||
    JarvisAI.LocalClockReply("¿Qué hora es en Tokio?", null, fixedFriday) != null ||
    JarvisAI.LocalClockReply("¿Estás seguro?", "Explica la gravedad", fixedFriday) != null ||
    !JarvisAI.LocalClockReply("Compruébalo", "¿Qué día es hoy?", fixedFriday)!.Text.Contains("viernes"))
    throw new Exception("Local clock routing regression");
Console.WriteLine("PASS: fixed Friday, midnight rollover, bounded routing and contextual recheck.");

using var store = new MemoryStore(Path.Combine(Path.GetTempPath(), "jarvis-response-check-" + Guid.NewGuid().ToString("N")));
var conversation = store.CreateConversation();
var session = new ConversationSession(store, conversation);
session.Append("Salúdame en español", new JarvisReply("¡Hola! ¿Cómo estás?", "es"));
session.Append("Can you tell me what time it is?", new JarvisReply("¡Hola! ¿Cómo estás?", "es"));
session.Append("And can you tell me what time it is?", new JarvisReply("¡Hola! ¿Cómo estás?", "es"));
using var handler = new InspectHandler();
using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
var ai = new JarvisAI(client, session);
foreach (var question in new[] { "Can you tell me what time it is?", "What is seven plus five?", "¿Qué hora es?" })
{
    var before = DateTimeOffset.Now;
    int callsBefore = handler.Calls;
    var answer = await ai.AskReplyAsync(question);
    Console.WriteLine(question + " => " + answer);
    if (answer.Text.Contains("Cómo estás", StringComparison.OrdinalIgnoreCase)) throw new Exception("Repeated unrelated greeting");
    if (question.Contains("plus"))
    {
        if (answer.Language != "en" || !(answer.Text.Contains("twelve", StringComparison.OrdinalIgnoreCase) || answer.Text.Contains("12"))) throw new Exception("Wrong arithmetic or language");
    }
    else
    {
        if (answer.Language != (question.StartsWith("¿") ? "es" : "en")) throw new Exception("Wrong response language");
        if (!answer.Text.Contains(before.ToString("HH:mm")) && !answer.Text.Contains(DateTimeOffset.Now.ToString("HH:mm"))) throw new Exception("Incorrect local clock time");
        if (handler.Calls != callsBefore) throw new Exception("Clock request unnecessarily sent to model");
    }
}
Console.WriteLine("PASS: resumed repeated-greeting history recovered; English time, arithmetic and Spanish time are correct.");

var wrongDay = DateTimeOffset.Now.AddDays(1).ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));
session.Append("¿Qué día es hoy?", new JarvisReply($"Hoy es {wrongDay}.", "es"));
session.Append("Compruébalo", new JarvisReply($"Sí, es {wrongDay}. Revisa tu calendario.", "es"));
ai = new JarvisAI(client, session); // Reopen the contaminated history, as at startup.
foreach (var question in new[] { "¿Qué día es hoy?", "¿Estás seguro? Compruébalo de nuevo y dime qué día es hoy.", "What day of the week is it today?" })
{
    var answer = await ai.AskReplyAsync(question);
    Console.WriteLine(question + " => " + answer);
    string language = question.StartsWith("¿") ? "es" : "en";
    string day = DateTimeOffset.Now.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo(language == "es" ? "es-ES" : "en-US"));
    if (answer.Language != language || !answer.Text.Contains(day, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Incorrect weekday or language: expected " + day);
    if (answer.Text.Contains("tu calendario", StringComparison.OrdinalIgnoreCase) || answer.Text.Contains("your calendar", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Deflected verification to user calendar");
}
if (!store.Load(session.Current.Id).Turns.Last().Answer.Contains("Friday", StringComparison.OrdinalIgnoreCase) && DateTimeOffset.Now.DayOfWeek == DayOfWeek.Friday)
    throw new Exception("Clock answer was not persisted");
Console.WriteLine("PASS: current weekday and verification use clock despite incorrect saved history, in Spanish and English.");
}
catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }

sealed class InspectHandler() : DelegatingHandler(new HttpClientHandler())
{
    public int Calls { get; private set; }
    public string ExpectedTime { get; private set; } = "";
    public string ExpectedSpanishDay { get; private set; } = "";
    public string ExpectedEnglishDay { get; private set; } = "";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
        var system = body["messages"]![0]!["content"]!.GetValue<string>();
        ExpectedTime = System.Text.RegularExpressions.Regex.Match(system, @"Current computer local date/time: \d{4}-\d{2}-\d{2} (\d{2}:\d{2})").Groups[1].Value;
        if (ExpectedTime.Length == 0) throw new Exception("Missing current clock");
        var dayMatch = System.Text.RegularExpressions.Regex.Match(system, @"Current weekday: (\w+) / (\w+)\.");
        ExpectedEnglishDay = dayMatch.Groups[1].Value;
        ExpectedSpanishDay = dayMatch.Groups[2].Value;
        var current = DateTimeOffset.Now;
        if (ExpectedEnglishDay != current.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("en-US")) ||
            ExpectedSpanishDay != current.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("es-ES")))
            throw new Exception("Clock weekday context missing or incorrect");
        Console.WriteLine("Latest sent: " + body["messages"]!.AsArray().Last()!.ToJsonString());
        request.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        return await base.SendAsync(request, ct);
    }
}
