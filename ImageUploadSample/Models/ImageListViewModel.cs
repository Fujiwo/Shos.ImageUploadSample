namespace ImageUploadSample.Models;

/// <summary>
/// One item of the image list: the columns of <see cref="ImageRecord"/> except the image data,
/// so that showing the list does not read any blob from the database.
/// </summary>
public record ImageSummary(
    int    Id,
    string Description,
    string MimeType,
    int    Width,
    int    Height,
    int    ThumbnailWidth,
    int    ThumbnailHeight
)
{
    // Used as the alt text, the caption, and the delete confirmation message, e.g. "My cat (image/jpeg)".
    public string AlternativeText => $"{Description} ({MimeType})";
}

/// <summary>
/// The model of Upload.cshtml: one page of images and the paging state.
/// </summary>
/// <param name="Page">The current 1-based page number.</param>
/// <param name="TotalPages">The number of pages; at least 1 even when there are no images.</param>
public record ImageListViewModel(IReadOnlyList<ImageSummary> Images, int Page, int TotalPages);
