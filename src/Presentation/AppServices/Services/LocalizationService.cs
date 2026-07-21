using System.ComponentModel;
using System.Globalization;
using System.Resources;
using ImageViewer.AppServices.Interfaces;

namespace ImageViewer.AppServices.Services;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly ResourceManager ResourceManager = new("ImageViewer.Resources.Strings", typeof(LocalizationService).Assembly);

    private readonly ISettingsService _settingsService;

    public LocalizationService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        CurrentLanguageCode = settingsService.Language;
        CurrentCulture = ResolveCulture(CurrentLanguageCode);
        ApplyCulture(CurrentCulture);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo CurrentCulture { get; private set; }

    public string CurrentLanguageCode { get; private set; }

    public string this[string key] => GetString(key);

    public void SetLanguage(string languageCode)
    {
        CurrentLanguageCode = languageCode;
        CurrentCulture = ResolveCulture(languageCode);
        ApplyCulture(CurrentCulture);
        _settingsService.SetLanguage(languageCode);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public string GetString(string key, params object?[] args)
    {
        var value = ResourceManager.GetString(key, CurrentCulture) ?? key;
        return args.Length > 0 ? string.Format(CurrentCulture, value, args) : value;
    }

    private static CultureInfo ResolveCulture(string languageCode)
    {
        return languageCode switch
        {
            "zh" => new CultureInfo("zh-CN"),
            "en" => new CultureInfo("en-US"),
            _ => CultureInfo.CurrentUICulture
        };
    }

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
