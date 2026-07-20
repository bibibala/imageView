using Avalonia.Media.Imaging;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.AppServices.Interfaces;
using ImageViewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ImageViewer.Presentation.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const double MinScale = 0.1;
    private const double MaxScale = 10.0;
    private const double ZoomStep = 0.15;

    private readonly IImageService _imageService;
    private readonly ILogger<MainViewModel> _logger;

    private IReadOnlyList<ImageItem> _images = Array.Empty<ImageItem>();
    private int _currentIndex = -1;

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
    private string _statusMessage = "请打开图片或文件夹";

    [ObservableProperty]
    private bool _hasImage;

    private string _themeLabel = "🌓";

    public string ThemeLabel
    {
        get => _themeLabel;
        private set => SetProperty(ref _themeLabel, value);
    }

    public MainViewModel(IImageService imageService, ILogger<MainViewModel> logger)
    {
        _imageService = imageService;
        _logger = logger;
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
            StatusMessage = "文件夹不存在";
            return;
        }

        _images = _imageService.LoadDirectory(folderPath);
        if (_images.Count == 0)
        {
            StatusMessage = "该文件夹下未找到支持的图片";
            return;
        }

        _currentIndex = 0;
        UpdateNavigationState();
        await LoadCurrentAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task PreviousAsync()
    {
        if (_currentIndex <= 0)
        {
            return;
        }
        _currentIndex--;
        UpdateNavigationState();
        await LoadCurrentAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        if (_currentIndex >= _images.Count - 1)
        {
            return;
        }
        _currentIndex++;
        UpdateNavigationState();
        await LoadCurrentAsync();
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
        StatusMessage = "已重置";
    }

    [RelayCommand]
    private void RotateClockwise() => Rotation = (Rotation + 90) % 360;

    [RelayCommand]
    private void RotateCounterClockwise() => Rotation = (Rotation - 90 + 360) % 360;

    [RelayCommand]
    private void ToggleFullScreen() => IsFullScreen = !IsFullScreen;

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
            StatusMessage = "无法加载图片";
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
            StatusMessage = $"{_currentIndex + 1} / {_images.Count}  -  {item.FileName}  -  {item.FileSizeDisplay}";
            PreviousCommand.NotifyCanExecuteChanged();
            NextCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载图片失败: {FilePath}", item.FilePath);
            StatusMessage = $"加载失败: {item.FileName}";
        }
    }

    private void UpdateScale(double newScale)
    {
        Scale = Math.Clamp(newScale, MinScale, MaxScale);
        StatusMessage = $"缩放: {Scale * 100:F0}%";
    }
}
