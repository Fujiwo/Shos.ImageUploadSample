using SkiaSharp;

namespace ImageUploadSample.Tests;

static class TestImages
{
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
        public string ToDataUrl(string mimeType = "image/png")
            => $"data:{mimeType};base64,{Convert.ToBase64String(data)}";
    }
}
