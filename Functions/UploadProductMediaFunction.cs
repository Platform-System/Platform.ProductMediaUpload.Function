using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Platform.ProductMediaUpload.Function.Configurations;
using Platform.ProductMediaUpload.Function.Enums;
using Platform.ProductMediaUpload.Function.Helpers;
using Platform.ProductMediaUpload.Function.Services;
using System.Net;

namespace Platform.ProductMediaUpload.Function.Functions;

public sealed class UploadProductMediaFunction
{
    private readonly ProductMediaUploadService _productMediaUploadService;
    private readonly BlobStorageOptions _blobStorageOptions;
    private readonly JwtTokenValidator _jwtTokenValidator;
    private readonly CatalogAuthorizationClient _catalogAuthorizationClient;

    public UploadProductMediaFunction(
        ProductMediaUploadService productMediaUploadService,
        IOptions<BlobStorageOptions> blobStorageOptions,
        JwtTokenValidator jwtTokenValidator,
        CatalogAuthorizationClient catalogAuthorizationClient)
    {
        _productMediaUploadService = productMediaUploadService;
        _blobStorageOptions = blobStorageOptions.Value;
        _jwtTokenValidator = jwtTokenValidator;
        _catalogAuthorizationClient = catalogAuthorizationClient;
    }

    [Function(nameof(UploadProductMediaFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "products/{productId:guid}/medias")]
        HttpRequestData request,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var token = request.GetBearerToken();
        if (token is null)
        {
            var unauthorized = request.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteStringAsync("Unauthorized.", cancellationToken);
            return unauthorized;
        }

        var validation = await _jwtTokenValidator.ValidateAsync(token, cancellationToken);
        if (!validation.IsValid || validation.UserId is null)
        {
            var unauthorized = request.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteStringAsync("Unauthorized.", cancellationToken);
            return unauthorized;
        }

        var authorization = await _catalogAuthorizationClient.AuthorizeProductMediaUploadAsync(
            productId,
            validation.UserId.Value,
            validation.Roles,
            cancellationToken);

        if (!authorization.IsAllowed)
        {
            var statusCode = authorization.Status == ProductMediaUploadAuthorizationStatus.Unavailable
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.Forbidden;

            var denied = request.CreateResponse(statusCode);
            await denied.WriteStringAsync(authorization.Error ?? "Forbidden.", cancellationToken);
            return denied;
        }

        var (files, readError) = await MultipartFileReader.ReadMultipleFilesAsync(request, cancellationToken);
        if (readError is not null || files.Count == 0)
            return await HttpRequestHelpers.CreateBadRequestAsync(request, readError!, cancellationToken);

        // Chặn request gửi quá nhiều ảnh trong một lần để function đỡ bị phình body.
        if (files.Count > ProductMediaUploadService.DefaultMaxFilesPerRequest)
        {
            return await HttpRequestHelpers.CreateBadRequestAsync(
                request,
                $"A maximum of {ProductMediaUploadService.DefaultMaxFilesPerRequest} media files is allowed per request.",
                cancellationToken);
        }

        var maxFileSizeInBytes = (long)_blobStorageOptions.MaxFileSizeInMb * 1024 * 1024;
        foreach (var file in files)
        {
            if (file.FileSize == 0)
                return await HttpRequestHelpers.CreateBadRequestAsync(request, "One of the files is empty.", cancellationToken);

            if (file.FileSize > maxFileSizeInBytes)
            {
                return await HttpRequestHelpers.CreateBadRequestAsync(
                    request,
                    $"Each file size must not exceed {_blobStorageOptions.MaxFileSizeInMb} MB.",
                    cancellationToken);
            }
        }

        try
        {
            // Nếu qua hết bước auth + validate file thì mới upload thật lên Blob Storage.
            var result = await _productMediaUploadService.UploadAsync(
                productId,
                files,
                authorization.Visibility!.Value,
                cancellationToken);

            var okResponse = request.CreateResponse(HttpStatusCode.OK);
            await okResponse.WriteAsJsonAsync(result, cancellationToken);
            return okResponse;
        }
        catch (InvalidOperationException ex)
        {
            return await HttpRequestHelpers.CreateBadRequestAsync(request, ex.Message, cancellationToken);
        }
        catch (Exception)
        {
            var internalError = request.CreateResponse(HttpStatusCode.InternalServerError);
            await internalError.WriteStringAsync("Unable to upload product media files.", cancellationToken);
            return internalError;
        }
    }
}
