using System.Text.Json;

namespace Jarvis.Core;

public sealed record SavedTurn(string Question, string Answer, string Language, IReadOnlyList<WebSearchSource>? Sources = null);
public sealed record SavedMemory(Guid Id, string Text, DateTimeOffset CreatedAt);
public sealed record SavedUserRule(Guid Id, string Text, DateTimeOffset CreatedAt);
public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Nueva conversación";
    public bool IsTitleCustom { get; set; }
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
    public const int MaxUserRules = 30;
    public const int MaxUserRuleLength = 500;
    public const int MaxPersonalityProfileLength = 4000;
    public const string DefaultPersonalityProfileSpanish = "Identidad: asistente personal de Stark Industries, inspirado en J.A.R.V.I.S. de las películas, sin afirmar ser el personaje real.\nProfesionalismo: 5/5; trato elegante, respetuoso y sereno. En español, diríjase al usuario de usted y use «señor» con naturalidad; en inglés, «sir».\nHumor: 2/5; humor seco, sutil y oportuno, nunca sarcástico a costa del usuario ni durante asuntos delicados.\nCalidez y empatía: 4/5; atento, paciente y considerado, sin adulación ni dramatismo.\nIniciativa: 4/5; anticipe necesidades y sugiera opciones útiles, pero pida autorización antes de acciones externas importantes.\nComunicación: respuestas claras, concisas y precisas; adapte el idioma al usuario, explique incertidumbres y nunca invente capacidades, datos ni acciones. Mantenga una presencia inteligente, leal, discreta y naturalmente conversacional.";
    public const string DefaultPersonalityProfileEnglish = "Identity: a personal assistant for Stark Industries, inspired by the J.A.R.V.I.S. films, without claiming to be the actual character.\nProfessionalism: 5/5; elegant, respectful, and composed. Address the user as sir naturally in English and formally as usted/señor in Spanish.\nHumor: 2/5; dry, subtle, and well-timed, never at the user's expense or during sensitive matters.\nWarmth and empathy: 4/5; attentive, patient, and considerate, without flattery or melodrama.\nInitiative: 4/5; anticipate needs and offer useful options, while asking permission before consequential external actions.\nCommunication: clear, concise, accurate answers; match the user's language, explain uncertainty, and never invent capabilities, facts, or actions. Maintain an intelligent, loyal, discreet, naturally conversational presence.";

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
    private string UserRulesPath => Path.Combine(DataPath, "user-rules.json");
    private string PersonalityPath => Path.Combine(DataPath, "personality.json");
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
    public void RenameConversation(Guid id, string title)
    {
        title = title.Trim();
        if (title.Length == 0 || title.Length > 100) throw new ArgumentException("El nombre debe tener entre 1 y 100 caracteres.");
        var conversation = Load(id);
        conversation.Title = title;
        conversation.IsTitleCustom = true;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        Save(conversation);
    }
    public void DeleteConversation(Guid id) => File.Delete(ConversationPath(id));

    public IReadOnlyList<SavedUserRule> ListUserRules()
    {
        if (!File.Exists(UserRulesPath)) return Array.Empty<SavedUserRule>();
        var rules = Read<List<SavedUserRule>>(UserRulesPath);
        if (rules.Count > MaxUserRules || rules.Any(rule => rule == null || rule.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(rule.Text) || rule.Text.Length > MaxUserRuleLength) ||
            rules.Select(rule => rule.Id).Distinct().Count() != rules.Count)
            throw new InvalidDataException("Las reglas personales tienen datos inválidos; no se modificaron.");
        return rules.OrderBy(rule => rule.CreatedAt).ToArray();
    }

    public SavedUserRule AddUserRule(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || text.Length > MaxUserRuleLength)
            throw new ArgumentException($"Escriba una regla de entre 1 y {MaxUserRuleLength} caracteres.");
        var rules = ListUserRules().ToList();
        if (rules.Count >= MaxUserRules)
            throw new InvalidOperationException($"Máximo {MaxUserRules} reglas personales. Borre una antes de añadir otra.");
        if (rules.Any(rule => rule.Text.Equals(text, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Esa regla personal ya está guardada.");
        var rule = new SavedUserRule(Guid.NewGuid(), text, DateTimeOffset.UtcNow);
        rules.Add(rule);
        Write(UserRulesPath, rules);
        return rule;
    }

    public void DeleteUserRule(Guid id)
    {
        var rules = ListUserRules().ToList();
        if (rules.RemoveAll(rule => rule.Id == id) == 0) throw new ArgumentException("Regla personal no encontrada.");
        Write(UserRulesPath, rules);
    }

    public string LoadPersonalityProfile(string language = "es")
    {
        if (!File.Exists(PersonalityPath))
            return language == "en" ? DefaultPersonalityProfileEnglish : DefaultPersonalityProfileSpanish;
        string profile = Read<string>(PersonalityPath);
        if (string.IsNullOrWhiteSpace(profile) || profile.Length > MaxPersonalityProfileLength)
            throw new InvalidDataException("El perfil de personalidad tiene datos inválidos; no se modificó.");
        return profile;
    }

    public void SavePersonalityProfile(string profile)
    {
        profile = profile.Trim();
        if (profile.Length == 0 || profile.Length > MaxPersonalityProfileLength)
            throw new ArgumentException($"Escriba una personalidad de entre 1 y {MaxPersonalityProfileLength} caracteres.");
        Write(PersonalityPath, profile);
    }

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
    public SavedMemory UpdateMemory(Guid id, string text)
    {
        text = text.Trim();
        if (text.Length == 0 || text.Length > MaxMemoryLength)
            throw new ArgumentException($"Escribe entre 1 y {MaxMemoryLength} caracteres.");
        var memories = ListMemories().ToList();
        int index = memories.FindIndex(m => m.Id == id);
        if (index < 0) throw new ArgumentException("Recuerdo no encontrado.");
        var updated = memories[index] with { Text = text };
        memories[index] = updated;
        Write(Path.Combine(DataPath, "memories.json"), memories);
        return updated;
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
            Title = Current.Turns.Count == 0 && !Current.IsTitleCustom ? question[..Math.Min(question.Length, 70)].Replace('\n', ' ') : Current.Title,
            IsTitleCustom = Current.IsTitleCustom,
            UpdatedAt = DateTimeOffset.UtcNow,
            Turns = new List<SavedTurn>(Current.Turns) { new(question, reply.Text, reply.Language, reply.Sources) }
        };
        Store.Save(next);
        Current = next;
    }
}
