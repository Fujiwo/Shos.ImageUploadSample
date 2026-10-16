using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using ImageUploadSample.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImageUploadSample.Tests;

public partial class ImagesControllerTests : IDisposable
{
    readonly TestApplicationFactory factory = new();
    readonly HttpClient client;

    public ImagesControllerTests()
        => client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""")]
    private static partial Regex AntiforgeryTokenPattern();

    async Task<string> GetAntiforgeryTokenAsync()
    {
        var html = await client.GetStringAsync("/");
        return AntiforgeryTokenPattern().Match(html).Groups[1].Value;
    }

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
    [InlineData("data:image/png;base64,AQID")]
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

        Assert.Contains($"<img src=\"/Images/Thumbnail/{id}\"", html);
        Assert.Contains($"href=\"/Images/Original/{id}\"", html);
        Assert.DoesNotContain($"src=\"/Images/Original/{id}\"", html);
    }

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
