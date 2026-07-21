using System.ComponentModel;
using System.Globalization;

namespace ImageViewer.AppServices.Interfaces;

public interface ILocalizationService : INotifyPropertyChanged
{
    string this[string key] { get; }

    CultureInfo CurrentCulture { get; }

    string CurrentLanguageCode { get; }

    void SetLanguage(string languageCode);

    string GetString(string key, params object?[] args);
}
