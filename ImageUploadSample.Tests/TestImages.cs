using SkiaSharp;

namespace ImageUploadSample.Tests;

// Creates image data for the tests in memory, so that no image files are needed.
static class TestImages
{
    // Encodes a solid color image of the given size and format.
    public static byte[] Create(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    extension(byte[] data)
    {
        // Wraps the bytes in a data URL, as canvas.toDataURL() does in the browser.
        public string ToDataUrl(string mimeType = "image/png")
            => $"data:{mimeType};base64,{Convert.ToBase64String(data)}";
    }
}
