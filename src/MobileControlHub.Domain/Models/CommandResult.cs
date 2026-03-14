namespace MobileControlHub.Domain.Models;

/// <summary>
/// Result of executing an external process command (adb, scrcpy, etc.).
/// </summary>
public class CommandResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string Command { get; set; } = string.Empty;

    public static CommandResult Failed(string error, string command = "") => new()
    {
        Success = false,
        ExitCode = -1,
        StandardError = error,
        Command = command
    };

    public static CommandResult Ok(string output = "", string command = "") => new()
    {
        Success = true,
        ExitCode = 0,
        StandardOutput = output,
        Command = command
    };
}
