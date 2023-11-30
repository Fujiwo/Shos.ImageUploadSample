//ex. ef.cmd [プロジェクト名]
//cd % 1
//dotnet ef --version
//dotnet tool update --global dotnet-ef
//dotnet ef migrations add InitialCreate
//dotnet ef database update

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace ImageUploadSample.Models;

public class Image
{
    public int Id { get; set; }

    [Required]
    public byte[] Data { get; set; } = new byte[] {};
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] ThumbnailData { get; set; } = new byte[] {};
    public int ThumbnailWidth { get; set; }
    public int ThumbnailHeight { get; set; }
    public string Description { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ImageContext : DbContext
{
    const string connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=20231129;Integrated Security=True;TrustServerCertificate=true;";

    public virtual DbSet<Image> Images { get; set; }

    public ImageContext()
    {}

    public ImageContext(DbContextOptions<ImageContext> options)
         : base(options)
    {}

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
        .UseSqlServer(connectionString)
        .LogTo(message => Debug.WriteLine(message),
               new[] { DbLoggerCategory.Database.Name },
               LogLevel.Debug,
               DbContextLoggerOptions.LocalTime);
}
