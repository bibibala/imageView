using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ImageViewer.Presentation.ViewModels;
using System.Diagnostics;

namespace ImageViewer.Presentation.Views;

public partial class SettingsView : Window
{
    private Button? _generalTabBtn;
    private Button? _aboutTabBtn;
    private StackPanel? _generalPanel;
    private StackPanel? _aboutPanel;

    private const string AuthorUrl = "https://github.com/bibibala";
    private const string RepoUrl = "https://github.com/bibibala/imageView";

    public SettingsView()
    {
        InitializeComponent();
    }

    public SettingsView(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _generalTabBtn = this.FindControl<Button>("GeneralTabBtn");
        _aboutTabBtn = this.FindControl<Button>("AboutTabBtn");
        _generalPanel = this.FindControl<StackPanel>("GeneralPanel");
        _aboutPanel = this.FindControl<StackPanel>("AboutPanel");
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _generalTabBtn is null || _aboutTabBtn is null || _generalPanel is null || _aboutPanel is null)
        {
            return;
        }

        _generalTabBtn.Classes.Remove("active");
        _aboutTabBtn.Classes.Remove("active");
        btn.Classes.Add("active");

        if (btn == _generalTabBtn)
        {
            _generalPanel.IsVisible = true;
            _aboutPanel.IsVisible = false;
        }
        else
        {
            _generalPanel.IsVisible = false;
            _aboutPanel.IsVisible = true;
        }
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
