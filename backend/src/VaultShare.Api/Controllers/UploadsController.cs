using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VaultShare.Application.Uploads;
using VaultShare.Infrastructure.Authorization;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/uploads")]
public sealed class UploadsController(
    IUploadService uploadService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet("{uploadId:guid}")]
    public async Task<ActionResult<UploadSessionInfo>> Get(Guid uploadId, CancellationToken cancellationToken)
    {
        var result = await uploadService.GetAsync(uploadId, User, cancellationToken);
        return result.Status == UploadOperationStatus.Success && result.Value is not null
            ? Ok(result.Value)
            : MapFailure(result.Status, result.ErrorCode);
    }

    [EnableRateLimiting("upload-create")]
    [HttpPost]
    public async Task<ActionResult<UploadSessionInfo>> Create(
        CreateUploadRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, request.WorkspaceId, WorkspacePolicies.Upload);
        if (!authorization.Succeeded) return Forbid();
        var result = await uploadService.CreateAsync(new CreateUploadCommand(
            request.WorkspaceId,
            request.Filename,
            request.FileSize,
            request.ClientMimeType,
            idempotencyKey ?? string.Empty), User, cancellationToken);
        return result.Status == UploadOperationStatus.Success && result.Value is not null
            ? Created($"/api/v1/uploads/{result.Value.Id:D}", result.Value)
            : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpPatch("{uploadId:guid}")]
    [Consumes("application/offset+octet-stream")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Append(
        Guid uploadId,
        [FromHeader(Name = "Upload-Offset")] long? uploadOffset,
        CancellationToken cancellationToken)
    {
        if (uploadOffset is null || Request.ContentLength is null)
            return MapFailure(UploadOperationStatus.Invalid, "upload.headers_required");
        var result = await uploadService.AppendChunkAsync(uploadId, uploadOffset.Value,
            Request.ContentLength.Value, Request.Body, User, cancellationToken);
        if (result.Status != UploadOperationStatus.Success || result.Value is null)
            return MapFailure(result.Status, result.ErrorCode);
        Response.Headers["Upload-Offset"] = result.Value.UploadOffset.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return NoContent();
    }

    [HttpPost("{uploadId:guid}/finalize")]
    public async Task<ActionResult<UploadSessionInfo>> FinalizeUpload(
        Guid uploadId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await uploadService.FinalizeAsync(uploadId, idempotencyKey ?? string.Empty, User, cancellationToken);
        return result.Status == UploadOperationStatus.Success && result.Value is not null
            ? Ok(result.Value)
            : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpDelete("{uploadId:guid}")]
    public async Task<IActionResult> Cancel(
        Guid uploadId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await uploadService.CancelAsync(uploadId, idempotencyKey ?? string.Empty, User, cancellationToken);
        return result.Status == UploadOperationStatus.Success
            ? NoContent()
            : MapFailure(result.Status, result.ErrorCode);
    }

    private ObjectResult MapFailure(UploadOperationStatus status, string code) => Problem(
        statusCode: status switch
        {
            UploadOperationStatus.Forbidden => StatusCodes.Status403Forbidden,
            UploadOperationStatus.NotFound => StatusCodes.Status404NotFound,
            UploadOperationStatus.Conflict => StatusCodes.Status409Conflict,
            UploadOperationStatus.TooLarge => StatusCodes.Status413PayloadTooLarge,
            UploadOperationStatus.QuotaExceeded => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest,
        },
        title: "Upload tidak dapat diproses.",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    public sealed record CreateUploadRequest(
        Guid WorkspaceId,
        [param: Required, StringLength(255, MinimumLength = 1)] string Filename,
        [param: Range(1, long.MaxValue)] long FileSize,
        [param: Required, StringLength(127, MinimumLength = 1)] string ClientMimeType);
}
