using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaultShare.Application.Encryption;
using VaultShare.Application.Shares;
using VaultShare.Application.Storage;

namespace VaultShare.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/previews")]
public sealed class PreviewsController(IDownloadService downloadService, IObjectStorage objectStorage,
    IFileEncryptionService encryptionService) : ControllerBase
{
    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> Preview(Guid fileId, CancellationToken cancellationToken)
    {
        var token = Request.Cookies["VaultShare.ShareSession"] ?? string.Empty;
        var authorization = await downloadService.AuthorizePreviewAsync(fileId, token, cancellationToken);
        if (authorization.Status != ShareOperationStatus.Success || authorization.Value is null)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Preview tidak tersedia.",
                extensions: new Dictionary<string, object?> { ["code"] = "preview.access_denied" });
        var file = authorization.Value;
        Response.ContentType = file.DetectedMimeType == "text/plain" ? "text/plain; charset=utf-8" : file.DetectedMimeType;
        Response.ContentLength = file.Size;
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers["Content-Security-Policy"] = file.DetectedMimeType == "application/pdf"
            ? "sandbox; default-src 'none'; frame-ancestors 'none'"
            : "default-src 'none'; frame-ancestors 'none'";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = file.Filename }.ToString();
        try
        {
            await using var ciphertext = await objectStorage.GetStreamAsync(file.ObjectKey, cancellationToken);
            await encryptionService.DecryptAsync(file.FileId, ciphertext, Response.Body, file.EncryptionMetadata, cancellationToken);
            return new EmptyResult();
        }
        catch
        {
            if (Response.HasStarted)
            {
                HttpContext.Abort();
                return new EmptyResult();
            }
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Preview gagal diproses.",
                extensions: new Dictionary<string, object?> { ["code"] = "preview.processing_failed" });
        }
    }
}
