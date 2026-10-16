namespace ImageUploadSample.Models;

/// <summary>
/// The model of Error.cshtml.
/// </summary>
/// <param name="RequestId">The ID to find the failed request in the logs.</param>
public record ErrorViewModel(string? RequestId)
{
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
