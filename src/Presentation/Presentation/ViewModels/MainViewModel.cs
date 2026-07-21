using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.AppServices.Interfaces;
using ImageViewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ImageViewer.Presentation.ViewModels;

public sealed record LanguageOption(string Code, string DisplayName);

public partial class MainViewModel : ViewModelBase
{
    private const double MinScale = 0.1;
    private const double MaxScale = 10.0;
    private const double ZoomStep = 0.15;

    private readonly IImageService _imageService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<MainViewModel> _logger;

    private IReadOnlyList<ImageItem> _images = Array.Empty<ImageItem>();
    private int _currentIndex = -1;
    private bool _isLoading;

    /// <summary>用于取色的像素缓存（WriteableBitmap），与 CurrentImage 保持同步</summary>
    private WriteableBitmap? _colorPickerBitmap;

    [ObservableProperty]
    private Bitmap? _currentImage;

    [ObservableProperty]
    private ImageItem? _currentItem;

    [ObservableProperty]
    private double _scale = 1.0;

    [ObservableProperty]
    private double _baseScale = 1.0;

    [ObservableProperty]
    private double _rotation;

    [ObservableProperty]
    private bool _isFullScreen;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasImage;

    /// <summary>取色器是否激活</summary>
    [ObservableProperty]
    private bool _isColorPickerActive;

    /// <summary>取色器当前颜色（HEX 格式）</summary>
    [ObservableProperty]
    private string _colorPickerHex = "#FFFFFF";

    /// <summary>取色器当前颜色（RGB 格式）</summary>
    [ObservableProperty]
    private string _colorPickerRgb = "rgb(255, 255, 255)";

    /// <summary>取色器颜色预览画刷</summary>
    [ObservableProperty]
    private IBrush? _colorPickerBrush;

    /// <summary>放大镜图像源（110×110，10倍放大）</summary>
    [ObservableProperty]
    private WriteableBitmap? _magnifierImage;

    /// <summary>放大镜是否可见</summary>
    [ObservableProperty]
    private bool _magnifierVisible;

    /// <summary>是否刚完成复制（用于显示反馈）</summary>
    [ObservableProperty]
    private bool _isCopied;

    // 放大镜常量
    private const int MagnifierSourceSize = 31;
    private const int MagnifierZoom = 5;
    private const int MagnifierDisplaySize = MagnifierSourceSize * MagnifierZoom; // 155

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    public ObservableCollection<LanguageOption> LanguageOptions { get; } = new()
    {
        new LanguageOption("auto", "跟随系统 / System"),
        new LanguageOption("zh", "简体中文"),
        new LanguageOption("en", "English")
    };

    private string _themeLabel = "🌓";

    public string ThemeLabel
    {
        get => _themeLabel;
        private set => SetProperty(ref _themeLabel, value);
    }

    public MainViewModel(IImageService imageService, ILocalizationService localizationService, ILogger<MainViewModel> logger)
    {
        _imageService = imageService;
        _localizationService = localizationService;
        _logger = logger;
        _localizationService.PropertyChanged += (_, _) => RefreshLocalizedStrings();
        SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Code == _localizationService.CurrentLanguageCode);
        StatusMessage = _localizationService.GetString("StatusOpenImageOrFolder");
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is not null && value.Code != _localizationService.CurrentLanguageCode)
        {
            _localizationService.SetLanguage(value.Code);
            RestartApplication();
        }
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

    private void RefreshLocalizedStrings()
    {
        if (!HasImage)
        {
            StatusMessage = _localizationService.GetString("StatusOpenImageOrFolder");
            return;
        }

        if (CurrentItem is null)
        {
            return;
        }

        StatusMessage = _localizationService.GetString("StatusImageInfo", _currentIndex + 1, _images.Count, CurrentItem.FileName, CurrentItem.FileSizeDisplay);
    }

    public void HandleCommandLineArgs(string?[]? args)
    {
        if (args is null || args.Length == 0)
        {
            return;
        }

        var path = args[0];
        if (File.Exists(path))
        {
            OpenFileCommand.Execute(path);
        }
        else if (Directory.Exists(path))
        {
            OpenFolderCommand.Execute(path);
        }
    }

    public bool CanGoPrevious => _images.Count > 0 && _currentIndex > 0;
    public bool CanGoNext => _images.Count > 0 && _currentIndex < _images.Count - 1;

    private void UpdateNavigationState()
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task OpenFileAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _images = _imageService.LoadDirectory(directory);
            _currentIndex = -1;
            for (var i = 0; i < _images.Count; i++)
            {
                if (string.Equals(_images[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    _currentIndex = i;
                    break;
                }
            }
        }

        if (_currentIndex < 0)
        {
            await LoadSingleAsync(filePath);
            return;
        }

        UpdateNavigationState();
        await LoadCurrentAsync();
    }

    [RelayCommand]
    private async Task OpenFolderAsync(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            StatusMessage = _localizationService.GetString("StatusFolderNotFound");
            return;
        }

        _images = _imageService.LoadDirectory(folderPath);
        if (_images.Count == 0)
        {
            StatusMessage = _localizationService.GetString("StatusNoSupportedImages");
            return;
        }

        _currentIndex = 0;
        UpdateNavigationState();
        await LoadCurrentAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task PreviousAsync()
    {
        if (_isLoading || _currentIndex <= 0)
        {
            return;
        }
        _isLoading = true;
        _currentIndex--;
        UpdateNavigationState();
        await LoadCurrentAsync();
        _isLoading = false;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        if (_isLoading || _currentIndex >= _images.Count - 1)
        {
            return;
        }
        _isLoading = true;
        _currentIndex++;
        UpdateNavigationState();
        await LoadCurrentAsync();
        _isLoading = false;
    }

    [RelayCommand]
    private void ZoomIn() => UpdateScale(Scale + ZoomStep);

    [RelayCommand]
    private void ZoomOut() => UpdateScale(Scale - ZoomStep);

    [RelayCommand]
    private void ResetTransform()
    {
        Scale = 1.0;
        Rotation = 0;
        StatusMessage = _localizationService.GetString("StatusReset");
    }

    [RelayCommand]
    private void RotateClockwise() => Rotation = (Rotation + 90) % 360;

    [RelayCommand]
    private void RotateCounterClockwise() => Rotation = (Rotation - 90 + 360) % 360;

    [RelayCommand]
    private void ToggleColorPicker()
    {
        if (!HasImage)
        {
            return;
        }

        IsColorPickerActive = !IsColorPickerActive;
        if (!IsColorPickerActive)
        {
            ClearColorPicker();
        }
    }

    /// <summary>根据原始图片像素坐标更新取色器颜色</summary>
    public void UpdateColorPicker(int pixelX, int pixelY)
    {
        if (_colorPickerBitmap is null)
        {
            return;
        }

        var size = _colorPickerBitmap.PixelSize;
        pixelX = Math.Clamp(pixelX, 0, size.Width - 1);
        pixelY = Math.Clamp(pixelY, 0, size.Height - 1);

        using var fb = _colorPickerBitmap.Lock();
        var bgra = new byte[4];
        var offset = pixelY * fb.RowBytes + pixelX * 4;
        Marshal.Copy(fb.Address + offset, bgra, 0, 4);

        var r = bgra[0];
        var g = bgra[1];
        var b = bgra[2];
        var a = bgra[3];

        ColorPickerHex = $"#{r:X2}{g:X2}{b:X2}";
        ColorPickerRgb = $"rgb({r}, {g}, {b})";
        ColorPickerBrush = new SolidColorBrush(new Color(a, r, g, b));
    }

    /// <summary>清除取色器显示</summary>
    public void ClearColorPicker()
    {
        ColorPickerHex = "—";
        ColorPickerRgb = "—";
        ColorPickerBrush = null;
        MagnifierVisible = false;
        MagnifierImage = null;
    }

    /// <summary>显示复制成功反馈（1.5 秒后自动消失）</summary>
    public async void ShowCopiedFeedback()
    {
        IsCopied = true;
        await Task.Delay(1500);
        IsCopied = false;
    }

    /// <summary>更新放大镜图像（以源图像素坐标为中心，31×31 区域放大 5 倍）</summary>
    public void UpdateMagnifier(int centerPixelX, int centerPixelY)
    {
        if (_colorPickerBitmap is null)
        {
            return;
        }

        var srcW = _colorPickerBitmap.PixelSize.Width;
        var srcH = _colorPickerBitmap.PixelSize.Height;
        var half = MagnifierSourceSize / 2; // 5

        var srcLeft = Math.Clamp(centerPixelX - half, 0, srcW - 1);
        var srcTop = Math.Clamp(centerPixelY - half, 0, srcH - 1);
        var srcRight = Math.Clamp(centerPixelX + half, 0, srcW - 1);
        var srcBottom = Math.Clamp(centerPixelY + half, 0, srcH - 1);
        var actualW = srcRight - srcLeft + 1;
        var actualH = srcBottom - srcTop + 1;

        var displaySize = MagnifierDisplaySize;
        var magPixels = new byte[displaySize * displaySize * 4];

        // 深灰背景
        var bgR = (byte)40;
        var bgG = (byte)40;
        var bgB = (byte)40;
        for (var i = 0; i < magPixels.Length; i += 4)
        {
            magPixels[i] = bgR;
            magPixels[i + 1] = bgG;
            magPixels[i + 2] = bgB;
            magPixels[i + 3] = 255;
        }

        // 在 110×110 中居中放置实际源区域
        var dispOffX = (displaySize - actualW * MagnifierZoom) / 2;
        var dispOffY = (displaySize - actualH * MagnifierZoom) / 2;

        using (var srcFb = _colorPickerBitmap.Lock())
        {
            var srcRowBytes = srcFb.RowBytes;
            var srcRowBuf = new byte[actualW * 4];

            for (var sy = 0; sy < actualH; sy++)
            {
                var srcY = srcTop + sy;
                Marshal.Copy(srcFb.Address + srcY * srcRowBytes + srcLeft * 4, srcRowBuf, 0, srcRowBuf.Length);

                for (var sx = 0; sx < actualW; sx++)
                {
                    var r = srcRowBuf[sx * 4];
                    var g = srcRowBuf[sx * 4 + 1];
                    var b = srcRowBuf[sx * 4 + 2];
                    var a = srcRowBuf[sx * 4 + 3];

                    for (var dy = 0; dy < MagnifierZoom; dy++)
                    {
                        var destY = dispOffY + sy * MagnifierZoom + dy;
                        var rowStart = destY * displaySize * 4;

                        for (var dx = 0; dx < MagnifierZoom; dx++)
                        {
                            var destX = dispOffX + sx * MagnifierZoom + dx;
                            var idx = rowStart + destX * 4;
                            magPixels[idx] = r;
                            magPixels[idx + 1] = g;
                            magPixels[idx + 2] = b;
                            magPixels[idx + 3] = a;
                        }
                    }
                }
            }
        }

        DrawCrosshairOnPixels(magPixels, displaySize);

        var magBmp = new WriteableBitmap(
            new PixelSize(displaySize, displaySize),
            new Vector(96, 96),
            PixelFormat.Rgba8888);
        using (var magFb = magBmp.Lock())
        {
            Marshal.Copy(magPixels, 0, magFb.Address, magPixels.Length);
        }

        MagnifierImage = magBmp;
        MagnifierVisible = true;
    }

    private static void DrawCrosshairOnPixels(byte[] pixels, int displaySize)
    {
        var center = displaySize / 2; // 55

        // 水平红线（第 55 行）
        var hRowStart = center * displaySize * 4;
        for (var x = 0; x < displaySize; x++)
        {
            var idx = hRowStart + x * 4;
            pixels[idx] = 255;
            pixels[idx + 1] = 0;
            pixels[idx + 2] = 0;
            pixels[idx + 3] = 255;
        }

        // 垂直红线（第 55 列）
        for (var y = 0; y < displaySize; y++)
        {
            var idx = y * displaySize * 4 + center * 4;
            pixels[idx] = 255;
            pixels[idx + 1] = 0;
            pixels[idx + 2] = 0;
            pixels[idx + 3] = 255;
        }
    }

    [RelayCommand]
    private void ToggleFullScreen() => IsFullScreen = !IsFullScreen;

    public bool CanDelete => HasImage && _currentIndex >= 0 && _currentIndex < _images.Count;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteCurrentImageAsync()
    {
        if (_currentIndex < 0 || _currentIndex >= _images.Count)
        {
            return;
        }

        var item = _images[_currentIndex];
        var deletedPath = item.FilePath;

        _isLoading = true;

        try
        {
            var list = new List<ImageItem>(_images);
            list.RemoveAt(_currentIndex);

            if (list.Count == 0)
            {
                _images = Array.Empty<ImageItem>();
                _currentIndex = -1;
                CurrentImage = null;
                CurrentItem = null;
                HasImage = false;
                StatusMessage = _localizationService.GetString("StatusOpenImageOrFolder");
            }
            else
            {
                _images = list;
                if (_currentIndex >= _images.Count)
                {
                    _currentIndex = _images.Count - 1;
                }
                await LoadCurrentAsync();
            }

            UpdateNavigationState();

            try
            {
                File.Delete(deletedPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除文件失败: {FilePath}", deletedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除图片失败");
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var app = Avalonia.Application.Current;
        if (app is null)
        {
            return;
        }

        var next = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        app.RequestedThemeVariant = next;
        ThemeLabel = next == ThemeVariant.Dark ? "🌙" : "☀️";
    }

    private async Task LoadSingleAsync(string filePath)
    {
        var item = await _imageService.LoadAsync(filePath);
        if (item is null)
        {
            StatusMessage = _localizationService.GetString("StatusCannotLoadImage");
            return;
        }

        _images = new[] { item };
        _currentIndex = 0;
        await LoadCurrentAsync();
    }

    private async Task LoadCurrentAsync()
    {
        if (_currentIndex < 0 || _currentIndex >= _images.Count)
        {
            return;
        }

        var item = _images[_currentIndex];
        try
        {
            var bytes = await _imageService.ReadBytesAsync(item.FilePath);
            CurrentImage = new Bitmap(new MemoryStream(bytes));
            CurrentItem = item;
            HasImage = true;
            Scale = 1.0;
            Rotation = 0;

            // 关闭取色器（切换图片时）
            if (IsColorPickerActive)
            {
                IsColorPickerActive = false;
                ClearColorPicker();
            }

            // 构建取色器像素缓存
            BuildColorPickerCache();

            StatusMessage = _localizationService.GetString("StatusImageInfo", _currentIndex + 1, _images.Count, item.FileName, item.FileSizeDisplay);
            PreviousCommand.NotifyCanExecuteChanged();
            NextCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载图片失败: {FilePath}", item.FilePath);
            StatusMessage = _localizationService.GetString("StatusLoadFailed", item.FileName);
        }
    }

    private void BuildColorPickerCache()
    {
        _colorPickerBitmap?.Dispose();
        _colorPickerBitmap = null;

        if (CurrentImage is null)
        {
            return;
        }

        var size = CurrentImage.PixelSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        try
        {
            var wb = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
            using (var fb = wb.Lock())
            {
                CurrentImage.CopyPixels(
                    new PixelRect(0, 0, size.Width, size.Height),
                    fb.Address,
                    fb.RowBytes * size.Height,
                    fb.RowBytes);
            }
            _colorPickerBitmap = wb;
        }
        catch
        {
            // 取色器缓存构建失败不影响正常浏览
            _colorPickerBitmap = null;
        }
    }

    public void UpdateScale(double newScale)
    {
        Scale = Math.Clamp(newScale, MinScale, MaxScale);
        StatusMessage = _localizationService.GetString("StatusZoom", Scale * 100);
    }
}
