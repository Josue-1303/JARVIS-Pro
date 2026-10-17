using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Jarvis.Core;

namespace Jarvis.Desktop;

public partial class MainWindow
{
    private readonly ObservableCollection<SavedMemory> savedMemories = new();
    private bool memoryMode;
    private bool rulesMode;
    private bool personalityMode;
    private bool loadingMemories;
    private Guid? selectedMemoryId;
    private Guid? selectedUserRuleId;
    private string chatDraft = string.Empty;
    private string memoryDraft = string.Empty;

    private void EnterMemoryMode()
    {
        if (!memoryMode)
        {
            if (!rulesMode && !personalityMode) { chatDraft = MessageInput.Text; memoryDraft = string.Empty; }
            MessageInput.Text = memoryDraft;
        }
        selectedMemoryId = null;
        SavedMemoryList.ItemsSource = savedMemories;
        StageHud.Visibility = Visibility.Visible;
        ChatNuclei.Visibility = Visibility.Collapsed;
        EnvironmentHud.Visibility = Visibility.Visible;
        MessagesScroll.Visibility = Visibility.Collapsed;
        RulesModePanel.Visibility = Visibility.Collapsed;
        PersonalityModePanel.Visibility = Visibility.Collapsed;
        MemoryModePanel.Visibility = Visibility.Visible;
        MemoryBrain.Visibility = Visibility.Visible;
        RulesBrain.Visibility = Visibility.Collapsed;
        MicrophoneButton.Visibility = Visibility.Collapsed;
        DeleteMemoryButton.Visibility = Visibility.Visible;
        SpeakCheckBox.Visibility = Visibility.Collapsed;
        memoryMode = true;
        rulesMode = false;
        personalityMode = false;
        RefreshMemories();
        UpdateMemoryModeLabels();
        MessageInput.Focus();
    }

    private void ExitMemoryMode()
    {
        if (!memoryMode) return;
        memoryDraft = MessageInput.Text;
        memoryMode = false;
        MemoryModePanel.Visibility = Visibility.Collapsed;
        RestoreChatWorkspace();
    }

    private void EnterRulesMode()
    {
        if (!rulesMode)
        {
            if (memoryMode) memoryDraft = MessageInput.Text;
            else if (!rulesMode && !personalityMode) chatDraft = MessageInput.Text;
        }
        memoryMode = false;
        rulesMode = true;
        personalityMode = false;
        StageHud.Visibility = Visibility.Visible;
        ChatNuclei.Visibility = Visibility.Collapsed;
        EnvironmentHud.Visibility = Visibility.Visible;
        MessagesScroll.Visibility = Visibility.Collapsed;
        MemoryModePanel.Visibility = Visibility.Collapsed;
        RulesModePanel.Visibility = Visibility.Visible;
        PersonalityModePanel.Visibility = Visibility.Collapsed;
        MemoryBrain.Visibility = Visibility.Visible;
        RulesBrain.Visibility = Visibility.Visible;
        RuleInput.Clear();
        selectedUserRuleId = null;
        MicrophoneButton.Visibility = Visibility.Collapsed;
        DeleteMemoryButton.Visibility = Visibility.Collapsed;
        SpeakCheckBox.Visibility = Visibility.Collapsed;
        MessageInput.Clear();
        RefreshRuleCards();
        UpdateMemoryModeLabels();
        RuleInput.Focus();
    }

    private void ExitRulesMode()
    {
        if (!rulesMode) return;
        rulesMode = false;
        personalityMode = false;
        RulesModePanel.Visibility = Visibility.Collapsed;
        RestoreChatWorkspace();
    }

    private void EnterPersonalityMode()
    {
        if (personalityMode) { ExitPersonalityMode(); return; }
        if (!memoryMode && !rulesMode) chatDraft = MessageInput.Text;
        if (memoryMode) memoryDraft = MessageInput.Text;
        try { PersonalityCurrentText.Text = store.LoadPersonalityProfile(interfaceLanguage); }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            PersonalityCurrentText.Text = interfaceLanguage == "en" ? MemoryStore.DefaultPersonalityProfileEnglish : MemoryStore.DefaultPersonalityProfileSpanish;
        }
        memoryMode = false;
        rulesMode = false;
        personalityMode = true;
        ChatNuclei.Visibility = Visibility.Collapsed;
        StageHud.Visibility = Visibility.Visible;
        EnvironmentHud.Visibility = Visibility.Visible;
        MessagesScroll.Visibility = Visibility.Collapsed;
        MemoryModePanel.Visibility = Visibility.Collapsed;
        RulesModePanel.Visibility = Visibility.Collapsed;
        PersonalityModePanel.Visibility = Visibility.Visible;
        RulesBrain.Visibility = Visibility.Collapsed;
        MemoryBrain.Visibility = Visibility.Collapsed;
        MicrophoneButton.Visibility = Visibility.Collapsed;
        DeleteMemoryButton.Visibility = Visibility.Collapsed;
        SpeakCheckBox.Visibility = Visibility.Collapsed;
        MessageInput.Clear();
        PersonalityInput.Clear();
        UpdateMemoryModeLabels();
        PersonalityInput.Focus();
        PersonalityInput.CaretIndex = PersonalityInput.Text.Length;
    }

    private void ExitPersonalityMode()
    {
        if (!personalityMode) return;
        personalityMode = false;
        PersonalityModePanel.Visibility = Visibility.Collapsed;
        RestoreChatWorkspace();
    }

    private void RestoreChatWorkspace()
    {
        memoryMode = false;
        rulesMode = false;
        personalityMode = false;
        StageHud.Visibility = Visibility.Visible;
        ChatNuclei.Visibility = Visibility.Visible;
        EnvironmentHud.Visibility = Visibility.Visible;
        MessagesScroll.Visibility = Visibility.Visible;
        MemoryModePanel.Visibility = Visibility.Collapsed;
        RulesModePanel.Visibility = Visibility.Collapsed;
        PersonalityModePanel.Visibility = Visibility.Collapsed;
        RulesBrain.Visibility = Visibility.Visible;
        MicrophoneButton.Visibility = Visibility.Visible;
        DeleteMemoryButton.Visibility = Visibility.Collapsed;
        SpeakCheckBox.Visibility = Visibility.Visible;
        MessageInput.IsEnabled = true;
        MessageInput.Text = chatDraft;
        chatDraft = string.Empty;
        UpdateMemoryModeLabels();
        MessageInput.Focus();
    }

    private void RefreshRuleCards()
    {
        try
        {
            var cards = AssistantRules.All.Select(rule => new RuleCardView(
                $"{rule.Number:00}  ·  {(interfaceLanguage == "en" ? rule.EnglishTitle : rule.SpanishTitle)}",
                interfaceLanguage == "en" ? rule.EnglishText : rule.SpanishText,
                T("immutableRule"), new SolidColorBrush(Color.FromRgb(217, 185, 110)), null))
                .Concat(store.ListUserRules().Select((rule, index) => new RuleCardView(
                    $"U{index + 1:00}  ·  {rule.Text}",
                    T("userRulePriority"), T("personalRule"), new SolidColorBrush(Color.FromRgb(183, 158, 255)), rule.Id)))
                .ToArray();
            RulesList.ItemsSource = cards;
            RulesList.SelectedItem = selectedUserRuleId is Guid selectedId ? cards.FirstOrDefault(card => card.UserRuleId == selectedId) : null;
            DeleteRuleButton.IsEnabled = RulesList.SelectedItem is RuleCardView { UserRuleId: not null };
            RulesCountText.Text = $"{AssistantRules.All.Count} + {cards.Length - AssistantRules.All.Count}";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            RulesList.ItemsSource = AssistantRules.All.Select(rule => new RuleCardView(
                $"{rule.Number:00}  ·  {(interfaceLanguage == "en" ? rule.EnglishTitle : rule.SpanishTitle)}",
                interfaceLanguage == "en" ? rule.EnglishText : rule.SpanishText,
                T("immutableRule"), new SolidColorBrush(Color.FromRgb(217, 185, 110)), null)).ToArray();
            DeleteRuleButton.IsEnabled = false;
            MessageBox.Show(this, ex.Message, T("rulesTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RulesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedUserRuleId = RulesList.SelectedItem is RuleCardView { UserRuleId: Guid id } ? id : null;
        DeleteRuleButton.IsEnabled = selectedUserRuleId is not null;
    }

    private void RuleInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            AddUserRule_Click(AddRuleButton, new RoutedEventArgs());
        }
    }

    private void AddUserRule_Click(object sender, RoutedEventArgs e)
    {
        if (!rulesMode) return;
        try
        {
            store.AddUserRule(RuleInput.Text);
            RuleInput.Clear();
            selectedUserRuleId = null;
            RefreshRuleCards();
            SetStatus("userRuleSaved");
            RuleInput.Focus();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, T("rulesTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            RuleInput.Focus();
        }
    }

    private void PersonalityInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter ||
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0) return;

        e.Handled = true;
        SavePersonality_Click(SavePersonalityButton, new RoutedEventArgs());
    }

    private void DeleteUserRule_Click(object sender, RoutedEventArgs e)
    {
        if (!rulesMode || selectedUserRuleId is not Guid id || RulesList.SelectedItem is not RuleCardView { UserRuleId: not null }) return;
        if (MessageBox.Show(this, T("confirmDeleteUserRule"), T("rulesTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            store.DeleteUserRule(id);
            selectedUserRuleId = null;
            RefreshRuleCards();
            SetStatus("userRuleDeleted");
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, T("rulesTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SavePersonality_Click(object sender, RoutedEventArgs e)
    {
        if (!personalityMode) return;
        try
        {
            store.SavePersonalityProfile(PersonalityInput.Text);
            PersonalityCurrentText.Text = PersonalityInput.Text.Trim();
            PersonalityInput.Clear();
            SetStatus("personalitySaved");
            PersonalityInput.Focus();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, T("personalityTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ResetPersonality_Click(object sender, RoutedEventArgs e)
    {
        if (!personalityMode || MessageBox.Show(this, T("confirmResetPersonality"), T("personalityTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        string defaultProfile = interfaceLanguage == "en" ? MemoryStore.DefaultPersonalityProfileEnglish : MemoryStore.DefaultPersonalityProfileSpanish;
        try
        {
            store.SavePersonalityProfile(defaultProfile);
            PersonalityInput.Clear();
            PersonalityCurrentText.Text = defaultProfile;
            SetStatus("personalitySaved");
            PersonalityInput.Focus();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, T("personalityTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshMemories(Guid? selectId = null)
    {
        loadingMemories = true;
        try
        {
            savedMemories.Clear();
            foreach (SavedMemory item in store.ListMemories().OrderByDescending(item => item.CreatedAt))
                savedMemories.Add(item);
            SavedMemoryList.SelectedItem = selectId is null ? null : savedMemories.FirstOrDefault(item => item.Id == selectId);
            MemoryEmptyText.Visibility = savedMemories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            MemoryCountText.Text = $"{savedMemories.Count} / {MemoryStore.MaxMemories}";
            DeleteMemoryButton.IsEnabled = SavedMemoryList.SelectedItem is SavedMemory;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, T("dialogTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { loadingMemories = false; }
    }

    private void SavedMemoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!memoryMode || loadingMemories || SavedMemoryList.SelectedItem is not SavedMemory selected) return;
        selectedMemoryId = selected.Id;
        MessageInput.Text = selected.Text;
        MessageInput.CaretIndex = MessageInput.Text.Length;
        DeleteMemoryButton.IsEnabled = true;
        MessageInput.Focus();
    }

    private async Task SaveMemoryAsync()
    {
        if (!memoryMode || operation != null) return;
        try
        {
            string text = MessageInput.Text.Trim();
            SavedMemory saved = selectedMemoryId is Guid id
                ? store.UpdateMemory(id, text)
                : store.AddMemory(text);
            selectedMemoryId = null;
            MessageInput.Clear();
            RefreshMemories();
            SetStatus("memorySaved");
            await MemoryBrain.PlayTransferAsync(deleting: false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, T("memoryTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void DeleteMemory_Click(object sender, RoutedEventArgs e)
    {
        if (!memoryMode || selectedMemoryId is not Guid id || SavedMemoryList.SelectedItem is not SavedMemory selected) return;
        var answer = MessageBox.Show(this, T("confirmDeleteMemory"), T("memoryTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            await MemoryBrain.PlayTransferAsync(deleting: true);
            store.DeleteMemory(id);
            MessageInput.Clear();
            selectedMemoryId = null;
            RefreshMemories();
            SetStatus("memoryDeleted");
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, T("memoryTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateMemoryModeLabels()
    {
        if (MemoryModeTitle is null) return;
        MemoriesButton.Content = T(memoryMode ? "backToChat" : "memories");
        MemoryModeTitle.Text = T("memoryTitleUpper");
        MemoryModeHint.Text = T("memoryModeHint");
        MemoryListTitle.Text = T("memoriesUpper");
        MemoryEmptyText.Text = T("memoryEmpty");
        MemoryEditorHint.Text = T("memoryEditorHint");
        DeleteMemoryButton.Content = T("deleteMemory");
        RulesButton.Content = T(rulesMode ? "backToChat" : "rules");
        PersonalityButton.Content = T(personalityMode ? "backToChat" : "personality");
        RulesModeTitle.Text = T("rulesTitle");
        RulesModeHint.Text = T("rulesHint");
        PersonalityModeTitle.Text = T("personalityTitle");
        PersonalityModeHint.Text = T("personalityHint");
        PersonalityCurrentTitle.Text = T("personalityCurrentTitle");
        RulesListTitle.Text = T("rulesListTitle");
        RulesFooterText.Text = T("rulesFooter");
        RuleInput.ToolTip = T("ruleInputTip");
        AddRuleButton.Content = T("addRule");
        DeleteRuleButton.Content = T("deleteUserRule");
        PersonalityInput.ToolTip = T("personalityInputTip");
        SavePersonalityButton.Content = T("savePersonality");
        ResetPersonalityButton.Content = T("resetPersonality");
        RefreshRuleCards();
        NewsPanel.Visibility = memoryMode || rulesMode || personalityMode ? Visibility.Collapsed : Visibility.Visible;
        SendButton.Content = T(memoryMode ? "saveMemory" : "send");
        ShortcutText.Text = memoryMode ? T("memoryShortcut") : T("shortcuts");
        ComposerPanel.Visibility = Visibility.Visible;
        ChatComposerControls.Visibility = rulesMode || personalityMode ? Visibility.Collapsed : Visibility.Visible;
        RulesComposerControls.Visibility = rulesMode ? Visibility.Visible : Visibility.Collapsed;
        PersonalityComposerControls.Visibility = personalityMode ? Visibility.Visible : Visibility.Collapsed;
        FooterControls.Visibility = rulesMode || personalityMode ? Visibility.Collapsed : Visibility.Visible;
        MessageInput.IsEnabled = !rulesMode && !personalityMode;
        SendButton.IsEnabled = !rulesMode && !personalityMode && operation is null;
        System.Windows.Automation.AutomationProperties.SetName(MemoryBrain, T("memoryBrainDescription"));
        System.Windows.Automation.AutomationProperties.SetName(RulesBrain, T("rulesBrainDescription"));
        System.Windows.Automation.AutomationProperties.SetName(PersonalityCore, T("personalityCoreDescription"));
    }

    private sealed record RuleCardView(string Title, string Description, string SourceLabel, Brush SourceColor, Guid? UserRuleId);
}
