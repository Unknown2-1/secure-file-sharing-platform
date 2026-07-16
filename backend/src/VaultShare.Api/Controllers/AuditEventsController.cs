using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaultShare.Application.Auditing;
using VaultShare.Infrastructure.Authorization;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/audit-events")]
public sealed class AuditEventsController(IAuditQueryService auditQuery, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AuditPage>> List(Guid workspaceId, int page = 1, int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!(await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.ManageSecurity)).Succeeded) return Forbid();
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest();
        return Ok(await auditQuery.ListAsync(workspaceId, page, pageSize, cancellationToken));
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> Export(Guid workspaceId, DateTimeOffset? from, DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (!(await authorizationService.AuthorizeAsync(User, workspaceId, WorkspacePolicies.ManageSecurity)).Succeeded) return Forbid();
        if (from >= to) return BadRequest();
        var events = await auditQuery.ExportAsync(workspaceId, from, to, cancellationToken);
        var csv = new StringBuilder("timestamp,action,target_type,target_id,result,actor_user_id,correlation_id\r\n");
        foreach (var item in events)
            csv.AppendLine(string.Join(',', Csv(item.Timestamp.ToString("O")), Csv(item.Action), Csv(item.TargetType),
                Csv(item.TargetId), Csv(item.Result), Csv(item.ActorUserId?.ToString("D")), Csv(item.CorrelationId)));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", $"vaultshare-audit-{workspaceId:D}.csv");
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@') value = $"'{value}";
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
