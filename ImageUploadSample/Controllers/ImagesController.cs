using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ImageUploadSample.Controllers;

using Models;
using Services;

/// <summary>
/// The only controller of the app: lists, uploads, serves, and deletes images, and shows the error page.
/// </summary>
/// <remarks>
/// Antiforgery tokens are validated for every POST by the global filter registered in Program.cs.
/// </remarks>
public class ImagesController(ImageContext context) : Controller
{
    const int pageSize         = 24;              // images per page in the list
    const int requestSizeLimit = 4 * 1024 * 1024; // the 2 MB image plus multipart / base64 overhead

    readonly ImageContext context = context;

    /// <summary>
    /// GET /Images/Upload?page=n (also the default route "/"): shows the upload forms and one page of the image list.
    /// </summary>
    /// <param name="page">The 1-based page number. Out-of-range values are clamped to an existing page.</param>
    public async Task<IActionResult> Upload(int page = 1)
    {
        var totalCount = await context.Images.CountAsync();
        // Rounds up, and keeps at least one page so that an empty list still has a valid page.
        var totalPages = Math.Max(1, (totalCount + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);

        // Newest first. Id breaks ties between images with the same CreatedAt so that the paging is stable.
        // The projection to ImageSummary leaves out the blob columns, so no image data is read for the list;
        // the browser fetches the thumbnails separately through Thumbnail().
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

    /// <summary>
    /// POST /Images/Upload: saves an image sent from the file form (multipart/form-data).
    /// </summary>
    /// <returns>A redirect to the list on success; otherwise 400 with the error message.</returns>
    [HttpPost]
    [RequestSizeLimit(requestSizeLimit)]
    public async Task<IActionResult> Upload(IFormFile? file, string? description)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file selected.");

        // Checked before reading the file into memory. ImageProcessor checks the size again.
        if (file.Length > ImageProcessor.MaximumFileSize)
            return BadRequest("Too large file.");

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        var error = await Save(memoryStream.ToArray(), description);
        // RedirectToAction() with no arguments goes to GET Upload (Post/Redirect/Get),
        // so that reloading the page does not upload the image again.
        return error is null ? RedirectToAction() : BadRequest(error);
    }

    /// <summary>
    /// POST /Images/UploadCameraImage: saves a camera snapshot sent by fetch from Upload.cshtml.
    /// </summary>
    /// <param name="imageBase64">The snapshot as a data URL ("data:image/png;base64,...").</param>
    /// <returns>204 on success (the script then reloads the list); otherwise 400 with the error message.</returns>
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

    /// <summary>
    /// POST /Images/Delete/{id}?page=n: deletes an image and returns to the same page of the list.
    /// </summary>
    // The thumbnail and the original are in the same row, so this deletes both.
    // ExecuteDeleteAsync deletes without loading the blobs.
    [HttpPost]
    public async Task<IActionResult> Delete(int id, int page = 1)
    {
        var deletedCount = await context.Images.Where(image => image.Id == id).ExecuteDeleteAsync();
        // If the deletion empties the last page, Upload() clamps the page number.
        return deletedCount == 0 ? NotFound() : RedirectToAction(nameof(Upload), new { page });
    }

    /// <summary>
    /// GET /Images/Thumbnail/{id}: returns the thumbnail, which is always PNG.
    /// </summary>
    // An image never changes once saved, so the browser may cache it for a day.
    [HttpGet]
    [ResponseCache(Duration = 24 * 60 * 60)]
    public async Task<IActionResult> Thumbnail(int id)
    {
        // Reads only the thumbnail column, not the whole row with the original.
        var thumbnailData = await context.Images
            .Where(image => image.Id == id)
            .Select(image => image.ThumbnailData)
            .FirstOrDefaultAsync();
        return thumbnailData is null ? NotFound() : File(thumbnailData, "image/png");
    }

    /// <summary>
    /// GET /Images/Original/{id}: returns the original image as it was uploaded.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 24 * 60 * 60)]
    public async Task<IActionResult> Original(int id)
    {
        // The anonymous type reads only the two columns needed, skipping the thumbnail.
        var image = await context.Images
            .Where(image => image.Id == id)
            .Select(image => new { image.Data, image.MimeType })
            .FirstOrDefaultAsync();
        // The MIME type was detected from the content when it was saved, so it can be trusted here.
        return image is null ? NotFound() : File(image.Data, image.MimeType);
    }

    /// <summary>
    /// The exception handler page outside Development (see UseExceptionHandler in Program.cs).
    /// </summary>
    // The handler re-executes the failed request here, which may be a POST, so the token must not be checked.
    // The error page must never be cached.
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));

    // Validates the description and the image, then stores the original and its thumbnail in one row.
    // Both upload actions share this method.
    // Returns an error message, or null on success.
    async Task<string?> Save(byte[] imageData, string? description)
    {
        // The description is optional, but the column does not allow null.
        description ??= string.Empty;
        // The input field has maxlength too, but the client cannot be trusted.
        if (description.Length > ImageRecord.DescriptionMaxLength)
            return "Too long description.";

        ProcessedImage processedImage;
        try {
            processedImage = ImageProcessor.Process(imageData);
        } catch (InvalidImageException exception) {
            // Expected for bad input; the message is shown to the user as is.
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
