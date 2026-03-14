using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.WebApi.Hubs;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly IScrcpyService _scrcpyService;
    private readonly ILogService _logService;
    private readonly IHubContext<DeviceHub> _hubContext;

    public SessionsController(
        IScrcpyService scrcpyService,
        ILogService logService,
        IHubContext<DeviceHub> hubContext)
    {
        _scrcpyService = scrcpyService;
        _logService = logService;
        _hubContext = hubContext;
    }

    /// <summary>Get all active scrcpy sessions.</summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<ScrcpySession>> GetSessions()
    {
        return Ok(_scrcpyService.GetActiveSessions());
    }

    /// <summary>Get session for a specific device.</summary>
    [HttpGet("device/{serial}")]
    public ActionResult<ScrcpySession> GetSessionForDevice(string serial)
    {
        var session = _scrcpyService.GetSessionForDevice(serial);
        if (session == null)
            return NotFound(ApiResult.Fail($"No active session for device {serial}"));
        return Ok(session);
    }

    /// <summary>Launch a scrcpy session for a device.</summary>
    [HttpPost("{serial}/start")]
    public async Task<ActionResult<ScrcpySession>> StartSession(string serial, [FromBody] LaunchSessionRequest? request = null, CancellationToken ct = default)
    {
        try
        {
            var session = await _scrcpyService.LaunchSessionAsync(
                serial,
                request?.CustomArgs,
                request?.AutoRestart ?? false,
                ct);

            await _logService.LogInfoAsync($"Started scrcpy session for {serial}", "Session", serial, ct);
            await _hubContext.Clients.All.SendAsync("SessionStarted", session, ct);
            return Ok(session);
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResult.Fail(ex.Message));
        }
    }

    /// <summary>Stop a scrcpy session by session ID.</summary>
    [HttpPost("{sessionId}/stop")]
    public async Task<ActionResult<ApiResult>> StopSession(string sessionId, CancellationToken ct)
    {
        var success = await _scrcpyService.StopSessionAsync(sessionId, ct);
        if (success)
        {
            await _logService.LogInfoAsync($"Stopped scrcpy session {sessionId}", "Session", ct: ct);
            await _hubContext.Clients.All.SendAsync("SessionStopped", sessionId, ct);
            return Ok(ApiResult.Ok("Session stopped"));
        }
        return BadRequest(ApiResult.Fail("Failed to stop session"));
    }

    /// <summary>Stop all scrcpy sessions.</summary>
    [HttpPost("stop-all")]
    public async Task<ActionResult<ApiResult>> StopAllSessions(CancellationToken ct)
    {
        await _scrcpyService.StopAllSessionsAsync(ct);
        await _logService.LogInfoAsync("Stopped all scrcpy sessions", "Session", ct: ct);
        await _hubContext.Clients.All.SendAsync("AllSessionsStopped", ct);
        return Ok(ApiResult.Ok("All sessions stopped"));
    }

    /// <summary>Restart a scrcpy session.</summary>
    [HttpPost("{sessionId}/restart")]
    public async Task<ActionResult> RestartSession(string sessionId, CancellationToken ct)
    {
        var session = await _scrcpyService.RestartSessionAsync(sessionId, ct);
        if (session != null)
        {
            await _logService.LogInfoAsync($"Restarted scrcpy session {sessionId}", "Session", ct: ct);
            await _hubContext.Clients.All.SendAsync("SessionRestarted", session, ct);
            return Ok(session);
        }
        return BadRequest(ApiResult.Fail("Failed to restart session"));
    }
}
