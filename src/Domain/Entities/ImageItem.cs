namespace ImageViewer.Domain.Entities;

/// <summary>
/// 图片实体
/// </summary>
public sealed record ImageItem
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required long FileSize { get; init; }
    public required DateTime ModifiedTime { get; init; }

    public string FileSizeDisplay => FormatFileSize(FileSize);

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }
        return $"{bytes / 1024.0 / 1024.0:F2} MB";
    }
}
