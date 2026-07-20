using ImageViewer.Domain.Entities;

namespace ImageViewer.AppServices.Interfaces;

public interface IImageService
{
    /// <summary>
    /// 加载单张图片
    /// </summary>
    Task<ImageItem?> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载目录下所有图片
    /// </summary>
    IReadOnlyList<ImageItem> LoadDirectory(string directoryPath);

    /// <summary>
    /// 读取图片用于显示
    /// </summary>
    Task<byte[]> ReadBytesAsync(string filePath, CancellationToken cancellationToken = default);
}
