using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Jarvis.Core;

namespace Jarvis.Desktop;

internal sealed class MemoryManagerWindow : Window
{
    private readonly MemoryStore store;
    private readonly bool english;
    private readonly ObservableCollection<SavedMemory> memories = new();
    private readonly ListBox memoryList = new();
    private readonly TextBox memoryInput = new();

    public MemoryManagerWindow(MemoryStore store, string interfaceLanguage, Style scrollbarStyle)
    {
        this.store = store;
        english = interfaceLanguage == "en";
        Title = english ? "J.A.R.V.I.S. · Memories" : "J.A.R.V.I.S. · Recuerdos";
        Width = 560;
        Height = 590;
        MinWidth = 480;
        MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#0B0E18");
        Foreground = Brush("#E2E5F0");
        FontFamily = new FontFamily("Segoe UI");

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        var title = new TextBlock
        {
            Text = english ? "MEMORIES" : "RECUERDOS",
            Foreground = Brush("#D9B96E"), FontSize = 18, FontWeight = FontWeights.SemiBold
        };
        root.Children.Add(title);

        var description = new TextBlock
        {
            Text = english
                ? "Saved locally on this device. You decide what J.A.R.V.I.S. can remember. For example: “My full name is …”."
                : "Se guardan en este equipo. Usted decide qué puede recordar J.A.R.V.I.S. Por ejemplo: «Mi nombre completo es …».",
            Foreground = Brush("#A8B0C5"), FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 9, 0, 14)
        };
        Grid.SetRow(description, 1);
        root.Children.Add(description);

        memoryList.ItemsSource = memories;
        memoryList.Background = Brush("#101522");
        memoryList.Foreground = Brush("#DCE2F2");
        memoryList.BorderBrush = Brush("#29334A");
        memoryList.BorderThickness = new Thickness(1);
        memoryList.Padding = new Thickness(8);
        memoryList.Resources.Add(typeof(ScrollBar), scrollbarStyle);
        ScrollViewer.SetHorizontalScrollBarVisibility(memoryList, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(memoryList, ScrollBarVisibility.Auto);
        memoryList.ItemTemplate = new DataTemplate(typeof(SavedMemory));
        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(SavedMemory.Text)));
        textFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        textFactory.SetValue(TextBlock.ForegroundProperty, Brush("#DCE2F2"));
        textFactory.SetValue(TextBlock.MarginProperty, new Thickness(8, 5, 8, 5));
        memoryList.ItemTemplate.VisualTree = textFactory;
        Grid.SetRow(memoryList, 2);
        root.Children.Add(memoryList);

        memoryInput.Foreground = Brush("#F1F3FB");
        memoryInput.Background = Brush("#111522");
        memoryInput.BorderBrush = Brush("#30384F");
        memoryInput.BorderThickness = new Thickness(1);
        memoryInput.Padding = new Thickness(14, 12, 14, 12);
        memoryInput.FontSize = 14;
        memoryInput.CaretBrush = Brush("#78AEFF");
        memoryInput.VerticalContentAlignment = VerticalAlignment.Center;
        memoryInput.MaxLength = MemoryStore.MaxMemoryLength;
        memoryInput.Margin = new Thickness(0, 14, 0, 9);
        memoryInput.ToolTip = english ? "Maximum 300 characters" : "Máximo 300 caracteres";
        memoryInput.AcceptsReturn = false;
        Grid.SetRow(memoryInput, 3);
        root.Children.Add(memoryInput);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var add = Button(english ? "＋  Save memory" : "＋  Guardar recuerdo", "#25203D", "#6247A1");
        add.Click += (_, _) => AddMemory();
        var remove = Button(english ? "Delete selected" : "Borrar seleccionado", "#261923", "#693346");
        remove.Margin = new Thickness(8, 0, 0, 0);
        remove.Click += (_, _) => DeleteSelected();
        actions.Children.Add(add);
        actions.Children.Add(remove);
        Grid.SetRow(actions, 4);
        root.Children.Add(actions);

        var clear = Button(english ? "Clear all memories" : "Borrar todos los recuerdos", "Transparent", "#394568");
        clear.HorizontalAlignment = HorizontalAlignment.Left;
        clear.Margin = new Thickness(0, 10, 0, 0);
        clear.Padding = new Thickness(10, 6, 10, 6);
        clear.Click += (_, _) => ClearMemories();
        Grid.SetRow(clear, 5);
        root.Children.Add(clear);

        RefreshMemories();
    }

    private Button Button(string text, string background, string border) => new()
    {
        Content = text, Foreground = Brush("#E7E4FF"), Background = Brush(background), BorderBrush = Brush(border),
        BorderThickness = new Thickness(1), Padding = new Thickness(12, 9, 12, 9), Cursor = System.Windows.Input.Cursors.Hand
    };

    private void AddMemory()
    {
        string text = memoryInput.Text.Trim();
        if (text.Length == 0) return;
        if (MessageBox.Show(this,
                english ? $"Save this memory on this device?\n\n{text}" : $"¿Guardar este recuerdo en este equipo?\n\n{text}",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            store.AddMemory(text);
            memoryInput.Clear();
            RefreshMemories();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DeleteSelected()
    {
        if (memoryList.SelectedItem is not SavedMemory selected) return;
        if (MessageBox.Show(this,
                english ? $"Delete this memory?\n\n{selected.Text}" : $"¿Borrar este recuerdo?\n\n{selected.Text}",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            store.DeleteMemory(selected.Id);
            RefreshMemories();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClearMemories()
    {
        if (memories.Count == 0) return;
        string message = english
            ? "Delete every saved memory? This cannot be undone."
            : "¿Borrar todos los recuerdos guardados? Esta acción no se puede deshacer.";
        if (MessageBox.Show(this, message, Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            store.ClearMemories();
            RefreshMemories();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshMemories()
    {
        try
        {
            memories.Clear();
            foreach (SavedMemory memory in store.ListMemories()) memories.Add(memory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static SolidColorBrush Brush(string value) => (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
}
