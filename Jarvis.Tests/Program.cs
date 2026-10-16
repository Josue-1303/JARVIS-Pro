using Jarvis.Core;
using System.Net;
using System.Text;
using System.Text.Json;

try
{
string testRoot = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "jarvis-v05-tests-" + Guid.NewGuid().ToString("N")));
if (args.Length > 0 && args[0].StartsWith("--live-"))
{
    using var liveStore = new MemoryStore(testRoot);
    if (args[0] == "--live-seed")
    {
        liveStore.AddMemory("Mi nombre de prueba es Aurora.");
        var session = new ConversationSession(liveStore, liveStore.CreateConversation());
        var ai = new JarvisAI(session: session);
        var answer = await ai.AskReplyAsync("La clave de esta conversación de prueba es ORBITA-527. Confirma brevemente que la recibiste.");
        Console.WriteLine("SEED: " + answer.Text);
    }
    else
    {
        var session = new ConversationSession(liveStore, liveStore.ListConversations().First());
        var ai = new JarvisAI(session: session);
        var answer = await ai.AskReplyAsync("¿Cuál es mi nombre de prueba y la clave que te dije? Responde brevemente.");
        Console.WriteLine("RESUME: " + answer.Text);
        if (!answer.Text.Contains("Aurora", StringComparison.OrdinalIgnoreCase) || !answer.Text.Contains("527"))
            throw new Exception("Live restart memory check failed.");
        Console.WriteLine("PASS: independent process recovered preference and conversation context.");
    }
    return;
}

int count = 0;
void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); count++; }
void Reject(Action action, string label) { bool failed = false; try { action(); } catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or InvalidOperationException) { failed = true; } Check(failed, label); }
Guid conversationId;
Guid memoryId;
using (var store = new MemoryStore(testRoot))
{
    Check(store.ListConversations().Count == 0 && store.ListMemories().Count == 0, "Empty first run");
    Reject(() => { using var other = new MemoryStore(testRoot); }, "Second writer blocked");
    var conversation = store.CreateConversation();
    conversationId = conversation.Id;
    memoryId = store.AddMemory("Prefiero respuestas breves en español.").Id;
    var session = new ConversationSession(store, conversation);
    session.Append("¿Cuál es la clave?", new JarvisReply("Árbol-42", "es"));
    Check(store.Load(conversationId).Turns[0].Answer == "Árbol-42", "Unicode turn persisted");
    Reject(() => store.AddMemory(" "), "Empty memory rejected");
    Reject(() => store.AddMemory(new string('a', 301)), "Oversized memory rejected");
}
using (var store = new MemoryStore(testRoot))
{
    Check(store.ListMemories().Single().Id == memoryId, "Memory survives reopening");
    Check(store.ListConversations().Single().Id == conversationId, "Conversation survives reopening");
    var session = new ConversationSession(store, store.Load(conversationId));
    using var fake = new FakeOllama();
    using var client = new HttpClient(fake);
    var ai = new JarvisAI(client, session);
    await ai.AskReplyAsync("Continúa");
    Check(JsonDocument.Parse(fake.LastRequest).RootElement.GetProperty("messages").EnumerateArray().Any(m =>
        m.GetProperty("role").GetString() == "assistant" &&
        JarvisAI.ParseReply(m.GetProperty("content").GetString()!).Text == "Árbol-42"), "Restored history sent to model");
    Check(JsonDocument.Parse(fake.LastRequest).RootElement.GetProperty("messages").EnumerateArray().Any(m => m.GetProperty("content").GetString()!.Contains("User-approved")), "Approved memory sent to model");
    Check(store.Load(conversationId).Turns.Count == 2, "Successful reply saved");
    store.DeleteMemory(memoryId);
    await ai.AskReplyAsync("Siguiente");
    Check(!fake.LastRequest.Contains("User-approved"), "Deleted memory absent from next request without restart");
    fake.Invalid = true;
    int before = session.Current.Turns.Count;
    bool invalid = false;
    try { await ai.AskReplyAsync("Failure"); } catch (InvalidDataException) { invalid = true; }
    Check(invalid && store.Load(conversationId).Turns.Count == before, "Invalid model response does not persist a partial turn");
    fake.Invalid = false;
    bool rejected = false;
    try { await ai.AskReplyAsync(new string('x', 8001)); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "Question size bounded");
    var newSession = new ConversationSession(store, store.CreateConversation());
    var newAI = new JarvisAI(client, newSession);
    await newAI.AskReplyAsync("Nueva");
    Check(JsonDocument.Parse(fake.LastRequest).RootElement.GetProperty("messages").GetArrayLength() == 2, "New conversation starts without previous history");
    for (int i = 0; i < 30; i++) newSession.Append("Question " + i + new string('x', 1000), new JarvisReply("Answer " + i, "en"));
    newAI = new JarvisAI(client, newSession);
    await newAI.AskReplyAsync("Current question");
    var messages = JsonDocument.Parse(fake.LastRequest).RootElement.GetProperty("messages").EnumerateArray().ToArray();
    Check(messages.Length <= 26, "At most 12 previous turns sent");
    Check(messages.Skip(1).SkipLast(1).Sum(m => m.GetProperty("content").GetString()!.Length) <= JarvisAI.MaxHistoryCharacters, "History character budget enforced");
    Check(store.Load(newSession.Current.Id).Turns.Count == 32, "Full history retained on disk despite context limit");
    Check(messages[1].GetProperty("role").GetString() == "user" && messages[^2].GetProperty("role").GetString() == "assistant", "Context trimming preserves complete pairs");

    // Console menus: save confirmation, deletion cancellation and deletion confirmation.
    var originalInput = Console.In;
    try
    {
        Console.SetIn(new StringReader("G\nRecuerdo aprobado\nNO\n\n")); MemoryMenu.Show(store);
        Check(store.ListMemories().Count == 0, "Unconfirmed preference not saved");
        Console.SetIn(new StringReader("G\nRecuerdo aprobado\nSI\n\n")); MemoryMenu.Show(store);
        Check(store.ListMemories().Count == 1, "Confirmed preference saved through menu");
        Console.SetIn(new StringReader("B\n1\nNO\n\n")); MemoryMenu.Show(store);
        Check(store.ListMemories().Count == 1, "Cancelled deletion preserves memory");
        Console.SetIn(new StringReader("B\n1\nSI\n\n")); MemoryMenu.Show(store);
        Check(store.ListMemories().Count == 0, "Confirmed deletion through menu");
        Console.SetIn(new StringReader("N\n"));
        var fresh = MemoryMenu.ChooseConversation(store, true);
        Check(fresh.Turns.Count == 0, "Startup new conversation option");
        Console.SetIn(new StringReader("\n"));
        Check(MemoryMenu.ChooseConversation(store, true).Id == fresh.Id, "Startup continue latest option");
    }
    finally { Console.SetIn(originalInput); }
    var selection = new VoiceSelection();
    Check(selection.Resolve("en") == "en" && selection.Resolve("unknown") == "es", "Automatic voice preserved");
    selection.HandleKey(ConsoleKey.D1); Check(selection.Resolve("en") == "es", "Manual Spanish preserved");
    selection.HandleKey(ConsoleKey.D2); Check(selection.Resolve("es") == "en", "Manual English preserved");
    selection.HandleKey(ConsoleKey.A); Check(selection.Resolve("es") == "es", "Automatic reset preserved");
    store.DeleteConversation(conversationId);
    Check(!store.ListConversations().Any(c => c.Id == conversationId), "Conversation deletion persists");
    Check(!Directory.EnumerateFiles(testRoot, "*.tmp", SearchOption.AllDirectories).Any(), "No incomplete writes left");
    string memoriesPath = Path.Combine(testRoot, "memories.json");
    File.WriteAllText(memoriesPath, "broken");
    Reject(() => store.AddMemory("Do not overwrite"), "Corrupt memory file blocks writes");
    Check(File.ReadAllText(memoriesPath) == "broken", "Corrupt source preserved");
    var recoveryInput = Console.In;
    try
    {
        Console.SetIn(new StringReader("NO\n")); MemoryMenu.Show(store);
        Check(File.ReadAllText(memoriesPath) == "broken", "Cancelled recovery preserves damaged file");
        Console.SetIn(new StringReader("SI\n\n")); MemoryMenu.Show(store);
    }
    finally { Console.SetIn(recoveryInput); }
    Check(store.ListMemories().Count == 0, "Explicit clear repairs memory file");
    string damaged = Path.Combine(testRoot, "conversations", Guid.NewGuid().ToString("N") + ".json");
    File.WriteAllText(damaged, "broken");
    Check(store.ListConversations().Count > 0 && store.Warnings.Count == 1 && File.ReadAllText(damaged) == "broken", "Damaged conversation preserved and reported, others available");
}
Console.WriteLine($"{count} checks passed. Isolated test data: {testRoot}");

}
catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }

sealed class FakeOllama : HttpMessageHandler
{
    public string LastRequest { get; private set; } = "";
    public bool Invalid { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        LastRequest = await request.Content!.ReadAsStringAsync(token);
        bool english = LastRequest.Contains("current answer must be in English");
        string content = Invalid ? "bad json" : JsonSerializer.Serialize(new { text = english ? "Test response." : "Respuesta de prueba.", language = english ? "en" : "es" });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { message = new { content } }), Encoding.UTF8, "application/json") };
    }
}
