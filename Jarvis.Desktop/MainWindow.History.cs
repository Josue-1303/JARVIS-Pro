using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Jarvis.Core;

namespace Jarvis.Desktop;

public partial class MainWindow
{
    private void RenameConversation_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (operation != null || (sender as Button)?.Tag is not ConversationEntry entry) return;
        try
        {
            Conversation conversation = store.Load(entry.Id);
            var dialog = new ConversationRenameDialog(conversation.Title, T("renameConversation"), T("renameConversationPrompt"), T("renameSave"), T("renameCancel"))
            {
                Owner = this
            };
            if (dialog.ShowDialog() != true) return;
            store.RenameConversation(entry.Id, dialog.TitleText);
            RefreshHistory(session.Current.Id);
            if (session.Current.Id == entry.Id)
            {
                session = new ConversationSession(store, store.Load(entry.Id));
                jarvis = CreateJarvis(session);
            }
            SetStatus("conversationRenamed");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, T("historyTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DeleteConversation_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (operation != null || (sender as Button)?.Tag is not ConversationEntry entry) return;
        if (MessageBox.Show(this, T("confirmDeleteConversation"), T("deleteConversation"), MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            bool deletingCurrent = session.Current.Id == entry.Id;
            store.DeleteConversation(entry.Id);
            if (deletingCurrent)
            {
                Conversation next = store.ListConversations().FirstOrDefault() ?? store.CreateConversation();
                session = new ConversationSession(store, next);
                jarvis = CreateJarvis(session);
                LoadCurrentConversation();
                RefreshHistory(next.Id);
                SetStatus("newConversationStatus");
            }
            else
            {
                RefreshHistory(session.Current.Id);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, T("historyTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

internal sealed class ConversationRenameDialog : Window
{
    private readonly TextBox titleInput;
    public string TitleText => titleInput.Text.Trim();

    public ConversationRenameDialog(string currentTitle, string title, string prompt, string saveLabel, string cancelLabel)
    {
        Title = title;
        Width = 430;
        Height = 190;
        MinWidth = 380;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(16, 19, 30));
        Foreground = new SolidColorBrush(Color.FromRgb(225, 229, 242));
        FontFamily = new FontFamily("Segoe UI");

        var promptText = new TextBlock
        {
            Text = prompt,
            Foreground = new SolidColorBrush(Color.FromRgb(183, 158, 255)),
            Margin = new Thickness(0, 0, 0, 9),
            TextWrapping = TextWrapping.Wrap
        };
        titleInput = new TextBox
        {
            Text = currentTitle,
            MaxLength = 100,
            Padding = new Thickness(10, 8, 10, 8),
            Background = new SolidColorBrush(Color.FromRgb(17, 21, 34)),
            Foreground = new SolidColorBrush(Color.FromRgb(241, 243, 251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(96, 76, 157)),
            CaretBrush = new SolidColorBrush(Color.FromRgb(120, 174, 255))
        };
        var save = new Button
        {
            Content = saveLabel,
            IsDefault = true,
            Width = 84,
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(37, 32, 61)),
            Foreground = Foreground,
            BorderBrush = new SolidColorBrush(Color.FromRgb(98, 71, 161))
        };
        save.Click += (_, _) =>
        {
            if (TitleText.Length == 0)
            {
                titleInput.Focus();
                return;
            }
            DialogResult = true;
        };
        var cancel = new Button
        {
            Content = cancelLabel,
            IsCancel = true,
            Width = 84,
            Padding = new Thickness(10, 7, 10, 7),
            Background = new SolidColorBrush(Color.FromRgb(32, 36, 52)),
            Foreground = Foreground,
            BorderBrush = new SolidColorBrush(Color.FromRgb(65, 72, 102))
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        actions.Children.Add(save);
        actions.Children.Add(cancel);
        var content = new StackPanel { Margin = new Thickness(18) };
        content.Children.Add(promptText);
        content.Children.Add(titleInput);
        content.Children.Add(actions);
        Content = content;
        Loaded += (_, _) => { titleInput.Focus(); titleInput.SelectAll(); };
    }
}
