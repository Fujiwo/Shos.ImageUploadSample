using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Drawing        = System.Drawing;
using DrawingImaging = System.Drawing.Imaging;

namespace ImageUploadSample.Controllers;

using Models;
using System.IO;
using System.Runtime.Versioning;

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
    [SupportedOSPlatform("windows")]
    public async Task<IActionResult> Upload(IFormFile file, string description)
    {
        const int maximumFileSize = 2 * 1024 * 1024; // 2 MB

        if (file == null || file.Length == 0 || file.Length > maximumFileSize)
            return BadRequest("No file selected.");

        if (file.Length > maximumFileSize)
            return BadRequest("Too large file.");

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        var (width, height) = GetImageDimensions(memoryStream);
        if (width == 0 || height == 0)
            return BadRequest("Invalid image.");

        var imageData = memoryStream.ToArray();
        await Save(file.ContentType, description, width, height, imageData);

        return RedirectToAction();
    }

    [HttpPost]
    [SupportedOSPlatform("windows")]
    public async Task<IActionResult> UploadCameraImage(string imageBase64, string description)
    {
        if (string.IsNullOrEmpty(imageBase64))
            return BadRequest("No image selected.");

        var splitedImageBase64 = SplitImageBase64(imageBase64);
        if (splitedImageBase64 is null)
            return BadRequest("Invalid image.");

        var (contentType, base64Text) = splitedImageBase64.Value;
        var imageData = Convert.FromBase64String(base64Text);

        using var memoryStream = new MemoryStream(imageData);
        var (width, height) = GetImageDimensions(memoryStream);
        await Save(contentType, "", width, height, imageData);

        return RedirectToAction("Upload");
    }

    static (string contentType, string base64Text)? SplitImageBase64(string imageBase64) {
        string[] parts = imageBase64.Split(';');
        if (parts.Length < 2)
            return null;

        var contentType = parts[0].Replace("data:", "");
        var base64 = imageBase64.Replace($"{parts[0]};base64,", "");
        return (contentType, base64);
    }

    [SupportedOSPlatform("windows")]
    async Task Save(string contentType, string description, int width, int height, byte[] imageData)
    {
        Image image = ToImage(contentType, description, width, height, imageData);

        context.Images.Add(image);
        await context.SaveChangesAsync();
    }

    [SupportedOSPlatform("windows")]
    static Image ToImage(string contentType, string description, int width, int height, byte[] imageData)
    {
        var (thumbnailData, thumnailWidth, thumnailHeight) = CreateThumbnail(imageData, width, height);

        var image = new Image {
            Data            = imageData     ,
            Width           = width         ,
            Height          = height        ,
            ThumbnailData   = thumbnailData ,
            ThumbnailWidth  = thumnailWidth ,
            ThumbnailHeight = thumnailHeight,
            Description     = description   ,
            MimeType        = contentType
        };
        return image;
    }

    // Supported only on Windows 6.1 and later.
    [SupportedOSPlatform("windows")]
    (int width, int height) GetImageDimensions(Stream stream)
    {
        var image = new Drawing.Bitmap(stream);
        return (image.Width, image.Height);
    }

    // Supported only on Windows 6.1 and later.
    [SupportedOSPlatform("windows")]
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
