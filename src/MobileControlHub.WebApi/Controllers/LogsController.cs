using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;

    public LogsController(ILogService logService)
    {
        _logService = logService;
    }

    /// <summary>Get log entries with optional filters.</summary>
    [HttpGet]
    public async Task<ActionResult<List<LogEntry>>> GetLogs(
        [FromQuery] string? level,
        [FromQuery] string? category,
        [FromQuery] string? deviceSerial,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 200,
        CancellationToken ct = default)
    {
        AppLogLevel? minLevel = null;
        if (!string.IsNullOrEmpty(level) && Enum.TryParse<AppLogLevel>(level, true, out var parsed))
        {
            minLevel = parsed;
        }

        var logs = await _logService.GetLogsAsync(minLevel, category, deviceSerial, from, to, limit, ct);
        return Ok(logs);
    }

    /// <summary>Get recent log entries.</summary>
    [HttpGet("recent")]
    public async Task<ActionResult<List<LogEntry>>> GetRecentLogs([FromQuery] int count = 50, CancellationToken ct = default)
    {
        var logs = await _logService.GetRecentLogsAsync(count, ct);
        return Ok(logs);
    }

    /// <summary>Clear logs older than a date.</summary>
    [HttpDelete]
    public async Task<ActionResult<ApiResult>> ClearLogs([FromQuery] DateTime? olderThan, CancellationToken ct = default)
    {
        await _logService.ClearLogsAsync(olderThan, ct);
        return Ok(ApiResult.Ok("Logs cleared"));
    }
}
