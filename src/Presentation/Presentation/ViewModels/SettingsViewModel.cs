using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.AppServices.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ImageViewer.Presentation.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private string _themeLabel = "🌓";

    public ObservableCollection<LanguageOption> LanguageOptions { get; } = new()
    {
        new LanguageOption("auto", "跟随系统 / System"),
        new LanguageOption("zh", "简体中文"),
        new LanguageOption("en", "English")
    };

    public ObservableCollection<string> ThemeOptions { get; } = new()
    {
        "系统",
        "浅色",
        "深色"
    };

    [ObservableProperty]
    private string _selectedTheme = "系统";

    public SettingsViewModel(ILocalizationService localizationService, ISettingsService settingsService, ILogger<SettingsViewModel> logger)
    {
        _localizationService = localizationService;
        _settingsService = settingsService;
        _logger = logger;

        SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Code == _localizationService.CurrentLanguageCode);
        
        var theme = _settingsService.Theme;
        SelectedTheme = theme switch
        {
            "Light" => "浅色",
            "Dark" => "深色",
            _ => "系统"
        };
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is not null && value.Code != _localizationService.CurrentLanguageCode)
        {
            _localizationService.SetLanguage(value.Code);
            RestartApplication();
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        var theme = value switch
        {
            "浅色" => "Light",
            "深色" => "Dark",
            _ => "System"
        };
        _settingsService.SetTheme(theme);
        RestartApplication();
    }

    private static void RestartApplication()
    {
        var process = Process.GetCurrentProcess();
        var startInfo = new ProcessStartInfo
        {
            FileName = process.MainModule?.FileName ?? process.StartInfo.FileName,
            UseShellExecute = true
        };
        Process.Start(startInfo);
        Environment.Exit(0);
    }
}
