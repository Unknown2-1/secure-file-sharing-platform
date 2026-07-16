using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VaultShare.Application.Shares;
using VaultShare.Infrastructure.Authorization;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/shares")]
public sealed class SharesController(IShareService shareService, IAuthorizationService authorizationService) : ControllerBase
{
    [EnableRateLimiting("share-create")]
    [HttpPost]
    public async Task<ActionResult<ShareCreated>> Create(CreateShareRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, request.WorkspaceId, WorkspacePolicies.Upload);
        if (!authorization.Succeeded) return Forbid();
        var result = await shareService.CreateAsync(new CreateShareCommand(request.WorkspaceId, request.FileIds,
            request.Name, request.Description, request.Password, request.StartsAt, request.ExpiresAt,
            request.MaximumDownloads, request.IsOneTime, request.AllowPreview, idempotencyKey ?? string.Empty),
            User, cancellationToken);
        return result.Status == ShareOperationStatus.Success && result.Value is not null
            ? Created($"/api/v1/shares/{result.Value.Id:D}", result.Value)
            : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShareSummary>>> List([FromQuery] Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.View);
        if (!authorization.Succeeded) return Forbid();
        var result = await shareService.ListAsync(workspaceId, User, cancellationToken);
        return result.Status == ShareOperationStatus.Success && result.Value is not null
            ? Ok(result.Value) : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpPost("{shareId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid shareId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var result = await shareService.RevokeAsync(shareId, idempotencyKey ?? string.Empty, User, cancellationToken);
        return result.Status == ShareOperationStatus.Success ? NoContent() : MapFailure(result.Status, result.ErrorCode);
    }

    private ObjectResult MapFailure(ShareOperationStatus status, string code) => Problem(
        statusCode: status switch
        {
            ShareOperationStatus.Forbidden => StatusCodes.Status403Forbidden,
            ShareOperationStatus.NotFound => StatusCodes.Status404NotFound,
            ShareOperationStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        }, title: "Operasi share tidak dapat diproses.", extensions: new Dictionary<string, object?> { ["code"] = code });

    public sealed record CreateShareRequest(Guid WorkspaceId,
        [param: MinLength(1), MaxLength(20)] IReadOnlyList<Guid> FileIds,
        [param: Required, StringLength(120, MinimumLength = 1)] string Name,
        [param: StringLength(1000)] string? Description,
        [param: StringLength(128, MinimumLength = 8)] string? Password,
        DateTimeOffset? StartsAt,
        DateTimeOffset ExpiresAt,
        [param: Range(1, 1_000_000)] int? MaximumDownloads,
        bool IsOneTime,
        bool AllowPreview);
}
