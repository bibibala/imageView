using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using ImageViewer.AppServices.Interfaces;
using ImageViewer.AppServices.Services;
using ImageViewer.Presentation.ViewModels;
using ImageViewer.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageViewer;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private static MainViewModel? _vm;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Name = "ImageViewer";
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();
            var theme = settingsService.Theme;
            RequestedThemeVariant = theme switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };

            var viewModel = Services.GetRequiredService<MainViewModel>();
            _vm = viewModel;
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            // 必须在这里、任何 await 之前订阅，否则冷启动那次 Activated 事件会被错过
            if (this.TryGetFeature<IActivatableLifetime>() is { } activatableLifetime)
            {
                activatableLifetime.Activated += (_, e) =>
                {
                    if (e is FileActivatedEventArgs fileArgs)
                    {
                        var path = fileArgs.Files.FirstOrDefault()?.Path.LocalPath;
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            OpenPath(path);
                        }
                    }
                };
            }

            // Windows / Linux: 双击关联文件时，路径是通过命令行参数传进来的
            desktop.Startup += (_, e) => OpenFilesFromArgs(e.Args);
            OpenFilesFromArgs(desktop.Args);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OpenFilesFromArgs(string?[]? args)
    {
        if (args is null || _vm is null) return;

        foreach (var a in args)
        {
            if (string.IsNullOrWhiteSpace(a) || a.StartsWith('-') || a.StartsWith("~$"))
                continue;

            if (OpenPath(a)) return;
        }
    }

    private static bool OpenPath(string path)
    {
        if (_vm is null) return false;

        if (File.Exists(path))
        {
            _vm.OpenFileCommand.Execute(path);
            return true;
        }

        if (Directory.Exists(path))
        {
            _vm.OpenFolderCommand.Execute(path);
            return true;
        }

        return false;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IImageService, ImageService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
