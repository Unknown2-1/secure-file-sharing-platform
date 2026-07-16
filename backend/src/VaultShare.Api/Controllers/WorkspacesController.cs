using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VaultShare.Application.Workspaces;
using VaultShare.Infrastructure.Authorization;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/workspaces")]
public sealed class WorkspacesController(
    IWorkspaceService workspaceService,
    IWorkspaceSettingService workspaceSettingService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceSummary>>> List(CancellationToken cancellationToken) =>
        Ok(await workspaceService.ListAsync(User, cancellationToken));

    [HttpGet("{workspaceId:guid}")]
    public async Task<ActionResult<WorkspaceSummary>> Get(Guid workspaceId, CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.View);
        if (!authorization.Succeeded)
        {
            return Forbid();
        }

        var workspace = await workspaceService.GetAsync(workspaceId, User, cancellationToken);
        return workspace is null ? NotFound() : Ok(workspace);
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceSummary>> Create(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceService.CreateAsync(request.Name, User, cancellationToken);
        return CreatedAtAction(nameof(Get), new { workspaceId = workspace.Id }, workspace);
    }

    [HttpGet("{workspaceId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceMemberSummary>>> ListMembers(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.View);
        return authorization.Succeeded
            ? Ok(await workspaceService.ListMembersAsync(workspaceId, User, cancellationToken))
            : Forbid();
    }

    [EnableRateLimiting("workspace-invitation")]
    [HttpPost("{workspaceId:guid}/invitations")]
    public async Task<ActionResult<WorkspaceInvitationCreated>> Invite(
        Guid workspaceId,
        InviteMemberRequest request,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.ManageMembers);
        if (!authorization.Succeeded) return Forbid();

        var result = await workspaceService.InviteAsync(
            workspaceId,
            request.Email,
            request.Role,
            request.ExpiresInHours,
            User,
            cancellationToken);
        if (result.Status == WorkspaceOperationStatus.Success && result.Value is not null)
            return Created($"/api/v1/workspace-invitations/{result.Value.Id:D}", result.Value);
        return MapFailure(result.Status, result.ErrorCode);
    }

    [HttpPatch("{workspaceId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> ChangeRole(
        Guid workspaceId,
        Guid userId,
        ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.ManageMembers);
        if (!authorization.Succeeded) return Forbid();
        var result = await workspaceService.ChangeMemberRoleAsync(workspaceId, userId, request.Role, User, cancellationToken);
        return result.Status == WorkspaceOperationStatus.Success ? NoContent() : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpDelete("{workspaceId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.ManageMembers);
        if (!authorization.Succeeded) return Forbid();
        var result = await workspaceService.RemoveMemberAsync(workspaceId, userId, User, cancellationToken);
        return result.Status == WorkspaceOperationStatus.Success ? NoContent() : MapFailure(result.Status, result.ErrorCode);
    }

    [HttpGet("{workspaceId:guid}/settings")]
    public async Task<ActionResult<WorkspaceSettingSummary>> GetSettings(Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.View);
        if (!authorization.Succeeded) return Forbid();
        var result = await workspaceSettingService.GetAsync(workspaceId, User, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{workspaceId:guid}/settings")]
    public async Task<ActionResult<WorkspaceSettingSummary>> UpdateSettings(Guid workspaceId,
        UpdateWorkspaceSettingRequest request, CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.ManageSecurity);
        if (!authorization.Succeeded) return Forbid();
        var result = await workspaceSettingService.UpdateAsync(workspaceId, request.StorageQuotaBytes,
            request.AuditRetentionDays, request.DeletedFileGraceDays, request.AllowMemberPublicShares,
            User, cancellationToken);
        return result is null ? Forbid() : Ok(result);
    }

    [HttpDelete("{workspaceId:guid}")]
    public async Task<IActionResult> DeleteWorkspace(Guid workspaceId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.ManageSecurity);
        if (!authorization.Succeeded) return Forbid();
        var result = await workspaceService.DeleteAsync(workspaceId, idempotencyKey ?? string.Empty,
            User, cancellationToken);
        return result.Status == WorkspaceOperationStatus.Success ? NoContent() : MapFailure(result.Status, result.ErrorCode);
    }

    private ObjectResult MapFailure(WorkspaceOperationStatus status, string code) => Problem(
        statusCode: status switch
        {
            WorkspaceOperationStatus.Forbidden => StatusCodes.Status403Forbidden,
            WorkspaceOperationStatus.NotFound => StatusCodes.Status404NotFound,
            WorkspaceOperationStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        },
        title: "Operasi workspace tidak dapat diproses.",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    public sealed record CreateWorkspaceRequest(
        [param: Required, StringLength(120, MinimumLength = 2)] string Name);

    public sealed record InviteMemberRequest(
        [param: Required, EmailAddress, StringLength(254)] string Email,
        [param: Required, StringLength(16)] string Role,
        [param: Range(1, 720)] int ExpiresInHours);

    public sealed record ChangeRoleRequest(
        [param: Required, StringLength(16)] string Role);

    public sealed record UpdateWorkspaceSettingRequest(
        [param: Range(1_048_576, 1_125_899_906_842_624)] long StorageQuotaBytes,
        [param: Range(30, 3650)] int AuditRetentionDays,
        [param: Range(0, 365)] int DeletedFileGraceDays,
        bool AllowMemberPublicShares);
}
