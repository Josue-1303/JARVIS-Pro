
using System;
using System.Threading.Tasks;

namespace Jarvis.Core
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try { await RunAsync(args); }
            catch (Exception ex)
            {
                Console.Error.WriteLine("No se pudo iniciar JARVIS: " + ex.Message);
                Environment.ExitCode = 1;
            }
        }

        private static async Task RunAsync(string[] args)
        {
            if (Array.Exists(args, arg => arg == "--diagnostics")) { await Diagnostics.RunAsync(); return; }
            if (Array.Exists(args, arg => arg == "--test-cancel-console"))
            {
                var speech = new PiperSpeechService();
                await CancellableOperation.RunAsync(ct => speech.SpeakAsync(
                    string.Join(" ", Enumerable.Repeat("Esta es una prueba de la tecla Escape. La voz debe detenerse al pulsarla. Continuaré hablando para que puedas comprobar que la interrupción funciona correctamente.", 4)),
                    "es", ct, stage => Console.WriteLine("TEST STAGE: " + stage)));
                Console.WriteLine("TEST: control devuelto a la consola.");
                return;
            }
            if (Array.Exists(args, arg => arg == "--test-voices"))
            {
                var speech = new PiperSpeechService();
                await speech.SpeakAsync("Hola. Soy JARVIS. La voz en español está lista.", "es");
                Console.WriteLine("PASS: español, síntesis y reproducción completadas.");
                await speech.SpeakAsync("Good evening, sir. The English voice is ready.", "en");
                Console.WriteLine("PASS: English synthesis and playback completed.");
                return;
            }

            if (Array.Exists(args, arg => arg == "--test-auto-voices"))
            {
                await AutoVoiceCheck.RunAsync();
                return;
            }

            Console.WriteLine("J.A.R.V.I.S. PRO V0.7.2 — mantenimiento de respuestas y voz");
            var voiceSelection = new VoiceSelection();
            int dataArgument = Array.IndexOf(args, "--data-dir");
            if (dataArgument >= 0 && dataArgument + 1 >= args.Length) throw new ArgumentException("Falta la ruta después de --data-dir.");
            using var store = new MemoryStore(dataArgument >= 0 ? args[dataArgument + 1] : MemoryStore.FindDataPath());
            var session = new ConversationSession(store, MemoryMenu.ChooseConversation(store, startup: true));
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            JarvisAI jarvis = new JarvisAI(client, session);
            using VoiceService voice = new VoiceService();
            var conversationInput = new ConversationInput(voice.Settings);
            if (Array.Exists(args, arg => arg == "--text-mode")) conversationInput.Pause();

            Console.WriteLine("==============================");
            Console.WriteLine("    J.A.R.V.I.S. PRO V0.7.2   ");
            Console.WriteLine("==============================");

            Console.WriteLine("Model: Qwen3 8B");
            Console.WriteLine("Piper: selección automática. [1] Español / [2] English / [A] Automático.");
            Console.WriteLine("Listo. Haz clic en esta terminal para que reciba Esc / Ctrl+C.");
            Console.WriteLine("1/2 solicitan respuestas y voz en español/inglés; A vuelve al idioma de cada pregunta.");

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("[SPACE] Speak");
                Console.WriteLine("[T] Type a message");
                Console.WriteLine($"[V] {(conversationInput.WakeEnabled ? "Pausar" : "Activar")} escucha | {(conversationInput.WakeEnabled ? "Micrófono activo en espera" : "Escucha pausada")}");
                Console.WriteLine("[N] Nueva conversación  [C] Continuar otra  [H] Ver historial");
                Console.WriteLine("[M] Consultar/guardar/borrar recuerdos  [X] Borrar conversación actual");
                Console.WriteLine($"Conversación: {session.Current.Title}");
                Console.WriteLine($"[1] Español  [2] English  [A] Automático | Modo: {voiceSelection.Description}");
                Console.WriteLine("[ESC] Exit");
                Console.WriteLine("Durante una respuesta: [ESC] o [Ctrl+C] cancela / detiene la voz.");
                Console.WriteLine();

                IdleInput idle;
                if (conversationInput.WakeEnabled)
                {
                    try { idle = await conversationInput.WaitAsync(voice.WaitForWakeAsync, voice.WaitForFollowUpAsync); }
                    catch (Exception ex)
                    {
                        conversationInput.Pause();
                        Console.WriteLine("Escucha automática pausada: " + ex.Message);
                        Console.WriteLine("Usa T para escribir o V para reintentar.");
                        continue;
                    }
                }
                else idle = new IdleInput(Console.ReadKey(true).Key, null);
                ConsoleKey? key = idle.Key;
                if (key == ConsoleKey.V) { conversationInput.Toggle(); continue; }

                if (key == ConsoleKey.Escape)
                {
                    Console.WriteLine(
                        "JARVIS: Shutting down, sir."
                    );

                    break;
                }

                if (key.HasValue && voiceSelection.HandleKey(key.Value))
                {
                    Console.WriteLine("Voz: " + voiceSelection.Description);
                    continue;
                }

                string input = idle.Text ?? "";
                if (idle.Text != null) Console.WriteLine("You: " + input);

                try
                {
                    if (key == ConsoleKey.M) { MemoryMenu.Show(store); continue; }
                    if (key == ConsoleKey.H) { MemoryMenu.ShowHistory(session.Current); continue; }
                    if (key == ConsoleKey.N || key == ConsoleKey.C || key == ConsoleKey.X)
                    {
                        Conversation next;
                        if (key == ConsoleKey.X)
                        {
                            if (!MemoryMenu.Confirm($"Borrar la conversación «{session.Current.Title}» y todo su historial?")) continue;
                            next = store.CreateConversation();
                            store.DeleteConversation(session.Current.Id);
                        }
                        else next = key == ConsoleKey.N ? store.CreateConversation() : MemoryMenu.ChooseConversation(store, startup: false);
                        session = new ConversationSession(store, next);
                        jarvis = new JarvisAI(client, session);
                        continue;
                    }
                    if (key == ConsoleKey.Spacebar)
                    {
                        using var stopRecording = new CancellationTokenSource();
                        await CancellableOperation.RunAsync(async ct =>
                        {
                            input = await voice.ListenAsync(ct, stopRecording.Token);
                        }, () =>
                        {
                            if (Console.IsInputRedirected || !Console.KeyAvailable) return null;
                            var pressed = Console.ReadKey(true).Key;
                            if (pressed == ConsoleKey.Spacebar) { stopRecording.Cancel(); return null; }
                            return pressed;
                        });

                        Console.WriteLine(
                            "You: " + input
                        );
                    }
                    else if (key == ConsoleKey.T)
                    {
                        Console.Write("You: ");

                        input = Console.ReadLine() ?? "";
                    }
                    else if (idle.Text == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(input))
                    {
                        Console.WriteLine(
                            "No input detected."
                        );

                        continue;
                    }

                    await CancellableOperation.RunAsync(async cancellationToken =>
                    {
                        Console.WriteLine("Pensando... [ESC] o [Ctrl+C] para cancelar.");
                        JarvisReply answer = await jarvis.AskReplyAsync(input, cancellationToken, voiceSelection.ManualLanguage);
                        Console.WriteLine("\nJARVIS: " + answer.Text);
                        Console.WriteLine("Respuesta guardada. Detener la voz conserva este texto en el historial.");
                        string language = voiceSelection.Resolve(answer);
                        Console.WriteLine($"Voz: {(language == "es" ? "Español" : "English")} ({voiceSelection.Description})");
                        await voice.SpeakAsync(answer.Text, language, cancellationToken, stage =>
                            Console.WriteLine(stage == SpeechStage.Synthesizing
                                ? "Preparando voz... [ESC] para cancelar."
                                : "Hablando... [ESC] para detener."));
                        conversationInput.ResponseFinished();
                    });
                    // Let the speaker tail settle before opening the microphone again.
                    if (conversationInput.WakeEnabled) await Task.Delay(300);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("La solicitud superó el tiempo de espera. Puedes volver a intentarlo.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        "ERROR: " + ex.Message
                    );
                }
            }
        }
    }
}
