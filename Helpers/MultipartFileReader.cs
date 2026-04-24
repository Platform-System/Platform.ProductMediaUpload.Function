using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Net.Http.Headers;
using Platform.ProductMediaUpload.Function.Models;

namespace Platform.ProductMediaUpload.Function.Helpers;

public static class MultipartFileReader
{
    public static async Task<(IReadOnlyList<MultipartFileData> Files, string? Error)> ReadMultipleFilesAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        // Media upload vẫn dùng multipart/form-data giống cover,
        // nhưng ở đây mình đọc nhiều file thay vì ép đúng 1 file.
        if (!request.Headers.TryGetValues("Content-Type", out var contentTypes))
            return ([], "Request must be multipart/form-data.");

        var contentType = contentTypes.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(contentType) ||
            !contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return ([], "Request must be multipart/form-data.");

        var boundary = HttpRequestHelpers.ExtractMultipartBoundary(contentType);
        if (string.IsNullOrWhiteSpace(boundary))
            return ([], "Missing multipart boundary.");

        // MultipartReader sẽ tách request body thành từng section nhỏ.
        // Function chỉ lấy những section thực sự là file upload.
        var reader = new MultipartReader(boundary, request.Body);
        var files = new List<MultipartFileData>();
        string? altText = null;

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(cancellationToken)) is not null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
                continue;

            if (disposition?.DispositionType == "form-data" && string.IsNullOrWhiteSpace(disposition.FileName.Value))
            {
                var fieldName = disposition.Name.Value?.Trim('"');
                if (string.Equals(fieldName, "altText", StringComparison.OrdinalIgnoreCase))
                {
                    using var textReader = new StreamReader(section.Body);
                    altText = await textReader.ReadToEndAsync(cancellationToken);
                }

                continue;
            }

            if (disposition?.DispositionType != "form-data" || string.IsNullOrWhiteSpace(disposition.FileName.Value))
                continue;

            var fileStream = new MemoryStream();
            await section.Body.CopyToAsync(fileStream, cancellationToken);
            fileStream.Position = 0;

            files.Add(new MultipartFileData
            {
                FileName = disposition.FileName.Value ?? disposition.FileNameStar.Value ?? string.Empty,
                ContentType = section.ContentType ?? "application/octet-stream",
                FileSize = fileStream.Length,
                FileStream = fileStream
            });
        }

        if (files.Count == 0)
            return ([], "At least one file is required.");

        foreach (var file in files)
        {
            file.AltText = altText ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(altText))
            return ([], "Alt text is required.");

        return (files, null);
    }
}
