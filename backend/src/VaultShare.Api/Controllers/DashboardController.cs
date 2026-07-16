using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaultShare.Application.Dashboard;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardSummary>> Get([FromQuery] Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var summary = await dashboardService.GetAsync(workspaceId, User, cancellationToken);
        return summary is null ? Forbid() : Ok(summary);
    }
}
