using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ImageUploadSample.Controllers;

using Models;
using Services;

public class ImagesController(ImageContext context) : Controller
{
    const int pageSize         = 24;
    const int requestSizeLimit = 4 * 1024 * 1024; // the 2 MB image plus multipart / base64 overhead

    readonly ImageContext context = context;

    public async Task<IActionResult> Upload(int page = 1)
    {
        var totalCount = await context.Images.CountAsync();
        var totalPages = Math.Max(1, (totalCount + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);

        var images = await context.Images
            .OrderByDescending(image => image.CreatedAt)
            .ThenByDescending(image => image.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(image => new ImageSummary(
                image.Id, image.Description, image.MimeType,
                image.Width, image.Height, image.ThumbnailWidth, image.ThumbnailHeight))
            .ToListAsync();

        return View(new ImageListViewModel(images, page, totalPages));
    }

    [HttpPost]
    [RequestSizeLimit(requestSizeLimit)]
    public async Task<IActionResult> Upload(IFormFile? file, string? description)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file selected.");

        if (file.Length > ImageProcessor.MaximumFileSize)
            return BadRequest("Too large file.");

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        var error = await Save(memoryStream.ToArray(), description);
        return error is null ? RedirectToAction() : BadRequest(error);
    }

    [HttpPost]
    [RequestSizeLimit(requestSizeLimit)]
    public async Task<IActionResult> UploadCameraImage(string? imageBase64, string? description)
    {
        if (string.IsNullOrEmpty(imageBase64))
            return BadRequest("No image selected.");

        var imageData = ImageProcessor.ParseDataUrl(imageBase64);
        if (imageData is null)
            return BadRequest("Invalid image.");

        var error = await Save(imageData, description);
        return error is null ? NoContent() : BadRequest(error);
    }

    // The thumbnail and the original are in the same row, so this deletes both.
    // ExecuteDeleteAsync deletes without loading the blobs.
    [HttpPost]
    public async Task<IActionResult> Delete(int id, int page = 1)
    {
        var deletedCount = await context.Images.Where(image => image.Id == id).ExecuteDeleteAsync();
        return deletedCount == 0 ? NotFound() : RedirectToAction(nameof(Upload), new { page });
    }

    [HttpGet]
    [ResponseCache(Duration = 24 * 60 * 60)]
    public async Task<IActionResult> Thumbnail(int id)
    {
        var thumbnailData = await context.Images
            .Where(image => image.Id == id)
            .Select(image => image.ThumbnailData)
            .FirstOrDefaultAsync();
        return thumbnailData is null ? NotFound() : File(thumbnailData, "image/png");
    }

    [HttpGet]
    [ResponseCache(Duration = 24 * 60 * 60)]
    public async Task<IActionResult> Original(int id)
    {
        var image = await context.Images
            .Where(image => image.Id == id)
            .Select(image => new { image.Data, image.MimeType })
            .FirstOrDefaultAsync();
        return image is null ? NotFound() : File(image.Data, image.MimeType);
    }

    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));

    // Returns an error message, or null on success.
    async Task<string?> Save(byte[] imageData, string? description)
    {
        description ??= string.Empty;
        if (description.Length > ImageRecord.DescriptionMaxLength)
            return "Too long description.";

        ProcessedImage processedImage;
        try {
            processedImage = ImageProcessor.Process(imageData);
        } catch (InvalidImageException exception) {
            return exception.Message;
        }

        context.Images.Add(new ImageRecord {
            Data            = imageData                     ,
            Width           = processedImage.Width          ,
            Height          = processedImage.Height         ,
            ThumbnailData   = processedImage.ThumbnailData  ,
            ThumbnailWidth  = processedImage.ThumbnailWidth ,
            ThumbnailHeight = processedImage.ThumbnailHeight,
            Description     = description                   ,
            MimeType        = processedImage.MimeType
        });
        await context.SaveChangesAsync();
        return null;
    }
}
