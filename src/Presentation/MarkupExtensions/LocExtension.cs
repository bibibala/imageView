using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ImageViewer.AppServices.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ImageViewer.Presentation.MarkupExtensions;

public class LocExtension : MarkupExtension
{
    public string Key { get; set; }

    public LocExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var service = App.Services.GetRequiredService<ILocalizationService>();
        return new Binding($"[{Key}]")
        {
            Source = service,
            Mode = BindingMode.OneWay
        };
    }
}
