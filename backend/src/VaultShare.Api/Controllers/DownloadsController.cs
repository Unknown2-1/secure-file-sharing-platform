using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VaultShare.Application.Encryption;
using VaultShare.Application.Shares;
using VaultShare.Application.Storage;

namespace VaultShare.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/downloads")]
public sealed class DownloadsController(
    IDownloadService downloadService,
    IObjectStorage objectStorage,
    IFileEncryptionService encryptionService) : ControllerBase
{
    [EnableRateLimiting("public-download")]
    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> Download(Guid fileId,
        [FromHeader(Name = "X-Download-Session")] string? sessionToken,
        CancellationToken cancellationToken)
    {
        var effectiveToken = sessionToken ?? Request.Cookies["VaultShare.ShareSession"] ?? string.Empty;
        var reservation = await downloadService.ReserveAsync(fileId, effectiveToken, cancellationToken);
        if (reservation.Status != ShareOperationStatus.Success || reservation.Value is null)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Download tidak dapat dimulai.",
                extensions: new Dictionary<string, object?> { ["code"] = "download.access_denied" });

        var file = reservation.Value;
        Response.ContentType = file.DetectedMimeType;
        Response.ContentLength = file.Size;
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        var disposition = new ContentDispositionHeaderValue("attachment") { FileNameStar = file.Filename };
        Response.Headers.ContentDisposition = disposition.ToString();
        try
        {
            await using var ciphertext = await objectStorage.GetStreamAsync(file.ObjectKey, cancellationToken);
            await encryptionService.DecryptAsync(file.FileId, ciphertext, Response.Body,
                file.EncryptionMetadata, cancellationToken);
            await downloadService.CompleteAsync(file.DownloadId, true, cancellationToken);
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await downloadService.CompleteAsync(file.DownloadId, false, CancellationToken.None);
            throw;
        }
        catch
        {
            await downloadService.CompleteAsync(file.DownloadId, false, CancellationToken.None);
            if (Response.HasStarted)
            {
                HttpContext.Abort();
                return new EmptyResult();
            }
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Download gagal diproses.",
                extensions: new Dictionary<string, object?> { ["code"] = "download.processing_failed" });
        }
    }
}
