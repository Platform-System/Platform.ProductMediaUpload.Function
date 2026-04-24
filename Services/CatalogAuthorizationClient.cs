using Grpc.Core;
using Platform.Catalog.Grpc;
using Platform.ProductMediaUpload.Function.Enums;
using Platform.ProductMediaUpload.Function.Results;

namespace Platform.ProductMediaUpload.Function.Services;

public sealed class CatalogAuthorizationClient
{
    private readonly CatalogIntegration.CatalogIntegrationClient _client;

    public CatalogAuthorizationClient(CatalogIntegration.CatalogIntegrationClient client)
    {
        _client = client;
    }

    public async Task<ProductMediaUploadAuthorizationResult> AuthorizeProductMediaUploadAsync(
        Guid productId,
        Guid userId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.AuthorizeProductCoverUploadAsync(
                new AuthorizeProductCoverUploadRequest
                {
                    ProductId = productId.ToString(),
                    UserId = userId.ToString(),
                    Roles = { roles }
                },
                cancellationToken: cancellationToken);

            if (!response.Status.IsSuccess)
                return ProductMediaUploadAuthorizationResult.Denied(response.Status.Errors.FirstOrDefault() ?? "Upload is not allowed.");

            return response.Data.Visibility switch
            {
                Platform.Catalog.Grpc.ProductCoverUploadVisibility.Public =>
                    ProductMediaUploadAuthorizationResult.Allowed(Enums.ProductMediaUploadVisibility.Public),
                Platform.Catalog.Grpc.ProductCoverUploadVisibility.Private =>
                    ProductMediaUploadAuthorizationResult.Allowed(Enums.ProductMediaUploadVisibility.Private),
                _ => ProductMediaUploadAuthorizationResult.Denied("Upload visibility is invalid.")
            };
        }
        catch (RpcException)
        {
            return ProductMediaUploadAuthorizationResult.Unavailable("Catalog service is unavailable.");
        }
    }
}
