using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Jarvis.Core;

namespace Jarvis.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer cursorTrackingTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly MemoryStore store;
    private readonly HttpClient httpClient;
    private readonly OllamaWebSearchClient webSearchClient = new();
    private readonly VoiceService voice;
    private readonly ObservableCollection<ChatMessageView> messages = new();
    private readonly ObservableCollection<ConversationEntry> conversations = new();
    private ConversationSession session;
    private JarvisAI jarvis;
    private CancellationTokenSource? operation;
    private string? preferredLanguage;
    private bool loadingHistory;
    private bool closingAfterOperation;
    private bool allowClose;
    private bool disposed;
    private HwndSource? windowMessageSource;
    private const int WmMouseHWheel = 0x020E;

    public MainWindow()
    {
        InitializeComponent();
        store = new MemoryStore(MemoryStore.FindDataPath());
        httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        voice = new VoiceService();

        var recent = store.ListConversations().FirstOrDefault();
        session = new ConversationSession(store, recent ?? store.CreateConversation());
        jarvis = CreateJarvis(session);
        MessageList.ItemsSource = messages;
        HistoryList.ItemsSource = conversations;
        LoadCurrentConversation();
        RefreshHistory(session.Current.Id);
        SetCoreState(CoreMode.Ready);
        InitializeInterfaceLanguage();
        InitializeInternetAccess();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        windowMessageSource = PresentationSource.FromVisual(this) as HwndSource;
        windowMessageSource?.AddHook(WindowMessageHook);
        cursorTrackingTimer.Tick += UpdateCursorTracking;
        cursorTrackingTimer.Start();
        StartEnvironmentHud();
        MessageInput.Focus();
    }

    private void UpdateCursorTracking(object? sender, EventArgs e)
    {
        if (!ChatNuclei.IsVisible || !GetCursorPos(out NativePoint point)) return;
        Point cursorInCore = ParticleCore.PointFromScreen(new Point(point.X, point.Y));
        double width = Math.Max(1, ParticleCore.ActualWidth);
        double height = Math.Max(1, ParticleCore.ActualHeight);
        double x = cursorInCore.X / width * 2 - 1;
        double y = cursorInCore.Y / height * 2 - 1;
        ParticleCore.TrackCursor(x, y);
        ChatRulesCore.TrackCursor(x, y);
        ChatPersonalityCore.TrackCursor(x, y);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (rulesMode) return;
        if (memoryMode) await SaveMemoryAsync();
        else await SendQuestionAsync(MessageInput.Text.Trim());
    }

    private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (rulesMode)
        {
            if (e.Key == Key.Enter) e.Handled = true;
            return;
        }
        if (memoryMode)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                await SaveMemoryAsync();
            }
            return;
        }
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            await SendQuestionAsync(MessageInput.Text.Trim());
        }
    }

    private async Task SendQuestionAsync(string question)
    {
        if (operation != null || string.IsNullOrWhiteSpace(question)) return;
        MessageInput.Clear();
        messages.Add(new ChatMessageView(T("you"), question, true));
        ScrollToLatest();
        await RunOperationAsync(async source =>
        {
            SetCoreState(CoreMode.Thinking);
            SetStatus("thinking");
            var reply = await jarvis.AskReplyAsync(question, source.Token, preferredLanguage);
            var assistantMessage = CreateAssistantMessage(reply);
            messages.Add(assistantMessage);
            RefreshHistory(session.Current.Id);
            ScrollToLatest();
            await SpeakReplyIfEnabled(reply, source.Token, assistantMessage);
        });
    }

    private async Task RunOperationAsync(Func<CancellationTokenSource, Task> work)
    {
        if (operation != null) return;
        var source = new CancellationTokenSource();
        operation = source;
        SetBusy(true);
        try { await work(source); }
        catch (OperationCanceledException) { SetStatus("operationStopped"); }
        catch (Exception ex)
        {
            SetCoreState(CoreMode.Error);
            SetStatus("operationFailed");
            MessageBox.Show(this, ex.Message, T("dialogTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (ReferenceEquals(operation, source)) operation = null;
            source.Dispose();
            SetBusy(false);
            if (!closingAfterOperation)
            {
                SetCoreState(CoreMode.Ready);
                if (statusKey is "thinking" or "ready") SetStatus("ready");
                if (!memoryMode && !rulesMode && !personalityMode)
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(RestoreChatInputFocus));
            }
            if (closingAfterOperation)
            {
                allowClose = true;
                _ = Dispatcher.BeginInvoke(Close);
            }
        }
    }

    private async void Microphone_Click(object sender, RoutedEventArgs e) => await ListenAndAskAsync();

    private async Task ListenAndAskAsync()
    {
        if (operation != null) return;
        await RunOperationAsync(async source =>
        {
            SetCoreState(CoreMode.Listening);
            SetStatus("listening");
            string transcript = await voice.ListenAsync(source.Token);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                SetStatus("noSpeech");
                return;
            }
            MessageInput.Text = transcript;
            messages.Add(new ChatMessageView(T("you"), transcript, true));
            ScrollToLatest();
            SetCoreState(CoreMode.Thinking);
            SetStatus("thinking");
            var reply = await jarvis.AskReplyAsync(transcript, source.Token, preferredLanguage);
            var assistantMessage = CreateAssistantMessage(reply);
            messages.Add(assistantMessage);
            RefreshHistory(session.Current.Id);
            ScrollToLatest();
            await SpeakReplyIfEnabled(reply, source.Token, assistantMessage);
        });
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (operation == null) return;
        SetStatus("stopping");
        operation.Cancel();
    }

    private void NewConversation_Click(object sender, RoutedEventArgs e)
    {
        if (operation != null) return;
        if (memoryMode) ExitMemoryMode();
        else if (rulesMode) ExitRulesMode();
        else if (personalityMode) ExitPersonalityMode();
        chatDraft = string.Empty;
        memoryDraft = string.Empty;
        session = new ConversationSession(store, store.CreateConversation());
        jarvis = CreateJarvis(session);
        messages.Clear();
        RefreshHistory(session.Current.Id);
        SetCoreState(CoreMode.Ready);
        SetStatus("newConversationStatus");
        MessageInput.Focus();
    }

    private void Memories_Click(object sender, RoutedEventArgs e)
    {
        if (operation != null) return;
        if (memoryMode) ExitMemoryMode();
        else EnterMemoryMode();
    }

    private void Rules_Click(object sender, RoutedEventArgs e)
    {
        if (operation != null) return;
        if (rulesMode) ExitRulesMode();
        else EnterRulesMode();
    }

    private void Personality_Click(object sender, RoutedEventArgs e)
    {
        if (operation != null) return;
        if (personalityMode) ExitPersonalityMode();
        else EnterPersonalityMode();
    }

    private JarvisAI CreateJarvis(ConversationSession currentSession) =>
        new(httpClient, currentSession, memoryManagementMode: "desktop",
            webSearch: SearchWebAsync,
            webSearchAvailable: () => WebSearchCredentialStore.HasKey(store.DataPath),
            internetAccessEnabled: () => internetAccessEnabled);

    private async Task<IReadOnlyList<WebSearchResult>> SearchWebAsync(string query, CancellationToken cancellationToken)
    {
        if (!internetAccessEnabled) throw new OperationCanceledException("Internet access is disabled.", cancellationToken);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, internetRequests.Token);
        string apiKey = WebSearchCredentialStore.Load(store.DataPath);
        return await webSearchClient.SearchAsync(query, apiKey, linked.Token);
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingHistory || operation != null || HistoryList.SelectedItem is not ConversationEntry selected) return;
        if (memoryMode) ExitMemoryMode();
        else if (rulesMode) ExitRulesMode();
        else if (personalityMode) ExitPersonalityMode();
        try
        {
            session = new ConversationSession(store, store.Load(selected.Id));
            jarvis = CreateJarvis(session);
            LoadCurrentConversation();
            RefreshHistory(selected.Id);
            SetStatus("conversationLoaded");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("historyTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StatusText is null || LanguageSelector.SelectedItem is not ComboBoxItem item) return;
        preferredLanguage = item.Tag?.ToString() is { Length: > 0 } language ? language : null;
        SetStatus(preferredLanguage switch
        {
            "es" => "responseSpanish",
            "en" => "responseEnglish",
            _ => "ready"
        });
    }

    private void LoadCurrentConversation()
    {
        messages.Clear();
        foreach (var turn in session.Current.Turns)
        {
            messages.Add(new ChatMessageView(T("you"), turn.Question, true));
            messages.Add(new ChatMessageView("J.A.R.V.I.S.", turn.Answer, false, turn.Sources));
        }
        ScrollToLatest();
    }

    private void RefreshHistory(Guid selectedId)
    {
        loadingHistory = true;
        conversations.Clear();
        foreach (var item in store.ListConversations())
            conversations.Add(new ConversationEntry(item.Id, string.IsNullOrWhiteSpace(item.Title) ? T("newConversationTitle") : item.Title,
                T("renameConversation"), T("deleteConversation")));
        HistoryList.SelectedItem = conversations.FirstOrDefault(item => item.Id == selectedId);
        loadingHistory = false;
    }

    private void ScrollToLatest() => Dispatcher.BeginInvoke(() => MessagesScroll.ScrollToBottom());

    private ChatMessageView CreateAssistantMessage(JarvisReply reply)
    {
        var message = new ChatMessageView("J.A.R.V.I.S.", reply.Text, false, reply.Sources);
        message.RevealProgress(0);
        return message;
    }

    private async Task RevealReplyQuicklyAsync(ChatMessageView message)
    {
        // Without speech, reveal text at a comfortable reading pace. Longer replies
        // intentionally take longer so the user can follow them as they appear.
        TimeSpan duration = TimeSpan.FromMilliseconds(Math.Clamp(message.Text.Length * 55, 700, 30_000));
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < duration)
        {
            message.RevealProgress(timer.Elapsed.TotalMilliseconds / duration.TotalMilliseconds);
            ScrollToLatest();
            await Task.Delay(50);
        }
        message.RevealProgress(1);
        ScrollToLatest();
    }

    private void RestoreChatInputFocus()
    {
        if (!IsActive || memoryMode || rulesMode || personalityMode || !MessageInput.IsVisible || !MessageInput.IsEnabled) return;
        MessageInput.Focus();
        Keyboard.Focus(MessageInput);
        MessageInput.CaretIndex = MessageInput.Text.Length;
    }

    private void SetBusy(bool busy)
    {
        StopButton.IsEnabled = busy;
        MessageInput.IsEnabled = !busy;
        HistoryList.IsEnabled = !busy;
        LanguageSelector.IsEnabled = !busy;
        SpeakCheckBox.IsEnabled = !busy;
        MemoriesButton.IsEnabled = !busy;
        RulesButton.IsEnabled = !busy;
        PersonalityButton.IsEnabled = !busy;
        DeleteMemoryButton.IsEnabled = !busy && SavedMemoryList.SelectedItem is SavedMemory;
        SendButton.IsEnabled = !busy && !rulesMode;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!allowClose && operation != null)
        {
            e.Cancel = true;
            closingAfterOperation = true;
            operation.Cancel();
            SetStatus("closing");
            return;
        }
        if (disposed) return;
        cursorTrackingTimer.Stop();
        cursorTrackingTimer.Tick -= UpdateCursorTracking;
        windowMessageSource?.RemoveHook(WindowMessageHook);
        windowMessageSource = null;
        StopEnvironmentHud();
        disposed = true;
        voice.Dispose();
        webSearchClient.Dispose();
        httpClient.Dispose();
        store.Dispose();
    }

    private void NewsArticle_Click(object sender, RoutedEventArgs e)
    {
        if (!internetAccessEnabled || sender is not Button { Tag: string url } || !Uri.TryCreate(url, UriKind.Absolute, out Uri? article) ||
            article.Scheme != Uri.UriSchemeHttps ||
            !(article.Host.Equals("yle.fi", StringComparison.OrdinalIgnoreCase) || article.Host.EndsWith(".yle.fi", StringComparison.OrdinalIgnoreCase)))
            return;
        try { Process.Start(new ProcessStartInfo(article.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception) { SetStatus("newsOpenFailed"); }
    }

    private void SourceLink_Click(object sender, RoutedEventArgs e)
    {
        if (!internetAccessEnabled || sender is not Button { Tag: string url } ||
            !Uri.TryCreate(url, UriKind.Absolute, out Uri? source) || source.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(source.UserInfo)) return;
        try { Process.Start(new ProcessStartInfo(source.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception) { SetStatus("newsOpenFailed"); }
    }

    private void RulesList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0) return;
        var viewer = FindVisualChild<ScrollViewer>(RulesList);
        if (viewer is null || viewer.ScrollableWidth <= 0) return;
        viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmMouseHWheel || !RulesList.IsVisible || !GetCursorPos(out NativePoint cursor)) return IntPtr.Zero;
        Point local = RulesList.PointFromScreen(new Point(cursor.X, cursor.Y));
        if (local.X < 0 || local.Y < 0 || local.X >= RulesList.ActualWidth || local.Y >= RulesList.ActualHeight)
            return IntPtr.Zero;
        int delta = unchecked((short)(wParam.ToInt64() >> 16));
        var viewer = FindVisualChild<ScrollViewer>(RulesList);
        if (delta == 0 || viewer is null || viewer.ScrollableWidth <= 0) return IntPtr.Zero;
        viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - delta * 1.5);
        handled = true;
        return new IntPtr(1);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            T? nested = FindVisualChild<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    private enum CoreMode { Ready, Listening, Thinking, Speaking, Error }
    private sealed record ConversationEntry(Guid Id, string Title, string RenameLabel, string DeleteLabel)
    {
        public override string ToString() => Title;
    }
}

public sealed class ChatMessageView : INotifyPropertyChanged
{
    private string displayText;

    public ChatMessageView(string speaker, string text, bool isUser, IReadOnlyList<WebSearchSource>? sources = null)
    {
        Speaker = speaker;
        Text = text;
        IsUser = isUser;
        SourceLinks = (sources ?? Array.Empty<WebSearchSource>())
            .Where(source => Uri.TryCreate(source.Url, UriKind.Absolute, out Uri? uri) &&
                uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo))
            .Select((source, index) => new ChatSourceLink($"[{index + 1}] {source.Title}", source.Url))
            .ToArray();
        displayText = text;
    }

    public string Speaker { get; private set; }
    public string Text { get; }
    public bool IsUser { get; }
    public IReadOnlyList<ChatSourceLink> SourceLinks { get; }
    public string DisplayText
    {
        get => displayText;
        private set
        {
            if (displayText == value) return;
            displayText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetSpeaker(string speaker)
    {
        if (Speaker == speaker) return;
        Speaker = speaker;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Speaker)));
    }

    public void RevealProgress(double progress)
    {
        int count = (int)Math.Ceiling(Text.Length * Math.Clamp(progress, 0, 1));
        DisplayText = Text[..count];
    }
}

public sealed record ChatSourceLink(string Label, string Url);
