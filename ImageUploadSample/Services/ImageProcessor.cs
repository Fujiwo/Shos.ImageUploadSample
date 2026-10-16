using System.Text.RegularExpressions;
using SkiaSharp;

namespace ImageUploadSample.Services;

/// <summary>
/// Thrown when uploaded data is not an acceptable image.
/// The message is safe to show to the user; the controller returns it as the body of a 400 response.
/// </summary>
public class InvalidImageException(string message) : Exception(message);

/// <summary>
/// The result of <see cref="ImageProcessor.Process"/>: what was learned from the image and the generated thumbnail.
/// </summary>
/// <param name="MimeType">The MIME type detected from the image content (never the one the client claimed).</param>
/// <param name="Width">The width of the original image in pixels.</param>
/// <param name="Height">The height of the original image in pixels.</param>
/// <param name="ThumbnailData">The thumbnail, always encoded as PNG.</param>
/// <param name="ThumbnailWidth">The width of the thumbnail in pixels.</param>
/// <param name="ThumbnailHeight">The height of the thumbnail in pixels.</param>
public record ProcessedImage(
    string MimeType,
    int    Width,
    int    Height,
    byte[] ThumbnailData,
    int    ThumbnailWidth,
    int    ThumbnailHeight
);

/// <summary>
/// All the image logic of the app: validation, format detection, thumbnail generation, and data URL parsing.
/// It has no state and does not touch the database, so it can be unit tested on its own.
/// </summary>
public static partial class ImageProcessor
{
    // Limits for uploaded images. They bound the memory used to decode a single image
    // (16 megapixels × 4 bytes per pixel ≈ 64 MB at most).
    public const int MaximumFileSize       = 2 * 1024 * 1024; // 2 MB
    public const int MaximumDimension      = 8000;            // pixels (width and height each)
    public const long MaximumPixelCount    = 4000 * 4000;   // 16 megapixels
    // Thumbnails keep the aspect ratio and fit in a box of MaximumThumbnailWidth × ThumbnailSize.
    public const int ThumbnailSize         = 100;             // thumbnail height in pixels
    public const int MaximumThumbnailWidth = 800;        // thumbnail width in pixels

    // The formats accepted, and the MIME type stored for each of them.
    // The browser's <input accept="..."> and DataUrlPrefix() list the same formats.
    static readonly Dictionary<SKEncodedImageFormat, string> supportedFormats = new() {
        [SKEncodedImageFormat.Png ] = "image/png" ,
        [SKEncodedImageFormat.Jpeg] = "image/jpeg",
        [SKEncodedImageFormat.Gif ] = "image/gif" ,
        [SKEncodedImageFormat.Bmp ] = "image/bmp" ,
        [SKEncodedImageFormat.Webp] = "image/webp"
    };

    /// <summary>
    /// Validates <paramref name="imageData"/> and creates its thumbnail.
    /// </summary>
    /// <remarks>
    /// Reads the header first and rejects oversized images before decoding them (decompression bomb protection).
    /// The image is then decoded only once, to create the thumbnail.
    /// </remarks>
    /// <exception cref="InvalidImageException">The data is empty, too large, not a supported image, or too big in pixels.</exception>
    public static ProcessedImage Process(byte[] imageData)
    {
        // Cheap checks on the raw bytes come first.
        if (imageData.Length == 0)
            throw new InvalidImageException("No image selected.");
        if (imageData.Length > MaximumFileSize)
            throw new InvalidImageException("Too large file.");

        // SKCodec.Create reads only the header, so nothing is decoded yet.
        // It returns null when the bytes are not an image that SkiaSharp recognizes.
        using var codec = SKCodec.Create(new SKMemoryStream(imageData))
                          ?? throw new InvalidImageException("Invalid image.");

        // The format comes from the content, so a renamed file or a forged Content-Type cannot fool it.
        if (!supportedFormats.TryGetValue(codec.EncodedFormat, out var mimeType))
            throw new InvalidImageException("Unsupported image format.");

        // A small file can declare huge dimensions, so check them before decoding.
        var (width, height) = codec.Size;
        if (width <= 0 || height <= 0)
            throw new InvalidImageException("Invalid image.");
        if (width > MaximumDimension || height > MaximumDimension)
            throw new InvalidImageException("Too large image dimensions.");
        // long, so that the multiplication cannot overflow.
        if ((long)width * height > MaximumPixelCount)
            throw new InvalidImageException("Too many image pixels.");

        var (thumbnailWidth, thumbnailHeight) = GetThumbnailSize(width, height);
        var thumbnailData = codec.CreateThumbnail(thumbnailWidth, thumbnailHeight);
        return new(mimeType, width, height, thumbnailData, thumbnailWidth, thumbnailHeight);
    }

    /// <summary>
    /// Calculates the thumbnail size for an image of <paramref name="width"/> × <paramref name="height"/> pixels.
    /// </summary>
    /// <remarks>
    /// The aspect ratio is kept. The thumbnail is normally <see cref="ThumbnailSize"/> pixels high,
    /// but a very wide image is scaled down further so that it is at most <see cref="MaximumThumbnailWidth"/> pixels wide.
    /// Small images are scaled up to the same height.
    /// </remarks>
    public static (int width, int height) GetThumbnailSize(int width, int height)
    {
        // The smaller scale is the one that fits both limits.
        var scale = Math.Min(
            (double)ThumbnailSize / height,
            (double)MaximumThumbnailWidth / width);
        // Rounding can give 0 for an extremely thin image, and an image needs at least 1 pixel per side.
        return (
            Math.Clamp((int)Math.Round(width * scale), 1, MaximumThumbnailWidth),
            Math.Clamp((int)Math.Round(height * scale), 1, ThumbnailSize));
    }

    // Private helpers on SKCodec (C# 14 extension members).
    extension(SKCodec codec)
    {
        // The dimensions read from the image header.
        (int Width, int Height) Size => (codec.Info.Width, codec.Info.Height);

        // Decodes the whole image, resizes it, and encodes the result as PNG.
        byte[] CreateThumbnail(int width, int height)
        {
            using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidImageException("Invalid image.");
            // Linear filtering with mipmaps keeps the thumbnail smooth even when it is much smaller than the original.
            using var thumbnail = bitmap.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                                  ?? throw new InvalidImageException("Invalid image.");
            using var image = SKImage.FromBitmap(thumbnail);
            // PNG is lossless, so the quality argument is ignored.
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }

    // Matches only the prefix of a data URL of a supported image type, such as "data:image/png;base64,".
    // The source generator compiles the regular expression at build time.
    [GeneratedRegex(@"^data:image/(png|jpeg|gif|bmp|webp);base64,", RegexOptions.CultureInvariant)]
    private static partial Regex DataUrlPrefix();

    /// <summary>
    /// Decodes a data URL such as <c>data:image/png;base64,...</c>, which the camera upload sends.
    /// </summary>
    /// <returns>The decoded bytes, or <see langword="null"/> when the data URL is invalid.</returns>
    /// <remarks>
    /// The image type in the prefix is checked only for its form;
    /// the actual format is detected from the content later by <see cref="Process"/>.
    /// </remarks>
    public static byte[]? ParseDataUrl(string? dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl))
            return null;

        var match = DataUrlPrefix().Match(dataUrl);
        if (!match.Success)
            return null;

        // Decodes the rest without copying it into a new string.
        var base64Text = dataUrl.AsSpan(match.Length);
        // Every 4 Base64 characters encode at most 3 bytes, so this buffer is always big enough.
        var buffer = new byte[base64Text.Length * 3 / 4];
        // TryFromBase64Chars fails, instead of throwing, on characters that are not Base64.
        // The padding makes the real length shorter than the buffer, so trim it to bytesWritten.
        return Convert.TryFromBase64Chars(base64Text, buffer, out var bytesWritten)
               ? buffer[..bytesWritten]
               : null;
    }
}
