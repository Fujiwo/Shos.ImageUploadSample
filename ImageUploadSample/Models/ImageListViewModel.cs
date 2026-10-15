namespace ImageUploadSample.Models;

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
    public string AlternativeText => $"{Description} ({MimeType})";
}

public record ImageListViewModel(IReadOnlyList<ImageSummary> Images, int Page, int TotalPages);
