using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VaultShare.Application.Notifications;
using VaultShare.Application.Workspaces;
using VaultShare.Domain.Auditing;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Workspaces;

internal sealed class WorkspaceService(
    VaultShareDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ILookupNormalizer normalizer,
    IEmailService emailService,
    ILogger<WorkspaceService> logger) : IWorkspaceService
{
    public async Task<IReadOnlyList<WorkspaceSummary>> ListAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(principal);
        return await (
            from member in dbContext.WorkspaceMembers
            join workspace in dbContext.Workspaces on member.WorkspaceId equals workspace.Id
            where member.UserId == user.Id && member.RemovedAt == null && workspace.DeletedAt == null
            orderby workspace.CreatedAt descending
            select new WorkspaceSummary(workspace.Id, workspace.Name, member.Role.ToString(), workspace.CreatedAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkspaceSummary?> GetAsync(
        Guid workspaceId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(principal);
        return await (
            from member in dbContext.WorkspaceMembers
            join workspace in dbContext.Workspaces on member.WorkspaceId equals workspace.Id
            where member.UserId == user.Id &&
                  member.WorkspaceId == workspaceId &&
                  member.RemovedAt == null &&
                  workspace.DeletedAt == null
            select new WorkspaceSummary(workspace.Id, workspace.Name, member.Role.ToString(), workspace.CreatedAt))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkspaceSummary> CreateAsync(
        string name,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(principal);
        var now = DateTimeOffset.UtcNow;
        var workspace = new Workspace(Guid.CreateVersion7(), name, user.Id, now);
        dbContext.Workspaces.Add(workspace);
        dbContext.WorkspaceMembers.Add(new WorkspaceMember(workspace.Id, user.Id, WorkspaceRole.Owner, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new WorkspaceSummary(workspace.Id, workspace.Name, WorkspaceRole.Owner.ToString(), workspace.CreatedAt);
    }

    public async Task<IReadOnlyList<WorkspaceMemberSummary>> ListMembersAsync(
        Guid workspaceId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        _ = await RequireMembershipAsync(workspaceId, principal, cancellationToken);
        return await (
            from member in dbContext.WorkspaceMembers
            join user in dbContext.Users on member.UserId equals user.Id
            where member.WorkspaceId == workspaceId && member.RemovedAt == null && user.DeletedAt == null
            orderby member.Role, user.DisplayName
            select new WorkspaceMemberSummary(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                member.Role.ToString(),
                member.JoinedAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkspaceOperationResult<WorkspaceInvitationCreated>> InviteAsync(
        Guid workspaceId,
        string email,
        string role,
        int expiresInHours,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actor = await RequireMembershipAsync(workspaceId, principal, cancellationToken);
        if (!Enum.TryParse<WorkspaceRole>(role, true, out var requestedRole) ||
            !Enum.IsDefined(requestedRole) ||
            requestedRole == WorkspaceRole.Owner ||
            expiresInHours is < 1 or > 720)
        {
            return WorkspaceOperationResult<WorkspaceInvitationCreated>.Failure(
                WorkspaceOperationStatus.Invalid,
                "workspace.invitation_invalid");
        }

        if (actor.Role is WorkspaceRole.Member or WorkspaceRole.Viewer ||
            (actor.Role == WorkspaceRole.Admin && requestedRole == WorkspaceRole.Admin))
        {
            return WorkspaceOperationResult<WorkspaceInvitationCreated>.Failure(
                WorkspaceOperationStatus.Forbidden,
                "workspace.invitation_forbidden");
        }

        var normalizedEmail = normalizer.NormalizeEmail(email.Trim());
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return WorkspaceOperationResult<WorkspaceInvitationCreated>.Failure(
                WorkspaceOperationStatus.Invalid,
                "workspace.invitation_invalid");
        }

        var now = DateTimeOffset.UtcNow;
        var previousInvitations = await dbContext.WorkspaceInvitations
            .Where(invitation => invitation.WorkspaceId == workspaceId &&
                                 invitation.NormalizedEmail == normalizedEmail &&
                                 invitation.AcceptedAt == null &&
                                 invitation.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var previous in previousInvitations) previous.Revoke(now);

        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var invitation = new WorkspaceInvitation(
            Guid.CreateVersion7(),
            workspaceId,
            normalizedEmail,
            requestedRole,
            tokenHash,
            actor.UserId,
            now,
            now.AddHours(expiresInHours));
        dbContext.WorkspaceInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var safeToken = HtmlEncoder.Default.Encode(rawToken);
            await emailService.SendAsync(new EmailMessage(
                email.Trim(),
                "Undangan workspace VaultShare",
                $"Anda diundang sebagai {requestedRole} ke workspace VaultShare. Masukkan kode undangan berikut di aplikasi:\n\n{rawToken}\n\nKode berakhir pada {invitation.ExpiresAt:O}.",
                $"<p>Anda diundang sebagai <strong>{requestedRole}</strong> ke workspace VaultShare.</p><p>Masukkan kode undangan berikut di aplikasi:</p><p><code>{safeToken}</code></p><p>Kode berakhir pada {invitation.ExpiresAt:O}.</p>"),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Workspace invitation delivery failed for invitation {InvitationId}", invitation.Id);
        }

        return WorkspaceOperationResult<WorkspaceInvitationCreated>.Success(
            new WorkspaceInvitationCreated(invitation.Id, rawToken, invitation.ExpiresAt));
    }

    public async Task<WorkspaceOperationResult<bool>> AcceptInvitationAsync(
        Guid invitationId,
        string secretToken,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(principal);
        var invitation = await dbContext.WorkspaceInvitations.SingleOrDefaultAsync(
            candidate => candidate.Id == invitationId,
            cancellationToken);
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(secretToken));
        if (invitation is null ||
            !CryptographicOperations.FixedTimeEquals(suppliedHash, Convert.FromHexString(invitation.SecretTokenHash)) ||
            invitation.NormalizedEmail != normalizer.NormalizeEmail(user.Email ?? string.Empty) ||
            invitation.RevokedAt is not null ||
            invitation.AcceptedAt is not null ||
            invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return WorkspaceOperationResult<bool>.Failure(
                WorkspaceOperationStatus.Invalid,
                "workspace.invitation_invalid");
        }

        var now = DateTimeOffset.UtcNow;
        var membership = await dbContext.WorkspaceMembers.FindAsync(
            [invitation.WorkspaceId, user.Id],
            cancellationToken);
        if (membership is null)
        {
            dbContext.WorkspaceMembers.Add(new WorkspaceMember(invitation.WorkspaceId, user.Id, invitation.Role, now));
        }
        else if (membership.RemovedAt is not null)
        {
            membership.Restore(invitation.Role, now);
        }
        else
        {
            return WorkspaceOperationResult<bool>.Failure(
                WorkspaceOperationStatus.Conflict,
                "workspace.member_exists");
        }

        invitation.Accept(user.Id, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return WorkspaceOperationResult<bool>.Success(true);
    }

    public async Task<WorkspaceOperationResult<bool>> ChangeMemberRoleAsync(
        Guid workspaceId,
        Guid targetUserId,
        string role,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actor = await RequireMembershipAsync(workspaceId, principal, cancellationToken);
        var target = await dbContext.WorkspaceMembers.SingleOrDefaultAsync(
            member => member.WorkspaceId == workspaceId && member.UserId == targetUserId && member.RemovedAt == null,
            cancellationToken);
        if (target is null) return WorkspaceOperationResult<bool>.Failure(WorkspaceOperationStatus.NotFound, "workspace.member_not_found");
        if (!Enum.TryParse<WorkspaceRole>(role, true, out var requestedRole) || !Enum.IsDefined(requestedRole) || requestedRole == WorkspaceRole.Owner)
            return WorkspaceOperationResult<bool>.Failure(WorkspaceOperationStatus.Invalid, "workspace.role_invalid");
        if (target.Role == WorkspaceRole.Owner)
            return WorkspaceOperationResult<bool>.Failure(WorkspaceOperationStatus.Conflict, "workspace.owner_immutable");
        if (actor.Role is WorkspaceRole.Member or WorkspaceRole.Viewer ||
            (actor.Role == WorkspaceRole.Admin && (target.Role == WorkspaceRole.Admin || requestedRole == WorkspaceRole.Admin)))
            return WorkspaceOperationResult<bool>.Failure(WorkspaceOperationStatus.Forbidden, "workspace.role_forbidden");

        target.ChangeRole(requestedRole);
        await dbContext.SaveChangesAsync(cancellationToken);
        return WorkspaceOperationResult<bool>.Success(true);
    }

    public async Task<WorkspaceOperationResult<bool>> RemoveMemberAsync(
        Guid workspaceId,
        Guid targetUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actor = await RequireMembershipAsync(workspaceId, principal, cancellationToken);
        var target = await dbContext.WorkspaceMembers.SingleOrDefaultAsync(
            member => member.WorkspaceId == workspaceId && member.UserId == targetUserId && member.RemovedAt == null,
            cancellationToken);
        if (target is null) return WorkspaceOperationResult<bool>.Failure(WorkspaceOperationStatus.NotFound, "workspace.member_not_found");
        if (target.Role == WorkspaceRole.Owner)
            return WorkspaceOperationResult<bool>.Failure(WorkspaceOperationStatus.Conflict, "workspace.owner_immutable");
        if (actor.Role is WorkspaceRole.Member or WorkspaceRole.Viewer ||
            (actor.Role == WorkspaceRole.Admin && target.Role == WorkspaceRole.Admin))
            return WorkspaceOperationResult<bool>.Failure(WorkspaceOperationStatus.Forbidden, "workspace.member_removal_forbidden");

        target.Remove(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return WorkspaceOperationResult<bool>.Success(true);
    }

    public async Task<WorkspaceOperationResult<bool>> DeleteAsync(Guid workspaceId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (idempotencyKey.Length is < 8 or > 128 || idempotencyKey.Any(character => character is < '!' or > '~'))
            return WorkspaceOperationResult<bool>.Failure(WorkspaceOperationStatus.Invalid, "workspace.invalid_idempotency_key");
        var user = await RequireUserAsync(principal);
        var membership = await dbContext.WorkspaceMembers.AsNoTracking().SingleOrDefaultAsync(member =>
            member.WorkspaceId == workspaceId && member.UserId == user.Id && member.RemovedAt == null,
            cancellationToken);
        if (membership?.Role != WorkspaceRole.Owner)
            return WorkspaceOperationResult<bool>.Failure(WorkspaceOperationStatus.Forbidden, "workspace.delete_forbidden");
        var workspace = await dbContext.Workspaces.SingleOrDefaultAsync(item => item.Id == workspaceId, cancellationToken);
        if (workspace is null) return WorkspaceOperationResult<bool>.Failure(WorkspaceOperationStatus.NotFound, "workspace.not_found");
        if (workspace.DeletedAt is not null) return WorkspaceOperationResult<bool>.Success(true);
        var now = DateTimeOffset.UtcNow;
        var shares = await dbContext.Shares.Where(share => share.WorkspaceId == workspaceId && !share.IsRevoked)
            .ToListAsync(cancellationToken);
        foreach (var share in shares) share.Revoke(user.Id, now);
        var files = await dbContext.StoredFiles.Where(file => file.WorkspaceId == workspaceId &&
            file.PurgedAt == null && file.DeletedAt == null).ToListAsync(cancellationToken);
        foreach (var file in files) file.SoftDelete(now);
        workspace.Delete(now);
        dbContext.AuditEvents.Add(new AuditEvent(Guid.CreateVersion7(), workspaceId, user.Id,
            "WorkspaceDeleted", "Workspace", workspaceId.ToString("D"), now, "redacted",
            "VaultShare.Api", "workspace-delete", "Success", "{}"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return WorkspaceOperationResult<bool>.Success(true);
    }

    private async Task<WorkspaceMember> RequireMembershipAsync(
        Guid workspaceId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(principal);
        return await dbContext.WorkspaceMembers.SingleOrDefaultAsync(
                   member => member.WorkspaceId == workspaceId && member.UserId == user.Id && member.RemovedAt == null,
                   cancellationToken)
               ?? throw new UnauthorizedAccessException();
    }

    private async Task<ApplicationUser> RequireUserAsync(ClaimsPrincipal principal) =>
        await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
}
