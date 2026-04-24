namespace Platform.ProductMediaUpload.Function.Models;

public sealed class UploadProductMediaItemResult
{
    public string FileName { get; init; } = null!;
    public string BlobName { get; init; } = null!;
    public string ContainerName { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public long Size { get; init; }
    public string AltText { get; init; } = string.Empty;
}
