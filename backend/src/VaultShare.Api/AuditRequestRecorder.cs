using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VaultShare.Domain.Auditing;
using VaultShare.Infrastructure.Persistence;

internal static class AuditRequestRecorder
{
    public static async Task RecordAsync(HttpContext context, IServiceProvider services, IConfiguration configuration,
        IHostEnvironment environment, CancellationToken cancellationToken)
    {
        var action = GetAction(context.Request.Method, context.Request.Path);
        if (action is null) return;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        var (targetType, targetId) = GetTarget(context);
        var workspaceId = await ResolveWorkspaceIdAsync(db, targetType, targetId, context, cancellationToken);
        var actorId = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedActor) ? parsedActor : (Guid?)null;
        var address = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var key = environment.IsEnvironment("Testing")
            ? SHA256.HashData("vaultshare-test-only-ip-key"u8.ToArray())
            : Convert.FromBase64String(configuration["PRIVACY_IP_HASH_KEY"]!);
        using var hmac = new HMACSHA256(key);
        var ipHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(address)));
        CryptographicOperations.ZeroMemory(key);
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var result = context.Response.StatusCode < 400 ? "Succeeded" : context.Response.StatusCode is 401 or 403 ? "Denied" : "Failed";
        db.AuditEvents.Add(new AuditEvent(Guid.CreateVersion7(), workspaceId, actorId, action, targetType,
            targetId, DateTimeOffset.UtcNow, ipHash, userAgent[..Math.Min(userAgent.Length, 256)],
            context.TraceIdentifier[..Math.Min(context.TraceIdentifier.Length, 64)], result, "{}"));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? GetAction(string method, PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.StartsWith("/api/v1/public/shares/access", StringComparison.Ordinal)) return "ShareAccessAttempted";
        if (method == "GET" && value.StartsWith("/api/v1/downloads/", StringComparison.Ordinal)) return "DownloadCompleted";
        if (method is not ("POST" or "PATCH" or "DELETE")) return null;
        if (value.Contains("/auth/register", StringComparison.Ordinal)) return "UserRegistered";
        if (value.Contains("/auth/login", StringComparison.Ordinal)) return "LoginAttempted";
        if (value.Contains("/auth/logout", StringComparison.Ordinal)) return "UserLoggedOut";
        if (value.Contains("/uploads", StringComparison.Ordinal)) return method == "DELETE" ? "UploadCancelled" : "UploadChanged";
        if (value.Contains("/shares", StringComparison.Ordinal)) return value.EndsWith("/revoke", StringComparison.Ordinal) ? "ShareRevoked" : "ShareChanged";
        if (value.Contains("/files", StringComparison.Ordinal)) return value.EndsWith("/purge", StringComparison.Ordinal) ? "FilePurged" : "FileChanged";
        if (value.Contains("/workspaces", StringComparison.Ordinal)) return "WorkspaceChanged";
        if (value.Contains("/security", StringComparison.Ordinal)) return "SecuritySettingChanged";
        if (value.Contains("/sessions", StringComparison.Ordinal)) return "SessionChanged";
        return null;
    }

    private static (string Type, string? Id) GetTarget(HttpContext context)
    {
        foreach (var pair in new[] { ("fileId", "File"), ("shareId", "Share"), ("uploadId", "Upload"), ("workspaceId", "Workspace"), ("sessionId", "Session") })
            if (context.Request.RouteValues.TryGetValue(pair.Item1, out var value)) return (pair.Item2, value?.ToString());
        var location = context.Response.Headers.Location.ToString();
        var lastSegment = location.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (Guid.TryParse(lastSegment, out var createdId))
        {
            if (context.Request.Path.StartsWithSegments("/api/v1/shares")) return ("Share", createdId.ToString("D"));
            if (context.Request.Path.StartsWithSegments("/api/v1/uploads")) return ("Upload", createdId.ToString("D"));
            if (context.Request.Path.StartsWithSegments("/api/v1/workspaces")) return ("Workspace", createdId.ToString("D"));
        }
        return ("Request", null);
    }

    private static async Task<Guid?> ResolveWorkspaceIdAsync(VaultShareDbContext db, string targetType,
        string? targetId, HttpContext context, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(context.Request.RouteValues["workspaceId"]?.ToString(), out var routeWorkspace)) return routeWorkspace;
        if (Guid.TryParse(context.Request.Query["workspaceId"], out var queryWorkspace)) return queryWorkspace;
        if (!Guid.TryParse(targetId, out var id)) return null;
        return targetType switch
        {
            "File" => await db.StoredFiles.Where(file => file.Id == id).Select(file => (Guid?)file.WorkspaceId).SingleOrDefaultAsync(cancellationToken),
            "Share" => await db.Shares.Where(share => share.Id == id).Select(share => (Guid?)share.WorkspaceId).SingleOrDefaultAsync(cancellationToken),
            "Upload" => await db.FileUploads.Where(upload => upload.Id == id).Select(upload => (Guid?)upload.WorkspaceId).SingleOrDefaultAsync(cancellationToken),
            _ => null,
        };
    }
}
