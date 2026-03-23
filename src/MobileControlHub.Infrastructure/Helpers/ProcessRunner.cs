using System.Diagnostics;
using MobileControlHub.Domain.Models;

namespace MobileControlHub.Infrastructure.Helpers;

/// <summary>
/// Utility for running external processes (adb, scrcpy, etc.) with proper
/// output capture, timeout handling, and cancellation support.
/// </summary>
public static class ProcessRunner
{
    /// <summary>
    /// Run an external process and capture its output.
    /// </summary>
    public static async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        int timeoutMs = 30000,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var command = $"{fileName} {arguments}";

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? string.Empty
            };

            process.Start();

            // Read stdout and stderr concurrently to avoid deadlocks
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            // Wait for process to exit with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout -- kill the process
                TryKillProcess(process);
                sw.Stop();
                return new CommandResult
                {
                    Success = false,
                    ExitCode = -1,
                    StandardError = $"Process timed out after {timeoutMs}ms",
                    Duration = sw.Elapsed,
                    Command = command
                };
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            sw.Stop();

            return new CommandResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = stdout.Trim(),
                StandardError = stderr.Trim(),
                Duration = sw.Elapsed,
                Command = command
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return CommandResult.Failed("Operation was cancelled", command);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return CommandResult.Failed($"Failed to execute: {ex.Message}", command);
        }
    }

    /// <summary>
    /// Start a long-running process (e.g., scrcpy) without waiting for exit.
    /// Returns the Process object for tracking.
    /// </summary>
    public static Process? StartDetached(
        string fileName,
        string arguments,
        string? workingDirectory = null)
    {
        try
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false, // scrcpy needs its own window
                WorkingDirectory = workingDirectory ?? string.Empty
            };

            process.Start();
            return process;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if an executable exists at the given path.
    /// </summary>
    public static bool ExecutableExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Check absolute/relative path
        if (File.Exists(path))
            return true;

        // Check in PATH environment variable
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        return pathDirs.Any(dir => File.Exists(Path.Combine(dir, path)));
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort kill
        }
    }
}
