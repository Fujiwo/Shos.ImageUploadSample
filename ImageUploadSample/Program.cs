using ImageUploadSample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// The connection string comes only from "ConnectionStrings:ImageContext" in the configuration (appsettings.json).
// "dotnet ef" also gets the context from here.
builder.Services.AddDbContext<ImageContext>(
    options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("ImageContext")));
// Validates the antiforgery token on every unsafe request (POST and so on), so no action can forget it.
// The forms send it as a hidden field, and the camera upload sends it in the RequestVerificationToken header.
builder.Services.AddControllersWithViews(
    options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

// Configure the HTTP request pipeline.
// Development shows the detailed exception page instead.
if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Images/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// "/" opens the upload page, which is also the image list.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Images}/{action=Upload}/{id?}");

app.Run();
