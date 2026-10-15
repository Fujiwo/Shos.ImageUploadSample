# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

ASP.NET Core 10.0 MVC sample app (single project `ImageUploadSample/`, solution `Shos.ImageUploadSample.sln`) that uploads images, from a file or from the browser camera, and stores them, along with generated thumbnails, in SQL Server (LocalDB) via EF Core 8. README and commit messages are in Japanese.

## Commands

Run these from `ImageUploadSample/`:

```bash
dotnet build
dotnet run                       # launch profiles: http://localhost:5257, https://localhost:7020
dotnet ef database update        # apply migrations (requires: dotnet tool install --global dotnet-ef)
dotnet ef migrations add <Name>  # after changing the Image entity
```

There is no test project and no lint configuration.

## Platform constraints

- Runs on Windows only. Image dimension and thumbnail code uses `System.Drawing.Common` (`Bitmap`, `GetThumbnailImage`), and those methods carry `[SupportedOSPlatform("windows")]`. Under WSL/Linux the project builds, but uploads fail at runtime. LocalDB is Windows-only too.
- Running the app or EF migrations needs a Windows `dotnet` with LocalDB available.

## Architecture

- The default route is `{controller=Images}/{action=Upload}/{id?}`. `ImagesController` is the only controller. Note that there is no `HomeController`, even though `Program.cs` points the non-Development exception handler at `/Home/Error`.
- `ImagesController` has two upload paths that share `Save` → `ToImage` → `CreateThumbnail`:
  - `POST Upload(IFormFile file, string description)`: a multipart form upload, limited to 2 MB.
  - `POST UploadCameraImage(string imageBase64, string description)`: called by jQuery `$.ajax` from inline JS in `Views/Images/Upload.cshtml`. The JS captures `getUserMedia` video to a canvas and posts a `data:image/png;base64,...` URL, which `SplitImageBase64` parses. This path currently saves `""` as the description and ignores the parameter.
- `Models/ImageContext.cs` holds both the `Image` entity (original bytes plus a PNG thumbnail scaled to a fixed height of 100px, stored as `byte[]` columns) and `ImageContext`.
- **Connection string gotcha:** `ImageContext.OnConfiguring` unconditionally calls `UseSqlServer` with a hard-coded connection string and sets up Debug logging. As a result, the `ConnectionStrings:ImageContext` value from `appsettings.json` registered in `Program.cs` is effectively overridden. To change the database, update both places, or make `OnConfiguring` respect `optionsBuilder.IsConfigured`.
- `Upload.cshtml` renders every image (full size and thumbnail) inline as base64 data URIs from `context.Images.ToListAsync()`. There is no paging and no separate image-serving endpoint.
