using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Drawing        = System.Drawing;
using DrawingImaging = System.Drawing.Imaging;

namespace ImageUploadSample.Controllers;

using Models;

public class ImagesController : Controller
{
    ImageContext context;

    public ImagesController(ImageContext context) => this.context = context;

    public async Task<IActionResult> Upload()
    {
        var images = await context.Images.ToListAsync();
        return View(images);
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, string description)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file selected.");

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        var (width, height) = GetImageDimensions(memoryStream);
        if (width == 0 || height == 0)
            return BadRequest("Invalid image.");

        var imageData = memoryStream.ToArray();
        var (thumbnailData, thumnailWidth, thumnailHeight) = CreateThumbnail(imageData, width, height);

        var image = new Image {
            Data            = imageData,
            Width           = width,
            Height          = height,
            ThumbnailData   = thumbnailData,
            ThumbnailWidth  = thumnailWidth,
            ThumbnailHeight = thumnailHeight,
            Description     = description,
            MimeType        = file.ContentType
        };

        context.Images.Add(image);
        await context.SaveChangesAsync();

        return RedirectToAction();
    }

    // Supported only on Windows 6.1 and later.
    (int width, int height) GetImageDimensions(Stream stream)
    {
        var image = new Drawing.Bitmap(stream);
        return (image.Width, image.Height);
    }

    // Supported only on Windows 6.1 and later.
    static (byte[], int, int) CreateThumbnail(byte[] imageData, int width, int height)
    {
        const int thumbnailSize = 100;
        var (thumbnailWidth, thumbnailHeight) = (thumbnailSize * width / height, thumbnailSize);

        using var imageStream = new MemoryStream(imageData);
        using var thumbnailStream = new MemoryStream();
        var thumbnail = CreateThumbnail(imageStream, width, height);
        thumbnail.Save(thumbnailStream, DrawingImaging.ImageFormat.Png);
        return (thumbnailStream.ToArray(), thumbnailWidth, thumbnailHeight);

        static Drawing.Bitmap CreateThumbnail(Stream stream, int width, int height)
        {
            var image     = new Drawing.Bitmap(stream);
            var thumbnail = image.GetThumbnailImage(width, height, () => false, IntPtr.Zero);
            return (Drawing.Bitmap)thumbnail;
        }
    }
}
