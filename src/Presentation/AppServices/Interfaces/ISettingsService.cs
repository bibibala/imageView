namespace ImageViewer.AppServices.Interfaces;

public interface ISettingsService
{
    string Language { get; }

    void SetLanguage(string language);
}
