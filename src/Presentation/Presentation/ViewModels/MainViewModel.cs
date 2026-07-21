using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
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

    public void UpdateScale(double newScale)
    {
        Scale = Math.Clamp(newScale, MinScale, MaxScale);
        StatusMessage = _localizationService.GetString("StatusZoom", Scale * 100);
    }
}
