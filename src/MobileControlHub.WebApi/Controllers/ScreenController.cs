using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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
    private static readonly HttpClient _proxyClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    // PC's local dashboard URL for proxying remote device requests
    // The tunnel forwards PC port 5000 to VPS port 15000
    private const string PcDashboardUrl = "http://127.0.0.1:15000";

    public ScreenController(IAdbService adbService, IConfigurationService configService)
    {
        _adbService = adbService;
        _configService = configService;
    }

    /// <summary>Check if a device serial belongs to a remote (pushed) physical device.</summary>
    private bool IsRemoteDevice(string serial)
    {
        // Remote devices are those that exist in the pushed device list but not in the local ADB
        // We check by looking at pushed devices in RemoteDevicesController
        return RemoteDevicesController.IsPushedDevice(serial);
    }

    /// <summary>
    /// Get a live screenshot from the device.
    /// Uses 'adb exec-out screencap -p' for direct PNG capture via stdout.
    /// Supports JPEG compression via ?quality=N (1-100) and resolution scaling via ?scale=N (0.1-1.0)
    /// for faster remote viewing over slow connections.
    /// For remote physical devices, proxies the request through the PC's local dashboard.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetScreenshot(
        string serial,
        [FromQuery] int quality = 80,
        [FromQuery] double scale = 1.0,
        CancellationToken ct = default)
    {
        // If this is a remote device, proxy the request to the PC's local dashboard
        if (IsRemoteDevice(serial))
        {
            return await ProxyToPcAsync($"/api/devices/{serial}/screen?quality={quality}&scale={scale}", ct);
        }

        var config = await _configService.LoadAsync(ct);
        var adbPath = config.AdbPath;

        if (!ProcessRunner.ExecutableExists(adbPath))
            return StatusCode(503, ApiResult.Fail("ADB not available"));

        quality = Math.Clamp(quality, 1, 100);
        scale = Math.Clamp(scale, 0.1, 1.0);

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

            // Convert PNG to JPEG with quality/scale for faster transfer
            try
            {
                using var srcBitmap = new Bitmap(ms);
                var targetWidth = (int)(srcBitmap.Width * scale);
                var targetHeight = (int)(srcBitmap.Height * scale);

                using var destBitmap = scale < 0.99
                    ? new Bitmap(srcBitmap, targetWidth, targetHeight)
                    : srcBitmap;

                using var jpegStream = new MemoryStream();
                var encoder = ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault(e => e.FormatID == ImageFormat.Jpeg.Guid);
                if (encoder != null)
                {
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                    destBitmap.Save(jpegStream, encoder, encoderParams);
                }
                else
                {
                    destBitmap.Save(jpegStream, ImageFormat.Jpeg);
                }

                Response.Headers["X-Original-Size"] = ms.Length.ToString();
                Response.Headers["X-Compressed-Size"] = jpegStream.Length.ToString();
                return File(jpegStream.ToArray(), "image/jpeg");
            }
            catch
            {
                // Fallback to raw PNG if image conversion fails
                ms.Position = 0;
                return File(ms.ToArray(), "image/png");
            }
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
        if (IsRemoteDevice(serial))
        {
            return await ProxyJsonToPcAsync<ScreenInfo>($"/api/devices/{serial}/screen/info", ct);
        }

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
        if (IsRemoteDevice(serial))
        {
            return await ProxyPostToPcAsync($"/api/devices/{serial}/screen/tap",
                System.Text.Json.JsonSerializer.Serialize(request), ct);
        }

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
        if (IsRemoteDevice(serial))
        {
            return await ProxyPostToPcAsync($"/api/devices/{serial}/screen/swipe",
                System.Text.Json.JsonSerializer.Serialize(request), ct);
        }

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
        if (IsRemoteDevice(serial))
        {
            return await ProxyPostToPcAsync($"/api/devices/{serial}/screen/key",
                System.Text.Json.JsonSerializer.Serialize(request), ct);
        }

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
        if (IsRemoteDevice(serial))
        {
            return await ProxyPostToPcAsync($"/api/devices/{serial}/screen/text",
                System.Text.Json.JsonSerializer.Serialize(request), ct);
        }

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
        if (IsRemoteDevice(serial))
        {
            return await ProxyPostToPcAsync($"/api/devices/{serial}/screen/longpress",
                System.Text.Json.JsonSerializer.Serialize(request), ct);
        }

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
        if (IsRemoteDevice(serial))
        {
            return await ProxyPostToPcAsync($"/api/devices/{serial}/screen/wake", null, ct);
        }

        var result = await _adbService.ExecuteShellCommandAsync(serial,
            "input keyevent KEYCODE_WAKEUP", ct);
        return result.Success
            ? Ok(ApiResult.Ok("Wake sent"))
            : BadRequest(ApiResult.Fail(result.StandardError));
    }

    // ===================== Proxy helpers for remote physical devices =====================

    /// <summary>Proxy a GET request to the PC's local dashboard and return raw content (images, etc).</summary>
    private async Task<IActionResult> ProxyToPcAsync(string path, CancellationToken ct)
    {
        try
        {
            var response = await _proxyClient.GetAsync($"{PcDashboardUrl}{path}", ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                return StatusCode((int)response.StatusCode,
                    ApiResult.Fail($"PC proxy error: {errorBody}"));
            }

            var content = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            return File(content, contentType);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, ApiResult.Fail(
                "Cannot reach PC dashboard. Make sure setup-all.bat is running on your PC and the tunnel is active."));
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, ApiResult.Fail("PC proxy request timed out"));
        }
    }

    /// <summary>Proxy a GET request that returns JSON to the PC's local dashboard.</summary>
    private async Task<ActionResult<T>> ProxyJsonToPcAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            var response = await _proxyClient.GetAsync($"{PcDashboardUrl}{path}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = System.Text.Json.JsonSerializer.Deserialize<T>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Ok(result);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502);
        }
    }

    /// <summary>Proxy a POST request to the PC's local dashboard.</summary>
    private async Task<ActionResult<ApiResult>> ProxyPostToPcAsync(string path, string? jsonBody, CancellationToken ct)
    {
        try
        {
            var content = jsonBody != null
                ? new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
                : null;
            var response = await _proxyClient.PostAsync($"{PcDashboardUrl}{path}", content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, ApiResult.Fail($"PC proxy: {body}"));

            return Ok(ApiResult.Ok("Proxied to PC successfully"));
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, ApiResult.Fail(
                "Cannot reach PC dashboard. Make sure setup-all.bat is running."));
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, ApiResult.Fail("PC proxy request timed out"));
        }
    }
}
