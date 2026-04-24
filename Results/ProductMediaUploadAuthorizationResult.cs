using Platform.ProductMediaUpload.Function.Enums;

namespace Platform.ProductMediaUpload.Function.Results;

public sealed class ProductMediaUploadAuthorizationResult
{
    public ProductMediaUploadAuthorizationStatus Status { get; private init; }
    public ProductMediaUploadVisibility? Visibility { get; private init; }
    public string? Error { get; private init; }

    public bool IsAllowed => Status == ProductMediaUploadAuthorizationStatus.Allowed;

    public static ProductMediaUploadAuthorizationResult Allowed(ProductMediaUploadVisibility visibility)
    {
        return new ProductMediaUploadAuthorizationResult
        {
            Status = ProductMediaUploadAuthorizationStatus.Allowed,
            Visibility = visibility
        };
    }

    public static ProductMediaUploadAuthorizationResult Denied(string error)
    {
        return new ProductMediaUploadAuthorizationResult
        {
            Status = ProductMediaUploadAuthorizationStatus.Denied,
            Error = error
        };
    }

    public static ProductMediaUploadAuthorizationResult Unavailable(string error)
    {
        return new ProductMediaUploadAuthorizationResult
        {
            Status = ProductMediaUploadAuthorizationStatus.Unavailable,
            Error = error
        };
    }
}
