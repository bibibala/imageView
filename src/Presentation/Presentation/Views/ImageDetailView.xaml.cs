using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using ImageViewer.AppServices.Interfaces;
using ImageViewer.Domain.Entities;
using MetadataExtractor;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.IO;

namespace ImageViewer.Presentation.Views;

public partial class ImageDetailView : Window
{
    private static readonly Dictionary<string, string> ExifNameZh = new()
    {
        // IFD0
        ["Make"] = "相机制造商",
        ["Model"] = "相机型号",
        ["Software"] = "软件",
        ["Artist"] = "作者",
        ["Copyright"] = "版权",
        ["Image Description"] = "图片描述",
        ["Orientation"] = "方向",
        ["X Resolution"] = "水平分辨率",
        ["Y Resolution"] = "垂直分辨率",
        ["Resolution Unit"] = "分辨率单位",
        ["Date/Time"] = "日期时间",
        ["YCbCr Positioning"] = "YCbCr 定位",
        // SubIFD
        ["Exposure Time"] = "曝光时间",
        ["F-Number"] = "光圈值",
        ["Exposure Program"] = "曝光程序",
        ["ISO Speed Ratings"] = "ISO 感光度",
        ["Sensitivity Type"] = "感光度类型",
        ["Date/Time Original"] = "拍摄日期",
        ["Date/Time Digitized"] = "数字化日期",
        ["Shutter Speed Value"] = "快门速度",
        ["Aperture Value"] = "光圈大小",
        ["Brightness Value"] = "亮度值",
        ["Exposure Bias Value"] = "曝光补偿",
        ["Max Aperture Value"] = "最大光圈",
        ["Metering Mode"] = "测光模式",
        ["Flash"] = "闪光灯",
        ["Focal Length"] = "焦距",
        ["Color Space"] = "色彩空间",
        ["Focal Plane X Resolution"] = "焦平面水平分辨率",
        ["Focal Plane Y Resolution"] = "焦平面垂直分辨率",
        ["Focal Plane Resolution Unit"] = "焦平面分辨率单位",
        ["Sensing Method"] = "感光方式",
        ["File Source"] = "文件来源",
        ["Scene Type"] = "场景类型",
        ["Custom Rendered"] = "自定义渲染",
        ["Exposure Mode"] = "曝光模式",
        ["White Balance Mode"] = "白平衡模式",
        ["White Balance"] = "白平衡",
        ["Digital Zoom Ratio"] = "数码变焦比例",
        ["Focal Length 35"] = "35mm 等效焦距",
        ["Scene Capture Type"] = "场景拍摄类型",
        ["Gain Control"] = "增益控制",
        ["Contrast"] = "对比度",
        ["Saturation"] = "饱和度",
        ["Sharpness"] = "锐度",
        ["Subject Distance Range"] = "拍摄距离范围",
        ["Lens Specification"] = "镜头规格",
        ["Lens Make"] = "镜头制造商",
        ["Lens Model"] = "镜头型号",
        ["Lens Serial Number"] = "镜头序列号",
        ["Body Serial Number"] = "机身序列号",
        // GPS
        ["GPS Latitude Ref"] = "GPS 纬度参考",
        ["GPS Latitude"] = "GPS 纬度",
        ["GPS Longitude Ref"] = "GPS 经度参考",
        ["GPS Longitude"] = "GPS 经度",
        ["GPS Altitude Ref"] = "GPS 海拔参考",
        ["GPS Altitude"] = "GPS 海拔",
        ["GPS Time-Stamp"] = "GPS 时间戳",
        ["GPS Date Stamp"] = "GPS 日期戳",
        // 通用
        ["Image Width"] = "图片宽度",
        ["Image Height"] = "图片高度",
        ["Bits Per Sample"] = "每样本位数",
        ["Compression"] = "压缩方式",
        ["Photometric Interpretation"] = "光度解释",
        ["Samples Per Pixel"] = "每像素样本数",
        ["Rows Per Strip"] = "每段行数",
        ["Strip Byte Counts"] = "段字节数",
    };

    private static readonly Dictionary<string, string> DirectoryNameZh = new()
    {
        ["Exif IFD0"] = "Exif 主图像",
        ["Exif SubIFD"] = "Exif 拍摄参数",
        ["Exif SubIFD2"] = "Exif 拍摄参数",
        ["Interop IFD"] = "互通性",
        ["GPS"] = "GPS 位置",
        ["Exif Thumbnail"] = "Exif 缩略图",
        ["JPEG"] = "JPEG 信息",
        ["JFIF"] = "JFIF 信息",
        ["JFXX"] = "JFXX 扩展",
        ["Huffman"] = "霍夫曼表",
        ["File Type"] = "文件类型",
        ["File"] = "文件信息",
        ["ICC Profile"] = "ICC 色彩配置",
        ["PrintIM"] = "PrintIM 打印信息",
        ["PNG"] = "PNG 信息",
        ["PNG-IHDR"] = "PNG 头部",
        ["PNG-gAMA"] = "PNG Gamma",
        ["PNG-tEXt"] = "PNG 文本",
        ["PNG-pHYs"] = "PNG 物理尺寸",
        ["GIF Header"] = "GIF 头部",
        ["GIF Control"] = "GIF 控制",
        ["BMP Header"] = "BMP 头部",
        ["WebP"] = "WebP 信息",
    };

    public ImageDetailView()
    {
        InitializeComponent();
    }

    public ImageDetailView(ImageItem item, PixelSize pixelSize) : this()
    {
        var ext = Path.GetExtension(item.FileName).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrEmpty(ext))
        {
            ext = "未知";
        }

        FileNameText.Text = item.FileName;
        FormatText.Text = ext;
        DimensionText.Text = $"{pixelSize.Width} × {pixelSize.Height}";
        FileSizeText.Text = item.FileSizeDisplay;
        ModifiedTimeText.Text = item.ModifiedTime.ToString("yyyy-MM-dd HH:mm:ss");
        FilePathText.Text = item.FilePath;

        LoadExif(item.FilePath);
    }

    private void LoadExif(string filePath)
    {
        try
        {
            var loc = App.Services.GetRequiredService<ILocalizationService>();
            var isZh = loc.CurrentLanguageCode.StartsWith("zh");

            var directories = ImageMetadataReader.ReadMetadata(filePath);
            var groups = new AvaloniaList<ExifGroup>();

            foreach (var directory in directories)
            {
                var tags = new AvaloniaList<ExifTag>();
                foreach (var tag in directory.Tags)
                {
                    if (!string.IsNullOrEmpty(tag.Description))
                    {
                        var name = tag.Name;
                        if (isZh && ExifNameZh.TryGetValue(name, out var zhName))
                        {
                            name = zhName;
                        }
                        tags.Add(new ExifTag(name, tag.Description));
                    }
                }

                if (tags.Count > 0)
                {
                    var dirName = directory.Name;
                    if (isZh && DirectoryNameZh.TryGetValue(dirName, out var zhDir))
                    {
                        dirName = zhDir;
                    }
                    groups.Add(new ExifGroup(dirName, tags));
                }
            }

            ExifItemsControl.ItemsSource = groups;

            if (groups.Count > 0)
            {
                NoExifText.IsVisible = false;
            }
        }
        catch
        {
            NoExifText.IsVisible = true;
        }
    }
}

public sealed class ExifGroup
{
    public string DirectoryName { get; set; } = string.Empty;
    public AvaloniaList<ExifTag> Tags { get; set; } = new();

    public ExifGroup() { }

    public ExifGroup(string directoryName, AvaloniaList<ExifTag> tags)
    {
        DirectoryName = directoryName;
        Tags = tags;
    }
}

public sealed class ExifTag
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public ExifTag() { }

    public ExifTag(string name, string value)
    {
        Name = name;
        Value = value;
    }
}
