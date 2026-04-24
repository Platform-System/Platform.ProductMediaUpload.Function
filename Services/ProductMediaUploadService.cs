using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Platform.ProductMediaUpload.Function.Configurations;
using Platform.ProductMediaUpload.Function.Constants;
using Platform.ProductMediaUpload.Function.Enums;
using Platform.ProductMediaUpload.Function.Helpers;
using Platform.ProductMediaUpload.Function.Models;

namespace Platform.ProductMediaUpload.Function.Services;

public sealed class ProductMediaUploadService
{
    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public const int DefaultMaxFilesPerRequest = 10;

    private readonly BlobStorageOptions _blobStorageOptions;

    public ProductMediaUploadService(IOptions<BlobStorageOptions> blobStorageOptions)
    {
        _blobStorageOptions = blobStorageOptions.Value;
    }

    public async Task<UploadProductMediaResult> UploadAsync(
        Guid productId,
        IReadOnlyCollection<MultipartFileData> files,
        ProductMediaUploadVisibility visibility,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            throw new InvalidOperationException("At least one media file is required.");

        if (string.IsNullOrWhiteSpace(_blobStorageOptions.ConnectionString))
            throw new InvalidOperationException("Blob storage connection string is not configured.");

        var blobServiceClient = new BlobServiceClient(_blobStorageOptions.ConnectionString);
        var containerName = visibility == ProductMediaUploadVisibility.Public
            ? BlobContainerNames.ProductsPublic
            : BlobContainerNames.ProductsPrivate;
        var containerAccessType = visibility == ProductMediaUploadVisibility.Public
            ? PublicAccessType.Blob
            : PublicAccessType.None;
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        // Media của product dùng chung rule public/private với cover:
        // owner thường -> private, owner admin -> public.
        await containerClient.CreateIfNotExistsAsync(containerAccessType, cancellationToken: cancellationToken);
        await containerClient.SetAccessPolicyAsync(containerAccessType, cancellationToken: cancellationToken);

        var items = new List<UploadProductMediaItemResult>(files.Count);

        foreach (var file in files)
        {
            if (!AllowedContentTypes.Contains(file.ContentType))
                throw new InvalidOperationException("Only JPEG, PNG, and WEBP images are allowed.");

            if (!ImageSignatureValidator.IsValid(file))
                throw new InvalidOperationException("File content does not match a supported image format.");

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
                throw new InvalidOperationException("File extension is required.");

            var generatedFileName = $"{Guid.NewGuid():N}{extension}";
            var blobName = $"products/{productId}/medias/{generatedFileName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            // Mỗi file được upload vào nhánh medias của product để sau này
            // Catalog chỉ cần nhận metadata rồi lưu tiếp vào DB.
            await using (file.FileStream)
            {
                await blobClient.UploadAsync(
                    file.FileStream,
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = file.ContentType
                        }
                    },
                    cancellationToken);
            }

            items.Add(new UploadProductMediaItemResult
            {
                FileName = generatedFileName,
                BlobName = blobName,
                ContainerName = containerClient.Name,
                ContentType = file.ContentType,
                Size = file.FileSize,
                AltText = file.AltText
            });
        }

        return new UploadProductMediaResult
        {
            Items = items
        };
    }
}
