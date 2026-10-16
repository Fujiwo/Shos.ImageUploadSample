using ImageUploadSample.Services;
using SkiaSharp;

namespace ImageUploadSample.Tests;

// Unit tests of ImageProcessor. They need no web host or database.
public class ImageProcessorTests
{
    // Arguments: the original width and height, then the expected thumbnail width and height.
    // Every thumbnail is 100 px high; small images are scaled up and thin ones are at least 1 px wide.
    [Theory]
    [InlineData(400, 200, 200, 100)]
    [InlineData(200, 400,  50, 100)]
    [InlineData(100, 100, 100, 100)]
    [InlineData(1, 1000,    1, 100)]
    public void GetThumbnailSize_ScalesToFixedHeight(int width, int height, int expectedWidth, int expectedHeight)
        => Assert.Equal((expectedWidth, expectedHeight), ImageProcessor.GetThumbnailSize(width, height));

    [Fact]
    public void Process_CreatesScaledDownThumbnail()
    {
        var result = ImageProcessor.Process(TestImages.Create(800, 400));

        Assert.Equal((800, 400), (result.Width, result.Height));
        Assert.Equal((200, 100), (result.ThumbnailWidth, result.ThumbnailHeight));

        using var thumbnail = SKBitmap.Decode(result.ThumbnailData);
        Assert.Equal((200, 100), (thumbnail.Width, thumbnail.Height));
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png , "image/png" )]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public void Process_DetectsMimeTypeFromContent(SKEncodedImageFormat format, string expectedMimeType)
        => Assert.Equal(expectedMimeType, ImageProcessor.Process(TestImages.Create(10, 10, format)).MimeType);

    [Fact]
    public void Process_RejectsNonImage()
    {
        var exception = Assert.Throws<InvalidImageException>(() => ImageProcessor.Process("not an image"u8.ToArray()));
        Assert.Equal("Invalid image.", exception.Message);
    }

    [Fact]
    public void Process_RejectsTooLargeDimensions()
    {
        var exception = Assert.Throws<InvalidImageException>(
            () => ImageProcessor.Process(TestImages.Create(ImageProcessor.MaximumDimension + 1, 1)));
        Assert.Equal("Too large image dimensions.", exception.Message);
    }

    // 4001 x 4000 is within the per-side limit but just over 16 megapixels.
    [Fact]
    public void Process_RejectsTooManyPixels()
    {
        var exception = Assert.Throws<InvalidImageException>(
            () => ImageProcessor.Process(TestImages.Create(4001, 4000)));
        Assert.Equal("Too many image pixels.", exception.Message);
    }

    // Very wide images are limited by the maximum thumbnail width (800 px) instead of the height.
    [Theory]
    [InlineData(8000, 1, 800, 1)]
    [InlineData(8000, 1000, 800, 100)]
    public void GetThumbnailSize_BoundsDimensions(
        int width, int height, int expectedWidth, int expectedHeight)
        => Assert.Equal(
            (expectedWidth, expectedHeight),
            ImageProcessor.GetThumbnailSize(width, height));

    [Fact]
    public void Process_RejectsTooLargeFile()
    {
        var exception = Assert.Throws<InvalidImageException>(
            () => ImageProcessor.Process(new byte[ImageProcessor.MaximumFileSize + 1]));
        Assert.Equal("Too large file.", exception.Message);
    }

    [Fact]
    public void ParseDataUrl_DecodesValidDataUrl()
    {
        byte[] data = [1, 2, 3, 4, 5];
        Assert.Equal(data, ImageProcessor.ParseDataUrl(data.ToDataUrl()));
    }

    // Missing data, a missing ";base64", a non-image type, invalid Base64, and prefixes that are not at the start.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("data:image/png,AQID")]
    [InlineData("data:text/html;base64,AQID")]
    [InlineData("data:image/png;base64,***")]
    [InlineData("xdata:image/png;base64,AQID")]
    [InlineData("image/png;base64,AQID")]
    public void ParseDataUrl_ReturnsNullForInvalidInput(string? dataUrl)
        => Assert.Null(ImageProcessor.ParseDataUrl(dataUrl));
}
