using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Jarvis.Desktop;

internal sealed class WebSearchKeyDialog : Window
{
    private readonly PasswordBox keyInput;
    public string ApiKey => keyInput.Password;
    public bool RemoveRequested { get; private set; }

    public WebSearchKeyDialog(string title, string description, string keyLabel, string saveLabel,
        string removeLabel, string cancelLabel, string openKeyLabel, string configuredLabel,
        string missingLabel, bool hasKey)
    {
        Title = title;
        Width = 510;
        Height = 310;
        MinWidth = 480;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(16, 19, 30));
        Foreground = new SolidColorBrush(Color.FromRgb(225, 229, 242));
        FontFamily = new FontFamily("Segoe UI");

        var content = new StackPanel { Margin = new Thickness(20) };
        content.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = new SolidColorBrush(Color.FromRgb(174, 187, 215)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });
        content.Children.Add(new TextBlock
        {
            Text = hasKey ? "●  " + configuredLabel : "○  " + missingLabel,
            Foreground = new SolidColorBrush(hasKey ? Color.FromRgb(89, 214, 206) : Color.FromRgb(217, 185, 110)),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 11)
        });
        content.Children.Add(new TextBlock
        {
            Text = keyLabel,
            Foreground = new SolidColorBrush(Color.FromRgb(183, 158, 255)),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 5)
        });
        keyInput = new PasswordBox
        {
            MaxLength = 512,
            Padding = new Thickness(10, 8, 10, 8),
            Background = new SolidColorBrush(Color.FromRgb(17, 21, 34)),
            Foreground = new SolidColorBrush(Color.FromRgb(241, 243, 251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(96, 76, 157)),
            CaretBrush = new SolidColorBrush(Color.FromRgb(120, 174, 255))
        };
        content.Children.Add(keyInput);

        var openKey = new Button
        {
            Content = openKeyLabel,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 174, 255)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        openKey.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://ollama.com/settings/keys") { UseShellExecute = true }); }
            catch (Exception) { }
        };
        content.Children.Add(openKey);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var save = MakeButton(saveLabel, isAccent: true);
        save.IsDefault = true;
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(keyInput.Password))
            {
                keyInput.Focus();
                return;
            }
            DialogResult = true;
        };
        var remove = MakeButton(removeLabel, isAccent: false);
        remove.IsEnabled = hasKey;
        remove.Margin = new Thickness(8, 0, 8, 0);
        remove.Click += (_, _) => { RemoveRequested = true; DialogResult = true; };
        var cancel = MakeButton(cancelLabel, isAccent: false);
        cancel.IsCancel = true;
        actions.Children.Add(save);
        actions.Children.Add(remove);
        actions.Children.Add(cancel);
        content.Children.Add(actions);
        Content = content;
        Loaded += (_, _) => keyInput.Focus();
    }

    private static Button MakeButton(string label, bool isAccent) => new()
    {
        Content = label,
        MinWidth = 84,
        Padding = new Thickness(11, 7, 11, 7),
        Background = new SolidColorBrush(isAccent ? Color.FromRgb(37, 32, 61) : Color.FromRgb(32, 36, 52)),
        Foreground = new SolidColorBrush(Color.FromRgb(225, 229, 242)),
        BorderBrush = new SolidColorBrush(isAccent ? Color.FromRgb(98, 71, 161) : Color.FromRgb(65, 72, 102)),
        BorderThickness = new Thickness(1),
        Cursor = System.Windows.Input.Cursors.Hand
    };
}
