using ImageUploadSample.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ImageUploadSample.Tests;

// Replaces SQL Server with in-memory SQLite so that the tests run on any OS.
public class TestApplicationFactory : WebApplicationFactory<Program>
{
    readonly SqliteConnection connection = new("DataSource=:memory:");

    public TestApplicationFactory() => connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.ConfigureServices(services => {
            services.RemoveAll<DbContextOptions<ImageContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ImageContext>>();
            services.AddDbContext<ImageContext>(options => options.UseSqlite(connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<ImageContext>().Database.EnsureCreated();
        });

    public ImageRecord[] GetImages()
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ImageContext>().Images.AsNoTracking().ToArray();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            connection.Dispose();
    }
}
