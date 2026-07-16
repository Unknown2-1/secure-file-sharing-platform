using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaultShare.Application.Encryption;
using VaultShare.Application.Files;
using VaultShare.Application.Storage;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
public sealed class InternalFilesController(
    IInternalFileAccessService internalFileAccessService,
    IObjectStorage objectStorage,
    IFileEncryptionService encryptionService) : ControllerBase
{
    [HttpDelete("api/v1/internal-file-grants/{grantId:guid}")]
    public async Task<IActionResult> Revoke(Guid grantId, CancellationToken cancellationToken)
    {
        var result = await internalFileAccessService.RevokeAsync(grantId, User, cancellationToken);
        return result.Status == FileOperationStatus.Success
            ? NoContent()
            : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpGet("api/v1/internal-files/{fileId:guid}/download")]
    public async Task<IActionResult> Download(Guid fileId, CancellationToken cancellationToken)
    {
        var authorization = await internalFileAccessService.AuthorizeDownloadAsync(fileId, User, cancellationToken);
        if (authorization.Status != FileOperationStatus.Success || authorization.Value is null)
            return MapFailure(authorization.Status, authorization.ErrorCode);

        var file = authorization.Value;
        Response.ContentType = file.DetectedMimeType;
        Response.ContentLength = file.Size;
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = file.Filename,
        }.ToString();
        try
        {
            await using var ciphertext = await objectStorage.GetStreamAsync(file.ObjectKey, cancellationToken);
            await encryptionService.DecryptAsync(file.FileId, ciphertext, Response.Body,
                file.EncryptionMetadata, cancellationToken);
            return new EmptyResult();
        }
        catch
        {
            if (Response.HasStarted)
            {
                HttpContext.Abort();
                return new EmptyResult();
            }

            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Download internal gagal diproses.",
                extensions: new Dictionary<string, object?> { ["code"] = "download.processing_failed" });
        }
    }

    [HttpGet("api/v1/internal-files/{fileId:guid}/preview")]
    public async Task<IActionResult> Preview(Guid fileId, CancellationToken cancellationToken)
    {
        var authorization = await internalFileAccessService.AuthorizePreviewAsync(fileId, User, cancellationToken);
        if (authorization.Status != FileOperationStatus.Success || authorization.Value is null)
            return MapFailure(authorization.Status, authorization.ErrorCode);
        var file = authorization.Value;
        Response.ContentType = file.DetectedMimeType == "text/plain" ? "text/plain; charset=utf-8" : file.DetectedMimeType;
        Response.ContentLength = file.Size;
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers["Content-Security-Policy"] = file.DetectedMimeType == "application/pdf"
            ? "sandbox; default-src 'none'; frame-ancestors 'none'"
            : "default-src 'none'; frame-ancestors 'none'";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = file.Filename,
        }.ToString();
        try
        {
            await using var ciphertext = await objectStorage.GetStreamAsync(file.ObjectKey, cancellationToken);
            await encryptionService.DecryptAsync(file.FileId, ciphertext, Response.Body,
                file.EncryptionMetadata, cancellationToken);
            return new EmptyResult();
        }
        catch
        {
            if (Response.HasStarted) { HttpContext.Abort(); return new EmptyResult(); }
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Preview internal gagal diproses.",
                extensions: new Dictionary<string, object?> { ["code"] = "preview.processing_failed" });
        }
    }

    private ObjectResult MapFailure(FileOperationStatus status, string code) => Problem(
        statusCode: status switch
        {
            FileOperationStatus.Forbidden => StatusCodes.Status403Forbidden,
            FileOperationStatus.NotFound => StatusCodes.Status404NotFound,
            FileOperationStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        }, title: "Akses file internal tidak dapat diproses.",
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
