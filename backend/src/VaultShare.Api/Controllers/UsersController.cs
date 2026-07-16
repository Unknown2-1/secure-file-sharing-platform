using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaultShare.Application.Privacy;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public sealed class UsersController(IUserDataExportService dataExportService) : ControllerBase
{
    [HttpGet("me/export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var export = await dataExportService.ExportAsync(User, cancellationToken);
        if (export is null) return Unauthorized();
        Response.ContentType = "application/json; charset=utf-8";
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = $"vaultshare-export-{DateTimeOffset.UtcNow:yyyyMMdd}.json",
        }.ToString();
        await JsonSerializer.SerializeAsync(Response.Body, export, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }, cancellationToken);
        return new EmptyResult();
    }
}
