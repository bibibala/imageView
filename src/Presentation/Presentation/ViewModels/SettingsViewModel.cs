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

    public ObservableCollection<string> ThemeOptions { get; } = new();

    [ObservableProperty]
    private string _selectedTheme = string.Empty;

    private bool _isInitializing;

    public SettingsViewModel(ILocalizationService localizationService, ISettingsService settingsService, ILogger<SettingsViewModel> logger)
    {
        _localizationService = localizationService;
        _settingsService = settingsService;
        _logger = logger;

        _isInitializing = true;

        ThemeOptions.Add(localizationService.GetString("ThemeSystem"));
        ThemeOptions.Add(localizationService.GetString("ThemeLight"));
        ThemeOptions.Add(localizationService.GetString("ThemeDark"));

        SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Code == _localizationService.CurrentLanguageCode);
        
        var theme = _settingsService.Theme;
        var themeIndex = theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };
        SelectedTheme = ThemeOptions[themeIndex];

        _isInitializing = false;
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_isInitializing) return;
        if (value is not null && value.Code != _localizationService.CurrentLanguageCode)
        {
            _localizationService.SetLanguage(value.Code);
            RestartApplication();
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        if (_isInitializing) return;
        var index = ThemeOptions.IndexOf(value);
        var theme = index switch
        {
            1 => "Light",
            2 => "Dark",
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
