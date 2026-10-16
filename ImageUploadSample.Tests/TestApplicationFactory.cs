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
    // An in-memory SQLite database lives only while its connection is open,
    // so one connection is kept open for the lifetime of the factory and shared by every request.
    readonly SqliteConnection connection = new("DataSource=:memory:");

    public TestApplicationFactory() => connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.ConfigureServices(services => {
            // AddDbContext in Program.cs registers both of these. Both must be removed;
            // otherwise UseSqlServer would still run, and EF Core rejects two providers on one context.
            services.RemoveAll<DbContextOptions<ImageContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ImageContext>>();
            services.AddDbContext<ImageContext>(options => options.UseSqlite(connection));

            // Creates the schema from the model (no migrations, which are written for SQL Server).
            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<ImageContext>().Database.EnsureCreated();
        });

    // Reads the stored rows directly from the database, so that the tests can check what was saved.
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
