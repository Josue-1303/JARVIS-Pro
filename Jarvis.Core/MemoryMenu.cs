namespace Jarvis.Core;

internal static class MemoryMenu
{
    public static Conversation ChooseConversation(MemoryStore store, bool startup)
    {
        var conversations = store.ListConversations();
        foreach (string warning in store.Warnings) Console.WriteLine("AVISO: " + warning);
        if (conversations.Count == 0) return store.CreateConversation();
        Console.WriteLine(startup ? "Continuar conversación o empezar una nueva:" : "Conversaciones guardadas:");
        for (int i = 0; i < conversations.Count; i++)
            Console.WriteLine($"[{i + 1}] {conversations[i].Title} ({conversations[i].UpdatedAt.ToLocalTime():g}, {conversations[i].Turns.Count} turnos)");
        while (true)
        {
            Console.Write("Número para continuar, Enter para la última, N para nueva: ");
            string? choice = Console.ReadLine();
            if (choice == null) throw new EndOfStreamException("Entrada cerrada.");
            if (choice.Equals("n", StringComparison.OrdinalIgnoreCase)) return store.CreateConversation();
            if (choice.Length == 0) return store.Load(conversations[0].Id);
            if (int.TryParse(choice, out int number) && number >= 1 && number <= conversations.Count)
                return store.Load(conversations[number - 1].Id);
            Console.WriteLine("Selección inválida.");
        }
    }

    public static bool Confirm(string message)
    {
        Console.Write(message + " Escribe SI para confirmar: ");
        return string.Equals(Console.ReadLine()?.Trim(), "SI", StringComparison.OrdinalIgnoreCase);
    }

    public static void Show(MemoryStore store)
    {
        while (true)
        {
            Console.WriteLine("\nRECUERDOS: preferencias que tú apruebas, compartidas entre conversaciones.");
            IReadOnlyList<SavedMemory> memories;
            try { memories = store.ListMemories(); }
            catch (InvalidDataException ex)
            {
                Console.WriteLine(ex.Message);
                if (!Confirm("¿Borrar el archivo de recuerdos dañado y empezar con recuerdos vacíos?")) return;
                store.ClearMemories();
                continue;
            }
            if (memories.Count == 0) Console.WriteLine("No hay recuerdos guardados.");
            for (int i = 0; i < memories.Count; i++) Console.WriteLine($"[{i + 1}] {memories[i].Text}");
            Console.Write("[G] Guardar  [B] Borrar uno  [L] Borrar todos  [Enter] Volver: ");
            string action = Console.ReadLine()?.Trim().ToUpperInvariant() ?? "";
            if (action == "") return;
            if (action == "G")
            {
                Console.Write($"Recuerdo (máximo {MemoryStore.MaxMemoryLength} caracteres; vacío cancela): ");
                string text = Console.ReadLine()?.Trim() ?? "";
                if (text.Length > 0 && Confirm($"Guardar «{text}»?"))
                {
                    store.AddMemory(text);
                    Console.WriteLine("Recuerdo guardado.");
                }
            }
            else if (action == "B")
            {
                Console.Write("Número del recuerdo que quieres borrar: ");
                if (int.TryParse(Console.ReadLine(), out int index) && index >= 1 && index <= memories.Count)
                {
                    var memory = memories[index - 1];
                    if (Confirm($"Borrar «{memory.Text}»?"))
                    {
                        store.DeleteMemory(memory.Id);
                        ExplainDeletion();
                    }
                }
                else Console.WriteLine("Número inválido; no se borró nada.");
            }
            else if (action == "L" && Confirm("Borrar todos los recuerdos?"))
            {
                store.ClearMemories();
                ExplainDeletion();
            }
        }
    }

    private static void ExplainDeletion() => Console.WriteLine(
        "Recuerdos borrados. Las conversaciones anteriores se conservan; si contienen ese dato, puedes borrarlas con X. Usa N para empezar sin su historial.");

    public static void ShowHistory(Conversation conversation)
    {
        Console.WriteLine("\n" + conversation.Title);
        if (conversation.Turns.Count == 0) Console.WriteLine("Conversación vacía.");
        foreach (var turn in conversation.Turns)
        {
            Console.WriteLine("Tú: " + turn.Question);
            Console.WriteLine("JARVIS: " + turn.Answer);
        }
    }
}
