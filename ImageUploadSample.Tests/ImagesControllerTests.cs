using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using ImageUploadSample.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImageUploadSample.Tests;

// Integration tests that send real HTTP requests to the app, backed by in-memory SQLite.
// xUnit creates a new instance for each test, so every test starts with an empty database.
public partial class ImagesControllerTests : IDisposable
{
    readonly TestApplicationFactory factory = new();
    readonly HttpClient client;

    // Redirects are not followed, so that the tests can assert the 302 responses themselves.
    public ImagesControllerTests()
        => client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    // Extracts the value of the hidden antiforgery field that the form tag helper renders.
    [GeneratedRegex(@"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""")]
    private static partial Regex AntiforgeryTokenPattern();

    // Loads the upload page to get a valid antiforgery token.
    // The client keeps the matching antiforgery cookie, which the server checks together with the token.
    async Task<string> GetAntiforgeryTokenAsync()
    {
        var html = await client.GetStringAsync("/");
        return AntiforgeryTokenPattern().Match(html).Groups[1].Value;
    }

    // Posts the file form as the browser does (multipart/form-data, token in a form field).
    async Task<HttpResponseMessage> PostFileAsync(byte[] fileData, string description = "", bool withToken = true)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileData);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        content.Add(new StringContent(description), "description");
        if (withToken)
            content.Add(new StringContent(await GetAntiforgeryTokenAsync()), "__RequestVerificationToken");
        return await client.PostAsync("/Images/Upload", content);
    }

    // Posts a camera snapshot as the page script does (form URL encoded, token in the RequestVerificationToken header).
    async Task<HttpResponseMessage> PostCameraImageAsync(string imageBase64, string description = "")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Images/UploadCameraImage") {
            Content = new FormUrlEncodedContent([
                new("imageBase64", imageBase64),
                new("description", description)
            ])
        };
        request.Headers.Add("RequestVerificationToken", await GetAntiforgeryTokenAsync());
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Index_ReturnsUploadPage()
    {
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Upload_SavesImageAndThumbnail()
    {
        var response = await PostFileAsync(TestImages.Create(400, 200), "file description");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var image = Assert.Single(factory.GetImages());
        Assert.Equal("file description", image.Description);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal((400, 200, 200, 100), (image.Width, image.Height, image.ThumbnailWidth, image.ThumbnailHeight));
    }

    [Fact]
    public async Task Upload_WithoutAntiforgeryToken_IsRejected()
    {
        var response = await PostFileAsync(TestImages.Create(10, 10), withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.GetImages());
    }

    [Fact]
    public async Task Upload_NonImage_ReturnsBadRequest()
    {
        var response = await PostFileAsync("not an image"u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid image.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_TooLargeFile_ReturnsBadRequest()
    {
        var response = await PostFileAsync(new byte[ImageProcessor.MaximumFileSize + 1]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Too large file.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_TooLongDescription_ReturnsBadRequest()
    {
        // One character over ImageRecord.DescriptionMaxLength (200).
        var response = await PostFileAsync(TestImages.Create(10, 10), new string('a', 201));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.GetImages());
    }

    [Fact]
    public async Task UploadCameraImage_SavesDescription()
    {
        var response = await PostCameraImageAsync(TestImages.Create(60, 40).ToDataUrl(), "camera description");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var image = Assert.Single(factory.GetImages());
        Assert.Equal("camera description", image.Description);
        Assert.Equal((150, 100), (image.ThumbnailWidth, image.ThumbnailHeight));
    }

    [Theory]
    [InlineData("data:image/png;base64,***")]
    [InlineData("data:image/png;base64,AQID")] // valid data URL, but the bytes are not an image
    [InlineData("garbage")]
    public async Task UploadCameraImage_InvalidData_ReturnsBadRequest(string imageBase64)
    {
        var response = await PostCameraImageAsync(imageBase64);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.GetImages());
    }

    [Fact]
    public async Task ThumbnailAndOriginal_ReturnStoredImages()
    {
        var original = TestImages.Create(400, 200);
        await PostFileAsync(original);
        var id = Assert.Single(factory.GetImages()).Id;

        var thumbnailResponse = await client.GetAsync($"/Images/Thumbnail/{id}");
        Assert.Equal("image/png", thumbnailResponse.Content.Headers.ContentType?.MediaType);

        var originalResponse = await client.GetAsync($"/Images/Original/{id}");
        Assert.Equal(original, await originalResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Index_ShowsOnlyThumbnailsAndLinksToOriginals()
    {
        await PostFileAsync(TestImages.Create(400, 200));
        var id = Assert.Single(factory.GetImages()).Id;

        var html = await client.GetStringAsync("/");

        // The list must not load the originals; they are only linked to.
        Assert.Contains($"src=\"/Images/Thumbnail/{id}\"", html);
        Assert.Contains($"href=\"/Images/Original/{id}\"", html);
        Assert.DoesNotContain($"src=\"/Images/Original/{id}\"", html);
    }

    // Posts the delete form of an image, optionally without the antiforgery token.
    async Task<HttpResponseMessage> PostDeleteAsync(int id, bool withToken = true)
    {
        var fields = new Dictionary<string, string>();
        if (withToken)
            fields["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync();
        using var content = new FormUrlEncodedContent(fields);
        return await client.PostAsync($"/Images/Delete/{id}?page=1", content);
    }

    [Fact]
    public async Task Index_ShowsDeleteFormForEachImage()
    {
        await PostFileAsync(TestImages.Create(10, 10));
        var id = Assert.Single(factory.GetImages()).Id;

        var html = await client.GetStringAsync("/");

        Assert.Contains($"action=\"/Images/Delete/{id}?page=1\"", html);
    }

    [Fact]
    public async Task Delete_RemovesImageAndThumbnail()
    {
        await PostFileAsync(TestImages.Create(10, 10));
        var id = Assert.Single(factory.GetImages()).Id;

        var response = await PostDeleteAsync(id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(factory.GetImages());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Images/Thumbnail/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Images/Original/{id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutAntiforgeryToken_IsRejected()
    {
        await PostFileAsync(TestImages.Create(10, 10));
        var id = Assert.Single(factory.GetImages()).Id;

        var response = await PostDeleteAsync(id, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(factory.GetImages());
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        var response = await PostDeleteAsync(12345);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Thumbnail_UnknownId_ReturnsNotFound()
    {
        var response = await client.GetAsync("/Images/Thumbnail/12345");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
