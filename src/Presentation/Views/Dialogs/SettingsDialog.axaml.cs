using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ImageViewer.Presentation.ViewModels;
using System.Diagnostics;

namespace ImageViewer.Presentation.Views.Dialogs;

public partial class SettingsDialog : Window
{
    private Button? _generalTabBtn;
    private Button? _aboutTabBtn;
    private Button? _shortcutsTabBtn;
    private StackPanel? _generalPanel;
    private StackPanel? _aboutPanel;
    private StackPanel? _shortcutsPanel;

    private const string AuthorUrl = "https://github.com/bibibala";
    private const string RepoUrl = "https://github.com/bibibala/imageView";

    public SettingsDialog()
    {
        InitializeComponent();
    }

    public SettingsDialog(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _generalTabBtn = this.FindControl<Button>("GeneralTabBtn");
        _aboutTabBtn = this.FindControl<Button>("AboutTabBtn");
        _shortcutsTabBtn = this.FindControl<Button>("ShortcutsTabBtn");
        _generalPanel = this.FindControl<StackPanel>("GeneralPanel");
        _aboutPanel = this.FindControl<StackPanel>("AboutPanel");
        _shortcutsPanel = this.FindControl<StackPanel>("ShortcutsPanel");
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn
            || _generalTabBtn is null || _aboutTabBtn is null || _shortcutsTabBtn is null
            || _generalPanel is null || _aboutPanel is null || _shortcutsPanel is null)
        {
            return;
        }

        _generalTabBtn.Classes.Remove("active");
        _aboutTabBtn.Classes.Remove("active");
        _shortcutsTabBtn.Classes.Remove("active");
        btn.Classes.Add("active");

        _generalPanel.IsVisible = btn == _generalTabBtn;
        _aboutPanel.IsVisible = btn == _aboutTabBtn;
        _shortcutsPanel.IsVisible = btn == _shortcutsTabBtn;
    }

    private void OnOpenAuthorUrl(object? sender, RoutedEventArgs e)
    {
        OpenUrl(AuthorUrl);
    }

    private void OnOpenRepoUrl(object? sender, RoutedEventArgs e)
    {
        OpenUrl(RepoUrl);
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
