using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Application.Maintenance;
using VaultShare.Domain.Auditing;
using VaultShare.Domain.Shares;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.IntegrationTests;

public sealed class MaintenanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MaintenanceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Expired_share_notification_and_audit_are_idempotent()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = $"maintenance-{Guid.NewGuid():N}@example.com",
            Email = $"maintenance-{Guid.NewGuid():N}@example.com",
            DisplayName = "Maintenance Test",
            EmailConfirmed = true,
        };
        user.UserName = user.Email;
        Assert.True((await users.CreateAsync(user, "ValidPass123!")).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        var workspace = new VaultShare.Domain.Workspaces.Workspace(Guid.CreateVersion7(), "Maintenance",
            user.Id, DateTimeOffset.UtcNow.AddDays(-2));
        db.Workspaces.Add(workspace);
        db.WorkspaceMembers.Add(new VaultShare.Domain.Workspaces.WorkspaceMember(workspace.Id, user.Id,
            VaultShare.Domain.Workspaces.WorkspaceRole.Owner, DateTimeOffset.UtcNow.AddDays(-2)));
        db.WorkspaceSettings.Add(new WorkspaceSetting(workspace.Id, 10 * 1024 * 1024, 30, 0,
            true, DateTimeOffset.UtcNow, user.Id));
        db.AuditEvents.Add(new AuditEvent(Guid.CreateVersion7(), workspace.Id, user.Id,
            "OldEvent", "Workspace", workspace.Id.ToString("D"), DateTimeOffset.UtcNow.AddDays(-31),
            "redacted", "test", "old-event", "Success", "{}"));
        var share = new Share(Guid.CreateVersion7(), workspace.Id, user.Id, "expired-test",
            new string('a', 64), new string('b', 64), "Expired maintenance share", null,
            null, DateTimeOffset.UtcNow.AddHours(-1), null, false, false, DateTimeOffset.UtcNow.AddHours(-2));
        db.Shares.Add(share);
        await db.SaveChangesAsync();

        var maintenance = scope.ServiceProvider.GetRequiredService<IMaintenanceService>();
        var first = await maintenance.RunAsync(100, default);
        var second = await maintenance.RunAsync(100, default);

        Assert.Equal(1, first.ExpiredSharesNotified);
        Assert.Equal(1, first.AuditEventsDeleted);
        Assert.Equal(0, second.ExpiredSharesNotified);
        Assert.Equal(1, await db.Notifications.CountAsync(item => item.Type == "ShareExpired"));
        Assert.Equal(1, await db.AuditEvents.CountAsync(item => item.Action == "ShareExpired"));
        Assert.NotNull(await db.Shares.Where(item => item.Id == share.Id)
            .Select(item => item.ExpirationNotifiedAt).SingleAsync());
    }
}
