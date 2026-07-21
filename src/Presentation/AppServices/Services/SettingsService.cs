using System.Text.Json;
using ImageViewer.AppServices.Interfaces;

namespace ImageViewer.AppServices.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private SettingsData _settings;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "ImageViewer");
        Directory.CreateDirectory(folder);
        _settingsFilePath = Path.Combine(folder, "settings.json");
        _settings = Load();
    }

    public string Language => _settings.Language;

    public void SetLanguage(string language)
    {
        _settings.Language = language;
        Save();
    }

    private SettingsData Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new SettingsData();
        }

        var json = File.ReadAllText(_settingsFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SettingsData();
        }

        var data = JsonSerializer.Deserialize<SettingsData>(json);
        return data ?? new SettingsData();
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);
    }

    private sealed class SettingsData
    {
        public string Language { get; set; } = "auto";
    }
}
