using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Authorization;

public static class WorkspacePolicies
{
    public const string View = "Workspace.View";

    public const string Upload = "Workspace.Upload";

    public const string ManageSecurity = "Workspace.ManageSecurity";

    public const string ManageMembers = "Workspace.ManageMembers";
}

public sealed record WorkspaceRoleRequirement(params WorkspaceRole[] AllowedRoles) : IAuthorizationRequirement;

internal sealed class WorkspaceRoleAuthorizationHandler(VaultShareDbContext dbContext)
    : AuthorizationHandler<WorkspaceRoleRequirement, Guid>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        WorkspaceRoleRequirement requirement,
        Guid workspaceId)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId) || workspaceId == Guid.Empty)
        {
            return;
        }

        var role = await (from member in dbContext.WorkspaceMembers
                          join workspace in dbContext.Workspaces on member.WorkspaceId equals workspace.Id
                          where member.WorkspaceId == workspaceId && member.UserId == userId &&
                                member.RemovedAt == null && workspace.DeletedAt == null
                          select (WorkspaceRole?)member.Role)
            .SingleOrDefaultAsync();

        if (role is not null && requirement.AllowedRoles.Contains(role.Value))
        {
            context.Succeed(requirement);
        }
    }
}
