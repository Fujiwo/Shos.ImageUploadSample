using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ImageUploadSample.Models;

public class ImageRecord
{
    public const int DescriptionMaxLength = 200;

    public int Id { get; set; }

    [Required]
    public byte[] Data { get; set; } = [];
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] ThumbnailData { get; set; } = [];
    public int ThumbnailWidth { get; set; }
    public int ThumbnailHeight { get; set; }
    [MaxLength(DescriptionMaxLength)]
    public string Description { get; set; } = string.Empty;
    [MaxLength(32)]
    public string MimeType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ImageContext(DbContextOptions<ImageContext> options) : DbContext(options)
{
    public DbSet<ImageRecord> Images => Set<ImageRecord>();
}
