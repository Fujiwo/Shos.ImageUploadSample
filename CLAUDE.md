# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

ASP.NET Core 10.0 MVC sample app (solution `Shos.ImageUploadSample.sln`: app `ImageUploadSample/`, tests `ImageUploadSample.Tests/`) that uploads images, from a file or from the browser camera, and stores them, along with generated thumbnails, in SQL Server (LocalDB) via EF Core 10. `README.md` is in English and `README.jp.md` is its Japanese counterpart; keep the two in sync. Commit messages are in Japanese.

## Commands

```bash
dotnet build                     # from the repo root (solution) or ImageUploadSample/
dotnet test                      # from the repo root; runs on any OS (SQLite in-memory)
dotnet run                       # from ImageUploadSample/; http://localhost:5257, https://localhost:7020
dotnet ef database update        # from ImageUploadSample/ (requires: dotnet tool install --global dotnet-ef)
dotnet ef migrations add <Name>  # after changing the ImageRecord entity
```

CI (`.github/workflows/build.yml`) runs restore, build, test, and a vulnerable-package check on `ubuntu-latest`. There is no lint configuration.

## Platform notes

- Image processing uses SkiaSharp (with `SkiaSharp.NativeAssets.Linux.NoDependencies`), so the app and tests run on Windows and Linux.
- The default connection string targets LocalDB, which is Windows-only. Running the app elsewhere needs a different SQL Server connection string in `appsettings.json`.
- In WSL, `ImageUploadSample/obj` may hold a Windows-side build cache that the Linux SDK can't read. Build with `--artifacts-path <dir>` to keep the two separate.
- Avoid ImageSharp: 3.x has unpatched vulnerabilities, and 4.x requires a license key at build time.
- `System.Security.Cryptography.Pkcs` is referenced only to pin a vulnerable transitive dependency (see the comment in the csproj).

## Architecture

- The default route is `{controller=Images}/{action=Upload}/{id?}`. `ImagesController` is the only controller and also hosts `Error` (the non-Development exception handler is `/Images/Error`).
- `Services/ImageProcessor.cs` holds all image logic: header-only dimension check (max 8000px, max 2 MB), format detection (the MIME type comes from the content, not from the client), single decode for the 100px-high PNG thumbnail, and strict `data:image/...;base64,` parsing. Failures throw `InvalidImageException`, which the controller turns into 400.
- `ImagesController` upload paths both go through `Save`:
  - `POST Upload(IFormFile file, string description)`: a multipart form, redirects to the list.
  - `POST UploadCameraImage(string imageBase64, string description)`: called by jQuery `$.ajax` from inline JS in `Views/Images/Upload.cshtml`, and returns 204.
- Antiforgery validation is global (`AutoValidateAntiforgeryTokenAttribute`). The camera Ajax call sends the token in the `RequestVerificationToken` header.
- The list (`GET Upload?page=n`) projects to `ImageSummary` (no blobs) with 24 items per page. Images are served by `GET Images/Thumbnail/{id}` and `GET Images/Original/{id}`. `POST Images/Delete/{id}?page=n` deletes the row (thumbnail and original together) with `ExecuteDeleteAsync`; the list asks for confirmation in JS first.
- `Models/ImageContext.cs` holds the `ImageRecord` entity (table `Images`) and `ImageContext`, which takes options only. The connection string comes solely from `ConnectionStrings:ImageContext`, and `dotnet ef` picks it up through `Program.cs`.
- Integration tests (`TestApplicationFactory`) swap the SQL Server registration for in-memory SQLite by removing `DbContextOptions<ImageContext>` and `IDbContextOptionsConfiguration<ImageContext>`.
