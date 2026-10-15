using System.Text.RegularExpressions;
using SkiaSharp;

namespace ImageUploadSample.Services;

public class InvalidImageException(string message) : Exception(message);

public record ProcessedImage(
    string MimeType,
    int    Width,
    int    Height,
    byte[] ThumbnailData,
    int    ThumbnailWidth,
    int    ThumbnailHeight
);

public static partial class ImageProcessor
{
    public const int MaximumFileSize  = 2 * 1024 * 1024; // 2 MB
    public const int MaximumDimension = 8000;            // pixels (width and height each)
    public const int ThumbnailSize    = 100;             // thumbnail height in pixels

    static readonly Dictionary<SKEncodedImageFormat, string> supportedFormats = new() {
        [SKEncodedImageFormat.Png ] = "image/png" ,
        [SKEncodedImageFormat.Jpeg] = "image/jpeg",
        [SKEncodedImageFormat.Gif ] = "image/gif" ,
        [SKEncodedImageFormat.Bmp ] = "image/bmp" ,
        [SKEncodedImageFormat.Webp] = "image/webp"
    };

    // Reads the header first and rejects oversized images before decoding them (decompression bomb protection).
    // The image is then decoded only once, to create the thumbnail.
    public static ProcessedImage Process(byte[] imageData)
    {
        if (imageData.Length == 0)
            throw new InvalidImageException("No image selected.");
        if (imageData.Length > MaximumFileSize)
            throw new InvalidImageException("Too large file.");

        using var codec = SKCodec.Create(new SKMemoryStream(imageData))
                          ?? throw new InvalidImageException("Invalid image.");

        if (!supportedFormats.TryGetValue(codec.EncodedFormat, out var mimeType))
            throw new InvalidImageException("Unsupported image format.");

        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width <= 0 || height <= 0)
            throw new InvalidImageException("Invalid image.");
        if (width > MaximumDimension || height > MaximumDimension)
            throw new InvalidImageException("Too large image dimensions.");

        var (thumbnailWidth, thumbnailHeight) = GetThumbnailSize(width, height);
        var thumbnailData = CreateThumbnail(codec, thumbnailWidth, thumbnailHeight);
        return new(mimeType, width, height, thumbnailData, thumbnailWidth, thumbnailHeight);
    }

    public static (int width, int height) GetThumbnailSize(int width, int height)
        => (Math.Max(1, (int)Math.Round((double)ThumbnailSize * width / height)), ThumbnailSize);

    static byte[] CreateThumbnail(SKCodec codec, int width, int height)
    {
        using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidImageException("Invalid image.");
        using var thumbnail = bitmap.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                              ?? throw new InvalidImageException("Invalid image.");
        using var image = SKImage.FromBitmap(thumbnail);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [GeneratedRegex(@"^data:image/(png|jpeg|gif|bmp|webp);base64,", RegexOptions.CultureInvariant)]
    private static partial Regex DataUrlPrefix();

    // Parses "data:image/png;base64,...". Returns null when the format is invalid.
    public static byte[]? ParseDataUrl(string? dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl))
            return null;

        var match = DataUrlPrefix().Match(dataUrl);
        if (!match.Success)
            return null;

        var base64Text = dataUrl.AsSpan(match.Length);
        var buffer = new byte[base64Text.Length * 3 / 4];
        return Convert.TryFromBase64Chars(base64Text, buffer, out var bytesWritten)
               ? buffer[..bytesWritten]
               : null;
    }
}
