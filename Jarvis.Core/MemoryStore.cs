using System.Text.Json;

namespace Jarvis.Core;

public sealed record SavedTurn(string Question, string Answer, string Language);
public sealed record SavedMemory(Guid Id, string Text, DateTimeOffset CreatedAt);
public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Nueva conversación";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<SavedTurn> Turns { get; set; } = new();
}

public sealed class MemoryStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly FileStream lease;
    public string DataPath { get; }
    public List<string> Warnings { get; } = new();
    public const int MaxMemories = 20;
    public const int MaxMemoryLength = 300;

    public static string FindDataPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".venv-piper"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("No se encontró la carpeta JARVIS-Pro."), "data");
    }

    public MemoryStore(string dataPath)
    {
        DataPath = Path.GetFullPath(dataPath);
        Directory.CreateDirectory(Path.Combine(DataPath, "conversations"));
        try { lease = new FileStream(Path.Combine(DataPath, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new IOException("No se pudo abrir la memoria. Comprueba si otra instancia de JARVIS está usando data.", ex); }
    }

    private string ConversationPath(Guid id) => Path.Combine(DataPath, "conversations", id.ToString("N") + ".json");
    private static T Read<T>(string path)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? throw new JsonException("Contenido nulo."); }
        catch (JsonException ex) { throw new InvalidDataException($"Archivo de memoria inválido; se conservó sin cambios: {path}", ex); }
    }
    private static void Write<T>(string path, T value)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, Json);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Validate(Conversation conversation, Guid id)
    {
        if (conversation.Id != id || conversation.Title == null || conversation.Turns == null ||
            conversation.Turns.Any(t => t == null || string.IsNullOrWhiteSpace(t.Question) || string.IsNullOrWhiteSpace(t.Answer)))
            throw new InvalidDataException("La conversación tiene datos inválidos; no se modificó.");
    }
    public Conversation Load(Guid id)
    {
        var conversation = Read<Conversation>(ConversationPath(id));
        Validate(conversation, id);
        return conversation;
    }
    public IReadOnlyList<Conversation> ListConversations()
    {
        Warnings.Clear();
        var result = new List<Conversation>();
        foreach (string path in Directory.EnumerateFiles(Path.Combine(DataPath, "conversations"), "*.json"))
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id)) continue;
            try { result.Add(Load(id)); }
            catch (Exception ex) when (ex is InvalidDataException or IOException) { Warnings.Add(ex.Message); }
        }
        return result.OrderByDescending(c => c.UpdatedAt).ToList();
    }
    public Conversation CreateConversation()
    {
        var conversation = new Conversation();
        Write(ConversationPath(conversation.Id), conversation);
        return conversation;
    }
    public void Save(Conversation conversation)
    {
        Validate(conversation, conversation.Id);
        Write(ConversationPath(conversation.Id), conversation);
    }
    public void DeleteConversation(Guid id) => File.Delete(ConversationPath(id));

    public IReadOnlyList<SavedMemory> ListMemories()
    {
        string path = Path.Combine(DataPath, "memories.json");
        if (!File.Exists(path)) return Array.Empty<SavedMemory>();
        var memories = Read<List<SavedMemory>>(path);
        if (memories.Count > MaxMemories || memories.Any(m => m == null || m.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(m.Text) || m.Text.Length > MaxMemoryLength) ||
            memories.Select(m => m.Id).Distinct().Count() != memories.Count)
            throw new InvalidDataException("Los recuerdos tienen datos inválidos; no se modificaron.");
        return memories;
    }
    public SavedMemory AddMemory(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || text.Length > MaxMemoryLength)
            throw new ArgumentException($"Escribe entre 1 y {MaxMemoryLength} caracteres.");
        var memories = ListMemories().ToList();
        if (memories.Count >= MaxMemories) throw new InvalidOperationException($"Máximo {MaxMemories} recuerdos. Borra uno antes de añadir otro.");
        var memory = new SavedMemory(Guid.NewGuid(), text, DateTimeOffset.UtcNow);
        memories.Add(memory);
        Write(Path.Combine(DataPath, "memories.json"), memories);
        return memory;
    }
    public void DeleteMemory(Guid id)
    {
        var memories = ListMemories().ToList();
        if (memories.RemoveAll(m => m.Id == id) == 0) throw new ArgumentException("Recuerdo no encontrado.");
        Write(Path.Combine(DataPath, "memories.json"), memories);
    }
    public void ClearMemories() => Write(Path.Combine(DataPath, "memories.json"), new List<SavedMemory>());
    public void Dispose() => lease.Dispose();
}

public sealed class ConversationSession
{
    public MemoryStore Store { get; }
    public Conversation Current { get; private set; }
    public ConversationSession(MemoryStore store, Conversation current) { Store = store; Current = current; }
    public void Append(string question, JarvisReply reply)
    {
        var next = new Conversation
        {
            Id = Current.Id,
            Title = Current.Turns.Count == 0 ? question[..Math.Min(question.Length, 70)].Replace('\n', ' ') : Current.Title,
            UpdatedAt = DateTimeOffset.UtcNow,
            Turns = new List<SavedTurn>(Current.Turns) { new(question, reply.Text, reply.Language) }
        };
        Store.Save(next);
        Current = next;
    }
}
