using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaultShare.Application.Files;
using VaultShare.Infrastructure.Authorization;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/files")]
public sealed class FilesController(IFileService fileService, IInternalFileAccessService internalFileAccessService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FilePage>> List(
        [FromQuery] Guid workspaceId,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.View);
        if (!authorization.Succeeded) return Forbid();
        var result = await fileService.ListAsync(workspaceId, search, status, page, pageSize, User, cancellationToken);
        return result.Status == FileOperationStatus.Success && result.Value is not null
            ? Ok(result.Value) : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpGet("{fileId:guid}")]
    public async Task<ActionResult<FileSummary>> Get(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await fileService.GetAsync(fileId, User, cancellationToken);
        return result.Status == FileOperationStatus.Success && result.Value is not null
            ? Ok(result.Value) : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpDelete("{fileId:guid}")]
    public async Task<IActionResult> Delete(Guid fileId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var result = await fileService.DeleteAsync(fileId, idempotencyKey ?? string.Empty, User, cancellationToken);
        return result.Status == FileOperationStatus.Success ? NoContent() : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpPost("{fileId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid fileId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var result = await fileService.RestoreAsync(fileId, idempotencyKey ?? string.Empty, User, cancellationToken);
        return result.Status == FileOperationStatus.Success ? NoContent() : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpDelete("{fileId:guid}/purge")]
    public async Task<IActionResult> Purge(Guid fileId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var result = await fileService.PurgeAsync(fileId, idempotencyKey ?? string.Empty, User, cancellationToken);
        return result.Status == FileOperationStatus.Success ? NoContent() : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpPost("{fileId:guid}/internal-grants")]
    public async Task<ActionResult<InternalGrantSummary>> GrantInternalAccess(Guid fileId,
        InternalGrantRequest request, CancellationToken cancellationToken)
    {
        var result = await internalFileAccessService.GrantAsync(fileId, request.RecipientEmail,
            request.Permission, request.ExpiresAt, User, cancellationToken);
        return result.Status == FileOperationStatus.Success && result.Value is not null
            ? Created($"/api/v1/internal-file-grants/{result.Value.Id:D}", result.Value)
            : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpGet("{fileId:guid}/internal-grants")]
    public async Task<ActionResult<IReadOnlyList<InternalGrantSummary>>> ListInternalAccess(Guid fileId,
        CancellationToken cancellationToken)
    {
        var result = await internalFileAccessService.ListAsync(fileId, User, cancellationToken);
        return result.Status == FileOperationStatus.Success && result.Value is not null
            ? Ok(result.Value) : MapFailure(result.Status, result.ErrorCode);
    }

    private ObjectResult MapFailure(FileOperationStatus status, string code) => Problem(
        statusCode: status switch
        {
            FileOperationStatus.Forbidden => StatusCodes.Status403Forbidden,
            FileOperationStatus.NotFound => StatusCodes.Status404NotFound,
            FileOperationStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        }, title: "Operasi file tidak dapat diproses.",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    public sealed record InternalGrantRequest(
        [param: System.ComponentModel.DataAnnotations.Required,
         System.ComponentModel.DataAnnotations.EmailAddress,
         System.ComponentModel.DataAnnotations.StringLength(256)] string RecipientEmail,
        [param: System.ComponentModel.DataAnnotations.Required,
         System.ComponentModel.DataAnnotations.StringLength(16)] string Permission,
        DateTimeOffset? ExpiresAt);
}
