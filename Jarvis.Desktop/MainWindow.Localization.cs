using System.IO;
using System.Windows;
using System.Windows.Controls;
using Jarvis.Core;

namespace Jarvis.Desktop;

public partial class MainWindow
{
    private static readonly IReadOnlyDictionary<string, (string Spanish, string English)> InterfaceText =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["brandCaption"] = ("PRO  ·  ASISTENTE LOCAL", "PRO  ·  LOCAL ASSISTANT"),
            ["newConversation"] = ("＋  Nueva conversación", "＋  New conversation"),
            ["memories"] = ("◇  Recuerdos", "◇  Memories"),
            ["rules"] = ("◈  Reglas", "◈  Rules"),
            ["personality"] = ("◉  Personalidad", "◉  Personality"),
            ["backToChat"] = ("←  Volver al chat", "←  Back to chat"),
            ["memoryTitle"] = ("Memoria de J.A.R.V.I.S.", "J.A.R.V.I.S. memory"),
            ["memoryTitleUpper"] = ("MEMORIA PERSISTENTE", "PERSISTENT MEMORY"),
            ["memoryModeHint"] = ("Seleccione un recuerdo para editarlo o escriba uno abajo para guardarlo.", "Select a memory to edit it, or write one below to save it."),
            ["memoriesUpper"] = ("RECUERDOS", "MEMORIES"),
            ["memoryEmpty"] = ("Aún no hay recuerdos guardados.", "No memories saved yet."),
            ["memoryEditorHint"] = ("Escriba un recuerdo en el cuadro inferior y pulse Guardar.", "Write a memory in the box below and select Save."),
            ["saveMemory"] = ("Guardar  ✓", "Save  ✓"),
            ["deleteMemory"] = ("Borrar", "Delete"),
            ["confirmDeleteMemory"] = ("¿Desea borrar este recuerdo? Esta acción no se puede deshacer.", "Delete this memory? This action cannot be undone."),
            ["memorySaved"] = ("Recuerdo guardado en la memoria", "Memory saved"),
            ["memoryDeleted"] = ("Recuerdo eliminado", "Memory deleted"),
            ["memoryShortcut"] = ("Intro guarda · Mayús+Intro añade una línea; seleccione para editar", "Enter saves · Shift+Enter adds a line; select to edit"),
            ["memoryBrainDescription"] = ("Red neuronal de memoria de J.A.R.V.I.S.", "J.A.R.V.I.S. memory neural network"),
            ["rulesTitle"] = ("DIRECTRICES ESENCIALES", "CORE DIRECTIVES"),
            ["rulesHint"] = ("Principios de seguridad, legalidad y respeto que guían cada respuesta.", "Safety, legality, and respect principles that guide every response."),
            ["rulesListTitle"] = ("PRINCIPIOS NO EDITABLES", "FIXED CORE PRINCIPLES"),
            ["rulesFooter"] = ("Directrices permanentes de la aplicación; un modelo de lenguaje no puede garantizar infalibilidad técnica.", "Permanent application guidance; a language model cannot be made technically infallible."),
            ["immutableRule"] = ("INMUTABLE", "IMMUTABLE"),
            ["personalRule"] = ("SU REGLA", "YOUR RULE"),
            ["userRulePriority"] = ("Preferencia personal, subordinada a las directrices esenciales.", "Personal preference, subordinate to core directives."),
            ["addRule"] = ("＋ Agregar regla", "＋ Add rule"),
            ["deleteUserRule"] = ("Borrar mi regla", "Delete my rule"),
            ["ruleInputTip"] = ("Escriba una regla personal (máximo 500 caracteres). Intro agrega; Mayús+Intro añade una línea.", "Write a personal rule (up to 500 characters). Enter adds; Shift+Enter adds a line."),
            ["confirmDeleteUserRule"] = ("¿Desea borrar esta regla personal? Las ocho directrices esenciales no se pueden borrar.", "Delete this personal rule? The eight core directives cannot be deleted."),
            ["userRuleSaved"] = ("Regla personal guardada", "Personal rule saved"),
            ["userRuleDeleted"] = ("Regla personal eliminada", "Personal rule deleted"),
            ["renameConversation"] = ("Cambiar nombre", "Rename conversation"),
            ["deleteConversation"] = ("Eliminar conversación", "Delete conversation"),
            ["renameConversationPrompt"] = ("Escriba un nombre para esta conversación:", "Enter a name for this conversation:"),
            ["confirmDeleteConversation"] = ("¿Desea eliminar esta conversación y su historial? Esta acción no se puede deshacer.", "Delete this conversation and its history? This cannot be undone."),
            ["conversationRenamed"] = ("Conversación renombrada", "Conversation renamed"),
            ["personalityTitle"] = ("PERSONALIDAD DE J.A.R.V.I.S.", "J.A.R.V.I.S. PERSONALITY"),
            ["personalityCurrentTitle"] = ("PERSONALIDAD ACTUAL", "CURRENT PERSONALITY"),
            ["personalityHint"] = ("Ajuste el estilo, el humor y la iniciativa del asistente. El núcleo verde representa su presencia e identidad.", "Adjust the assistant's style, humor, and initiative. The green core represents its presence and identity."),
            ["personalityInputTip"] = ("Escriba la personalidad completa que desea guardar. Intro guarda; Mayús+Intro añade una línea.", "Write the full personality profile to save. Enter saves; Shift+Enter adds a line."),
            ["savePersonality"] = ("Guardar personalidad ✓", "Save personality ✓"),
            ["resetPersonality"] = ("Restaurar valores base", "Restore defaults"),
            ["confirmResetPersonality"] = ("¿Desea restaurar la personalidad base de J.A.R.V.I.S.? Se reemplazará el perfil actual.", "Restore J.A.R.V.I.S.'s default personality? This replaces the current profile."),
            ["personalitySaved"] = ("Personalidad guardada", "Personality saved"),
            ["personalityCoreDescription"] = ("Núcleo holográfico verde de personalidad de J.A.R.V.I.S.", "J.A.R.V.I.S. green holographic personality core"),
            ["renameSave"] = ("Guardar", "Save"),
            ["renameCancel"] = ("Cancelar", "Cancel"),
            ["rulesBrainDescription"] = ("Núcleo de reglas de J.A.R.V.I.S.", "J.A.R.V.I.S. rules core"),
            ["newConversationTitle"] = ("Nueva conversación", "New conversation"),
            ["conversations"] = ("CONVERSACIONES", "CONVERSATIONS"),
            ["interfaceLanguage"] = ("IDIOMA DE INTERFAZ", "INTERFACE LANGUAGE"),
            ["localExecution"] = ("EJECUCIÓN LOCAL", "LOCAL EXECUTION"),
            ["voiceMemory"] = ("Voz y memoria en este equipo", "Voice and memory on this device"),
            ["internetAccess"] = ("ACCESO A INTERNET", "INTERNET ACCESS"),
            ["internetOn"] = ("ACTIVADO", "ON"),
            ["internetOff"] = ("DESACTIVADO", "OFF"),
            ["internetEnabledStatus"] = ("Acceso a Internet activado", "Internet access enabled"),
            ["internetDisabledStatus"] = ("Acceso a Internet desactivado", "Internet access disabled"),
            ["internetSettingsError"] = ("No se pudo guardar el ajuste de Internet", "Could not save the Internet setting"),
            ["internetOffHint"] = ("Acceso a Internet desactivado", "Internet access is off"),
            ["internetOffLocationTip"] = ("Active Acceso a Internet para permitir la ubicación y el pronóstico.", "Turn on Internet access to enable location and weather."),
            ["webSearchKeyMissing"] = ("BÚSQUEDA WEB · SIN CONFIGURAR", "WEB SEARCH · NOT CONFIGURED"),
            ["webSearchKeyReady"] = ("BÚSQUEDA WEB · CONFIGURADA", "WEB SEARCH · CONFIGURED"),
            ["webSearchConfigure"] = ("Configurar", "Configure"),
            ["webSearchDialogTitle"] = ("Configurar búsqueda web", "Configure web search"),
            ["webSearchDialogDescription"] = ("Pegue aquí su clave de API de Ollama. Windows la cifrará para su cuenta. Al buscar, solo se envía la consulta actual; no se envían recuerdos ni el historial.", "Paste your Ollama API key here. Windows will encrypt it for your account. Searches send only the current query; memories and chat history are not sent."),
            ["webSearchKeyLabel"] = ("CLAVE DE API", "API KEY"),
            ["webSearchSaveKey"] = ("Guardar clave", "Save key"),
            ["webSearchRemoveKey"] = ("Eliminar clave", "Remove key"),
            ["webSearchCancel"] = ("Cancelar", "Cancel"),
            ["webSearchOpenKeys"] = ("Abrir configuración de claves de Ollama ↗", "Open Ollama API key settings ↗"),
            ["webSearchKeySaved"] = ("Clave de búsqueda guardada y cifrada en este equipo", "Web search key saved and encrypted on this device"),
            ["webSearchKeyRemoved"] = ("Clave de búsqueda eliminada", "Web search key removed"),
            ["automatic"] = ("Automático", "Automatic"),
            ["spanish"] = ("Español", "Spanish"),
            ["english"] = ("English", "English"),
            ["stop"] = ("■  Detener", "■  Stop"),
            ["localNeuralCore"] = ("LOCAL NEURAL CORE", "LOCAL NEURAL CORE"),
            ["localProcessing"] = ("PROCESAMIENTO LOCAL", "LOCAL PROCESSING"),
            ["systemActive"] = ("SISTEMA ACTIVO", "SYSTEM ACTIVE"),
            ["localTime"] = ("HORA LOCAL", "LOCAL TIME"),
            ["localClock"] = ("RELOJ DEL DISPOSITIVO", "DEVICE CLOCK"),
            ["devicePosition"] = ("UBICACIÓN DEL DISPOSITIVO", "DEVICE LOCATION"),
            ["enableLocation"] = ("Activar ubicación", "Enable location"),
            ["allowLocation"] = ("Permitir ubicación", "Allow location"),
            ["locationPermission"] = ("Solo con tu permiso", "Only with your permission"),
            ["locationRequesting"] = ("Solicitando permiso...", "Requesting permission..."),
            ["locationActive"] = ("Ubicación activa · solo en memoria", "Location active · memory only"),
            ["locationDenied"] = ("Windows denegó el permiso", "Windows denied permission"),
            ["locationDeniedTip"] = ("En Configuración > Privacidad y seguridad > Ubicación, activa Servicios de ubicación y Permitir que las aplicaciones de escritorio accedan a tu ubicación.", "In Settings > Privacy & security > Location, turn on Location services and Let desktop apps access your location."),
            ["locationUnavailableTip"] = ("No se pudo obtener la ubicación del dispositivo.", "Could not retrieve this device's location."),
            ["locationDiagnostic"] = ("Pasa el cursor para ver el diagnóstico", "Hover for the diagnostic"),
            ["locationUnavailable"] = ("Ubicación no disponible", "Location unavailable"),
            ["locationTip"] = ("La ubicación se solicita solo al pulsar aquí. J.A.R.V.I.S. no la guarda.", "Location is requested only when you click here. J.A.R.V.I.S. does not save it."),
            ["locationPrivacyTip"] = ("Ubicación temporal en memoria. Las coordenadas se envían a Open-Meteo al pedir el pronóstico y no se guardan.", "Location is held in memory temporarily. Coordinates are sent to Open-Meteo for the forecast and are not saved."),
            ["localWeather"] = ("CLIMA LOCAL", "LOCAL WEATHER"),
            ["locationOff"] = ("Ubicación desactivada", "Location is off"),
            ["weatherTip"] = ("Pronóstico gratuito de Open-Meteo. Para consultar el clima se envían las coordenadas a su servicio.", "Free Open-Meteo forecast. Coordinates are sent to its service to retrieve local weather."),
            ["temperatureRange"] = ("↑ {0}°   ↓ {1}°", "↑ {0}°   ↓ {1}°"),
            ["precipitationChance"] = ("Prob. precipitación {0}%", "Precipitation chance {0}%"),
            ["todayForecast"] = ("Pronóstico de hoy", "Today's forecast"),
            ["weatherUpdating"] = ("OPEN-METEO · actualizando", "OPEN-METEO · updating"),
            ["weatherOffline"] = ("OPEN-METEO · sin conexión", "OPEN-METEO · offline"),
            ["weatherUpdated"] = ("OPEN-METEO · {0}", "OPEN-METEO · {0}"),
            ["weatherNoForecast"] = ("Pronóstico no disponible", "Forecast unavailable"),
            ["latestNews"] = ("NOTICIAS RECIENTES", "LATEST NEWS"),
            ["newsLoading"] = ("Cargando noticias…", "Loading news…"),
            ["newsOffline"] = ("Noticias no disponibles. Se volverá a intentar más tarde.", "News unavailable. J.A.R.V.I.S. will retry later."),
            ["newsUpdated"] = ("Actualizado {0}", "Updated {0}"),
            ["newsUnknownTime"] = ("Hora no disponible", "Time unavailable"),
            ["newsOpenFailed"] = ("No se pudo abrir el artículo.", "Could not open the article."),
            ["clear"] = ("Despejado", "Clear"),
            ["mostlyClear"] = ("Mayormente despejado", "Mostly clear"),
            ["partlyCloudy"] = ("Parcialmente nublado", "Partly cloudy"),
            ["overcast"] = ("Nublado", "Overcast"),
            ["fog"] = ("Neblina", "Fog"),
            ["drizzle"] = ("Llovizna", "Drizzle"),
            ["rain"] = ("Lluvia", "Rain"),
            ["snow"] = ("Nieve", "Snow"),
            ["thunderstorm"] = ("Tormenta", "Thunderstorm"),
            ["variable"] = ("Condición variable", "Variable conditions"),
            ["speak"] = ("🎙  Hablar", "🎙  Speak"),
            ["send"] = ("Enviar  ➤", "Send  ➤"),
            ["speakReplies"] = ("Leer respuestas en voz alta", "Read replies aloud"),
            ["shortcuts"] = ("Intro envía · Mayús+Intro añade una línea", "Enter sends · Shift+Enter adds a line"),
            ["ready"] = ("J.A.R.V.I.S. está listo", "J.A.R.V.I.S. is ready"),
            ["thinking"] = ("J.A.R.V.I.S. está pensando", "J.A.R.V.I.S. is thinking"),
            ["operationStopped"] = ("Operación detenida. Puedes continuar la conversación.", "Operation stopped. You can continue the conversation."),
            ["operationFailed"] = ("No se pudo completar la operación.", "The operation could not be completed."),
            ["listening"] = ("Te escucho; terminaré cuando detecte silencio", "Listening; I'll stop when I detect silence"),
            ["noSpeech"] = ("No detecté una frase. Puedes intentarlo otra vez.", "I didn't detect speech. Please try again."),
            ["stopping"] = ("Deteniendo...", "Stopping..."),
            ["newConversationStatus"] = ("Conversación nueva", "New conversation"),
            ["conversationLoaded"] = ("Conversación cargada", "Conversation loaded"),
            ["responseSpanish"] = ("Respuesta solicitada en español", "Spanish reply selected"),
            ["responseEnglish"] = ("Respuesta solicitada en inglés", "English reply selected"),
            ["closing"] = ("Cerrando de forma segura...", "Closing safely..."),
            ["speakingSpanish"] = ("J.A.R.V.I.S. está hablando · di «Jarvis, detente» o pulsa Detener", "J.A.R.V.I.S. is speaking · say “Jarvis, stop” or click Stop"),
            ["speakingEnglish"] = ("J.A.R.V.I.S. está hablando · di «Jarvis, stop» o pulsa Detener", "J.A.R.V.I.S. is speaking · say “Jarvis, stop” or click Stop"),
            ["synthesizing"] = ("Preparando voz...", "Preparing voice..."),
            ["interruptRecognized"] = ("Orden de interrupción reconocida", "Interruption command recognized"),
            ["interruptMicUnavailable"] = ("Micrófono de interrupción no disponible", "Interruption microphone unavailable"),
            ["speechStopped"] = ("Voz detenida; la respuesta quedó guardada", "Speech stopped; the reply was saved"),
            ["you"] = ("TÚ", "YOU"),
            ["dialogTitle"] = ("J.A.R.V.I.S. Pro", "J.A.R.V.I.S. Pro"),
            ["historyTitle"] = ("Historial", "History")
        };

    private string interfaceLanguage = "es";
    private string statusKey = "ready";
    private string locationStatusKey = "locationPermission";
    private string? statusDetail;
    private bool applyingInterfaceLanguage;

    private string T(string key)
    {
        if (!InterfaceText.TryGetValue(key, out var text)) return key;
        return interfaceLanguage == "en" ? text.English : text.Spanish;
    }

    private void InitializeInterfaceLanguage()
    {
        string path = Path.Combine(store.DataPath, "interface-language.txt");
        try { interfaceLanguage = File.Exists(path) && File.ReadAllText(path).Trim().Equals("en", StringComparison.OrdinalIgnoreCase) ? "en" : "es"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { interfaceLanguage = "es"; }
        ApplyInterfaceLanguage();
    }

    private void InterfaceLanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (applyingInterfaceLanguage || InterfaceLanguageSelector.SelectedItem is not ComboBoxItem item) return;
        string language = item.Tag?.ToString() == "en" ? "en" : "es";
        if (interfaceLanguage == language) return;
        interfaceLanguage = language;
        try { File.WriteAllText(Path.Combine(store.DataPath, "interface-language.txt"), interfaceLanguage); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        ApplyInterfaceLanguage();
    }

    private void ApplyInterfaceLanguage()
    {
        applyingInterfaceLanguage = true;
        InterfaceLanguageSelector.SelectedIndex = interfaceLanguage == "en" ? 1 : 0;
        applyingInterfaceLanguage = false;

        BrandCaptionText.Text = T("brandCaption");
        NewConversationButton.Content = T("newConversation");
        UpdateMemoryModeLabels();
        ConversationsLabel.Text = T("conversations");
        InterfaceLanguageLabel.Text = T("interfaceLanguage");
        LocalExecutionTitle.Text = T("localExecution");
        LocalExecutionDetails.Text = T("voiceMemory");
        InternetAccessLabel.Text = T("internetAccess");
        InternetAccessStateText.Text = T(internetAccessEnabled ? "internetOn" : "internetOff");
        InternetAccessToggle.ToolTip = interfaceLanguage == "en"
            ? "Allow J.A.R.V.I.S. to retrieve news and weather from Internet services."
            : "Permite que J.A.R.V.I.S. consulte noticias y el pronóstico en servicios de Internet.";
        ConfigureWebSearchButton.Content = T("webSearchConfigure");
        WebSearchKeyStatusText.Text = T(WebSearchCredentialStore.HasKey(store.DataPath) ? "webSearchKeyReady" : "webSearchKeyMissing");
        WebSearchKeyStatusText.Foreground = WebSearchCredentialStore.HasKey(store.DataPath)
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(89, 214, 206))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(133, 143, 169));
        VoiceLanguageAutomatic.Content = T("automatic");
        VoiceLanguageSpanish.Content = T("spanish");
        VoiceLanguageEnglish.Content = T("english");
        StopButton.Content = T("stop");
        LocalCoreLabel.Text = T("localNeuralCore");
        LocalProcessingLabel.Text = T("localProcessing");
        SystemActiveLabel.Text = T("systemActive");
        ClockTitleText.Text = T("localTime");
        ClockFooterText.Text = T("localClock");
        LocationTitleText.Text = T("devicePosition");
        LocationButton.ToolTip = !internetAccessEnabled ? T("internetOffLocationTip") : currentDeviceLocation is null ? T("locationTip") : T("locationPrivacyTip");
        WeatherTitleText.Text = T("localWeather");
        WeatherPanel.ToolTip = T("weatherTip");
        NewsTitleText.Text = T("latestNews");
        NewsList.ItemsSource = latestNews.Select(CreateNewsView).ToArray();
        RenderNewsStatus();
        NewsSourceText.Text = "YLE · RSS";
        MicrophoneButton.Content = T("speak");
        SendButton.Content = T("send");
        SpeakCheckBox.Content = T("speakReplies");
        ShortcutText.Text = T("shortcuts");
        LocationText.Text = currentDeviceLocation?.PlaceName ?? T(locationPermissionDenied ? "allowLocation" : "enableLocation");
        LocationStatusText.Text = T(locationStatusKey);
        StatusText.Text = T(statusKey) + (statusDetail is null ? string.Empty : ": " + statusDetail);
        UpdateExistingSpeakerLabels();
        if (session is not null && HistoryList is not null) RefreshHistory(session.Current.Id);
        UpdateLocalClock();
        RenderWeatherHud();
        RenderNewsStatus();
        UpdateMemoryModeLabels();
    }

    private void SetStatus(string key, string? detail = null)
    {
        statusKey = key;
        statusDetail = detail;
        StatusText.Text = T(key) + (detail is null ? string.Empty : ": " + detail);
    }

    private void UpdateExistingSpeakerLabels()
    {
        for (int i = 0; i < messages.Count; i++)
        {
            ChatMessageView message = messages[i];
            string speaker = message.IsUser ? T("you") : "J.A.R.V.I.S.";
            if (!string.Equals(message.Speaker, speaker, StringComparison.Ordinal))
                message.SetSpeaker(speaker);
        }
    }
}
