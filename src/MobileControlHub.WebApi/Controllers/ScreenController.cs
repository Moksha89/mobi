using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Infrastructure.Helpers;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

[ApiController]
[Route("api/devices/{serial}/screen")]
public class ScreenController : ControllerBase
{
    private readonly IAdbService _adbService;
    private readonly IConfigurationService _configService;

    public ScreenController(IAdbService adbService, IConfigurationService configService)
    {
        _adbService = adbService;
        _configService = configService;
    }

    /// <summary>
    /// Get a live screenshot from the device as a JPEG image.
    /// Uses 'adb exec-out screencap -p' for direct PNG capture via stdout,
    /// avoiding the slower screencap-to-file-then-pull approach.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetScreenshot(string serial, [FromQuery] int quality = 80, CancellationToken ct = default)
    {
        var config = await _configService.LoadAsync(ct);
        var adbPath = config.AdbPath;

        if (!ProcessRunner.ExecutableExists(adbPath))
            return StatusCode(503, ApiResult.Fail("ADB not available"));

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = $"-s {serial} exec-out screencap -p",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            using var ms = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(ms, ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(10000);
            try { await process.WaitForExitAsync(timeoutCts.Token); }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                return StatusCode(504, ApiResult.Fail("Screenshot timed out"));
            }

            if (ms.Length < 100)
            {
                var stderr = await process.StandardError.ReadToEndAsync(ct);
                return BadRequest(ApiResult.Fail($"Screenshot failed: {stderr}"));
            }

            ms.Position = 0;
            return File(ms.ToArray(), "image/png");
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult.Fail($"Screenshot error: {ex.Message}"));
        }
    }

    /// <summary>Get device screen resolution.</summary>
    [HttpGet("info")]
    public async Task<ActionResult<ScreenInfo>> GetScreenInfo(string serial, CancellationToken ct)
    {
        var result = await _adbService.ExecuteShellCommandAsync(serial, "wm size", ct);
        int width = 1080, height = 1920;
        if (result.Success)
        {
            // Output: "Physical size: 1080x1920"
            var parts = result.StandardOutput.Split(':').LastOrDefault()?.Trim().Split('x');
            if (parts?.Length == 2)
            {
                int.TryParse(parts[0], out width);
                int.TryParse(parts[1], out height);
            }
        }

        return Ok(new ScreenInfo { Width = width, Height = height });
    }

    /// <summary>Send a tap event to the device.</summary>
    [HttpPost("tap")]
    public async Task<ActionResult<ApiResult>> Tap(string serial, [FromBody] TapRequest request, CancellationToken ct)
    {
        var result = await _adbService.ExecuteShellCommandAsync(serial,
            $"input tap {request.X} {request.Y}", ct);
        return result.Success
            ? Ok(ApiResult.Ok("Tap sent"))
            : BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Send a swipe event to the device.</summary>
    [HttpPost("swipe")]
    public async Task<ActionResult<ApiResult>> Swipe(string serial, [FromBody] SwipeRequest request, CancellationToken ct)
    {
        var duration = request.DurationMs > 0 ? request.DurationMs : 300;
        var result = await _adbService.ExecuteShellCommandAsync(serial,
            $"input swipe {request.X1} {request.Y1} {request.X2} {request.Y2} {duration}", ct);
        return result.Success
            ? Ok(ApiResult.Ok("Swipe sent"))
            : BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Send a key event to the device (e.g., KEYCODE_BACK = 4, KEYCODE_HOME = 3).</summary>
    [HttpPost("key")]
    public async Task<ActionResult<ApiResult>> SendKey(string serial, [FromBody] KeyRequest request, CancellationToken ct)
    {
        var result = await _adbService.ExecuteShellCommandAsync(serial,
            $"input keyevent {request.KeyCode}", ct);
        return result.Success
            ? Ok(ApiResult.Ok("Key sent"))
            : BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Type text on the device.</summary>
    [HttpPost("text")]
    public async Task<ActionResult<ApiResult>> SendText(string serial, [FromBody] TextRequest request, CancellationToken ct)
    {
        // Escape special characters for adb shell input text
        var escaped = request.Text.Replace(" ", "%s").Replace("'", "\\'").Replace("\"", "\\\"");
        var result = await _adbService.ExecuteShellCommandAsync(serial,
            $"input text \"{escaped}\"", ct);
        return result.Success
            ? Ok(ApiResult.Ok("Text sent"))
            : BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Send a long press event to the device.</summary>
    [HttpPost("longpress")]
    public async Task<ActionResult<ApiResult>> LongPress(string serial, [FromBody] TapRequest request, CancellationToken ct)
    {
        // Long press is a swipe from same point to same point with duration
        var result = await _adbService.ExecuteShellCommandAsync(serial,
            $"input swipe {request.X} {request.Y} {request.X} {request.Y} 800", ct);
        return result.Success
            ? Ok(ApiResult.Ok("Long press sent"))
            : BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Wake up / turn on the screen.</summary>
    [HttpPost("wake")]
    public async Task<ActionResult<ApiResult>> WakeScreen(string serial, CancellationToken ct)
    {
        var result = await _adbService.ExecuteShellCommandAsync(serial,
            "input keyevent KEYCODE_WAKEUP", ct);
        return result.Success
            ? Ok(ApiResult.Ok("Wake sent"))
            : BadRequest(ApiResult.Fail(result.StandardError));
    }
}
