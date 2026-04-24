namespace Platform.ProductMediaUpload.Function.Results;

public sealed class ProductMediaSetResult
{
    public bool IsSuccess { get; private init; }
    public bool IsUnavailable { get; private init; }
    public string? Error { get; private init; }

    public static ProductMediaSetResult Success() => new() { IsSuccess = true };
    public static ProductMediaSetResult Failure(string error) => new() { IsSuccess = false, Error = error };
    public static ProductMediaSetResult Unavailable(string error) => new() { IsSuccess = false, IsUnavailable = true, Error = error };
}
