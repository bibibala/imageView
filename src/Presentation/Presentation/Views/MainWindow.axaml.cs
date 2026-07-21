using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ImageViewer.AppServices.Interfaces;
using ImageViewer.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;

namespace ImageViewer.Presentation.Views;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    private readonly ILocalizationService _localizationService;

    private double _wheelAccumulator;

    private const double WheelThreshold = 0.5;

    public MainWindow()
    {
        _localizationService = App.Services.GetRequiredService<ILocalizationService>();
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(KeyDownEvent, OnKeyDown);
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged);
        ImageScrollViewer.LayoutUpdated += OnScrollViewerLayoutUpdated;

        if (OperatingSystem.IsMacOS())
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        }
        else if (OperatingSystem.IsWindows())
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent };
        }

        Opened += (_, _) =>
        {
            if (OperatingSystem.IsWindows())
            {
                DisableWindowsSystemCorners();
            }
        };
    }

    private void DisableWindowsSystemCorners()
    {
        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd != IntPtr.Zero)
        {
            int preference = 1;
            DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private void OnScrollViewerLayoutUpdated(object? sender, EventArgs e)
    {
        var viewport = ImageScrollViewer.Viewport;
        if (viewport.Width > 0 && viewport.Height > 0)
        {
            ImageView.MaxWidth = viewport.Width;
            ImageView.MaxHeight = viewport.Height;
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _localizationService.GetString("PickerTitleOpenImage"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(_localizationService.GetString("FilePickerImage"))
                {
                    Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.webp", "*.tiff", "*.tif" }
                }
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            await Dispatcher.UIThread.InvokeAsync(() => ViewModel.OpenFileCommand.Execute(path));
        }
    }

    private async void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = _localizationService.GetString("PickerTitleOpenFolder"),
            AllowMultiple = false,
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            await Dispatcher.UIThread.InvokeAsync(() => ViewModel.OpenFolderCommand.Execute(path));
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer is null)
        {
            return;
        }

        string? firstPath = null;
        foreach (var item in e.DataTransfer.Items)
        {
            if (item is Avalonia.Platform.Storage.IStorageItem storageItem)
            {
                firstPath = storageItem.Path.LocalPath;
                break;
            }
        }

        if (string.IsNullOrEmpty(firstPath))
        {
            return;
        }

        if (Directory.Exists(firstPath))
        {
            await Dispatcher.UIThread.InvokeAsync(() => ViewModel.OpenFolderCommand.Execute(firstPath));
        }
        else if (File.Exists(firstPath))
        {
            await Dispatcher.UIThread.InvokeAsync(() => ViewModel.OpenFileCommand.Execute(firstPath));
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var isMeta = e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (isMeta)
        {
            switch (e.Key)
            {
                case Key.M:
                    WindowState = WindowState.Minimized;
                    e.Handled = true;
                    return;
                case Key.W:
                    Close();
                    e.Handled = true;
                    return;
                case Key.F:
                    ViewModel.ToggleFullScreenCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

        switch (e.Key)
        {
            case Key.Left:
                if (ViewModel.CanGoPrevious)
                {
                    ViewModel.PreviousCommand.Execute(null);
                }
                e.Handled = true;
                break;
            case Key.Right:
                if (ViewModel.CanGoNext)
                {
                    ViewModel.NextCommand.Execute(null);
                }
                e.Handled = true;
                break;
            case Key.F:
                ViewModel.ToggleFullScreenCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.R:
                ViewModel.ResetTransformCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Add:
            case Key.OemPlus:
                ViewModel.ZoomInCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Subtract:
            case Key.OemMinus:
                ViewModel.ZoomOutCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!ViewModel.HasImage)
        {
            return;
        }

        _wheelAccumulator += e.Delta.Y;

        if (_wheelAccumulator >= WheelThreshold)
        {
            ViewModel.ZoomInCommand.Execute(null);
            _wheelAccumulator = 0;
        }
        else if (_wheelAccumulator <= -WheelThreshold)
        {
            ViewModel.ZoomOutCommand.Execute(null);
            _wheelAccumulator = 0;
        }

        e.Handled = true;
    }
}
