using ImageViewer.AppServices.Interfaces;
using ImageViewer.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ImageViewer.AppServices.Services;

public sealed class ImageService : IImageService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif"
    };

    private readonly ILogger<ImageService> _logger;

    public ImageService(ILogger<ImageService> logger)
    {
        _logger = logger;
    }

    public async Task<ImageItem?> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("图片文件不存在: {FilePath}", filePath);
            return null;
        }

        var fileInfo = new FileInfo(filePath);
        return await Task.FromResult(new ImageItem
        {
            FilePath = fileInfo.FullName,
            FileName = fileInfo.Name,
            FileSize = fileInfo.Length,
            ModifiedTime = fileInfo.LastWriteTime,
        });
    }

    public IReadOnlyList<ImageItem> LoadDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<ImageItem>();
        }

        var directory = new DirectoryInfo(directoryPath);
        return directory
            .EnumerateFiles()
            .Where(file => SupportedExtensions.Contains(file.Extension))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new ImageItem
            {
                FilePath = file.FullName,
                FileName = file.Name,
                FileSize = file.Length,
                ModifiedTime = file.LastWriteTime,
            })
            .ToList();
    }

    public async Task<byte[]> ReadBytesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await File.ReadAllBytesAsync(filePath, cancellationToken);
    }
}
