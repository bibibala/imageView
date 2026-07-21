using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ImageViewer.Presentation.Views.Dialogs;

public partial class ConfirmDialog : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string message) : this()
    {
        MessageText.Text = message;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
