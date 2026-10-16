using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ImageUploadSample.Models;

/// <summary>
/// An uploaded image (table "Images"). The original and its thumbnail are stored together in one row.
/// </summary>
/// <remarks>
/// After changing this class, add a migration with "dotnet ef migrations add &lt;Name&gt;".
/// </remarks>
public class ImageRecord
{
    public const int DescriptionMaxLength = 200;

    public int Id { get; set; }

    // The original image, stored exactly as uploaded.
    [Required]
    public byte[] Data { get; set; } = [];
    // The size of the original in pixels. The list uses it to give the modal image its aspect ratio before it loads.
    public int Width { get; set; }
    public int Height { get; set; }
    // The thumbnail is always PNG; see ImageProcessor.GetThumbnailSize for its size.
    public byte[] ThumbnailData { get; set; } = [];
    public int ThumbnailWidth { get; set; }
    public int ThumbnailHeight { get; set; }
    // Optional text entered by the user (empty when not given).
    [MaxLength(DescriptionMaxLength)]
    public string Description { get; set; } = string.Empty;
    // The MIME type of the original, detected from its content (for example "image/jpeg").
    [MaxLength(32)]
    public string MimeType { get; set; } = string.Empty;
    // The upload time in UTC. The list shows the newest images first.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The EF Core database context of the app.
/// </summary>
/// <remarks>
/// It takes only options, so the provider and the connection string are chosen where it is registered:
/// SQL Server in Program.cs, and in-memory SQLite in the integration tests.
/// </remarks>
public class ImageContext(DbContextOptions<ImageContext> options) : DbContext(options)
{
    public DbSet<ImageRecord> Images => Set<ImageRecord>();
}
