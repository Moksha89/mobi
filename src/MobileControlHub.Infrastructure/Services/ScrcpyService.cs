using System.Collections.Concurrent;
using System.Diagnostics;
using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.Infrastructure.Helpers;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Implementation of IScrcpyService for launching and managing scrcpy sessions.
/// </summary>
public class ScrcpyService : IScrcpyService
{
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;
    private readonly ConcurrentDictionary<string, ScrcpySession> _sessions = new();
    private readonly ConcurrentDictionary<string, Process> _processes = new();

    public event EventHandler<ScrcpySession>? SessionStateChanged;

    public ScrcpyService(IConfigurationService configService, ILogService logService)
    {
        _configService = configService;
        _logService = logService;
    }

    private async Task<string> GetScrcpyPathAsync()
    {
        var config = await _configService.LoadAsync();
        return config.ScrcpyPath;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var path = await GetScrcpyPathAsync();
            if (ProcessRunner.ExecutableExists(path))
            {
                var result = await ProcessRunner.RunAsync(path, "--version", timeoutMs: 5000, ct: ct);
                return result.Success;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        var path = await GetScrcpyPathAsync();
        var result = await ProcessRunner.RunAsync(path, "--version", timeoutMs: 5000, ct: ct);
        return result.Success ? result.StandardOutput.Split('\n').FirstOrDefault()?.Trim() ?? "Unknown" : "Unknown";
    }

    public async Task<ScrcpySession> LaunchSessionAsync(
        string deviceSerial,
        string? customArgs = null,
        bool autoRestart = false,
        CancellationToken ct = default)
    {
        // Check if session already exists for this device
        var existing = GetSessionForDevice(deviceSerial);
        if (existing != null && existing.State == SessionState.Running)
        {
            await _logService.LogWarningAsync(
                $"scrcpy session already running for {deviceSerial}",
                category: "scrcpy", deviceSerial: deviceSerial);
            return existing;
        }

        var config = await _configService.LoadAsync(ct);
        var scrcpyPath = config.ScrcpyPath;
        var args = $"--serial={deviceSerial}";

        // Add custom args or defaults
        var extraArgs = !string.IsNullOrWhiteSpace(customArgs) ? customArgs : config.DefaultScrcpyArgs;
        if (!string.IsNullOrWhiteSpace(extraArgs))
            args += $" {extraArgs}";

        var session = new ScrcpySession
        {
            DeviceSerial = deviceSerial,
            State = SessionState.Starting,
            CustomArguments = extraArgs,
            AutoRestart = autoRestart,
            StartedAt = DateTime.UtcNow
        };

        await _logService.LogInfoAsync(
            $"Launching scrcpy for {deviceSerial} with args: {args}",
            category: "scrcpy", deviceSerial: deviceSerial);

        var process = ProcessRunner.StartDetached(scrcpyPath, args);

        if (process == null)
        {
            session.State = SessionState.Error;
            session.LastError = "Failed to start scrcpy process";
            await _logService.LogErrorAsync(
                $"Failed to launch scrcpy for {deviceSerial}",
                deviceSerial: deviceSerial);
            SessionStateChanged?.Invoke(this, session);
            return session;
        }

        session.ProcessId = process.Id;
        session.State = SessionState.Running;

        _sessions[session.SessionId] = session;
        _processes[session.SessionId] = process;

        // Monitor the process for exit
        _ = MonitorProcessAsync(session, process);

        SessionStateChanged?.Invoke(this, session);
        return session;
    }

    public async Task<bool> StopSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return false;

        if (_processes.TryRemove(sessionId, out var process))
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                await _logService.LogErrorAsync(
                    $"Error stopping scrcpy process: {ex.Message}",
                    deviceSerial: session.DeviceSerial);
            }
            finally
            {
                process.Dispose();
            }
        }

        session.State = SessionState.Stopped;
        session.EndedAt = DateTime.UtcNow;
        session.AutoRestart = false; // Prevent restart on explicit stop

        _sessions.TryRemove(sessionId, out _);

        await _logService.LogInfoAsync(
            $"Stopped scrcpy session for {session.DeviceSerial}",
            category: "scrcpy", deviceSerial: session.DeviceSerial);

        SessionStateChanged?.Invoke(this, session);
        return true;
    }

    public async Task StopAllSessionsAsync(CancellationToken ct = default)
    {
        var sessionIds = _sessions.Keys.ToList();
        foreach (var id in sessionIds)
        {
            await StopSessionAsync(id, ct);
        }
    }

    public IReadOnlyList<ScrcpySession> GetActiveSessions()
    {
        return _sessions.Values.ToList().AsReadOnly();
    }

    public ScrcpySession? GetSessionForDevice(string deviceSerial)
    {
        return _sessions.Values.FirstOrDefault(s => s.DeviceSerial == deviceSerial);
    }

    public bool IsSessionAlive(string sessionId)
    {
        if (!_processes.TryGetValue(sessionId, out var process))
            return false;

        try
        {
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ScrcpySession?> RestartSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var oldSession))
            return null;

        await StopSessionAsync(sessionId, ct);

        var newSession = await LaunchSessionAsync(
            oldSession.DeviceSerial,
            oldSession.CustomArguments,
            oldSession.AutoRestart,
            ct);

        newSession.RestartCount = oldSession.RestartCount + 1;
        return newSession;
    }

    /// <summary>
    /// Monitor a scrcpy process and handle its exit.
    /// </summary>
    private async Task MonitorProcessAsync(ScrcpySession session, Process process)
    {
        try
        {
            await process.WaitForExitAsync();
        }
        catch
        {
            // Process may have been killed
        }

        if (!_sessions.ContainsKey(session.SessionId))
            return; // Already cleaned up

        session.EndedAt = DateTime.UtcNow;

        if (session.AutoRestart && session.RestartCount < 5)
        {
            session.State = SessionState.Reconnecting;
            SessionStateChanged?.Invoke(this, session);

            await _logService.LogWarningAsync(
                $"scrcpy session ended for {session.DeviceSerial}, auto-restarting (attempt {session.RestartCount + 1})",
                category: "scrcpy", deviceSerial: session.DeviceSerial);

            // Brief delay before restart
            await Task.Delay(2000);
            await RestartSessionAsync(session.SessionId);
        }
        else
        {
            session.State = SessionState.Stopped;
            _sessions.TryRemove(session.SessionId, out _);
            _processes.TryRemove(session.SessionId, out _);

            await _logService.LogInfoAsync(
                $"scrcpy session ended for {session.DeviceSerial}",
                category: "scrcpy", deviceSerial: session.DeviceSerial);

            SessionStateChanged?.Invoke(this, session);
        }
    }
}
