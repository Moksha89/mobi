namespace MobileControlHub.Domain.Models;

/// <summary>
/// Configuration for VPS and RustDesk self-hosted relay infrastructure.
/// </summary>
public class VpsConfiguration
{
    /// <summary>VPS host IP or hostname.</summary>
    public string Host { get; set; } = "69.197.142.77";

    /// <summary>SSH port for VPS management (default 22).</summary>
    public int SshPort { get; set; } = 22;

    /// <summary>RustDesk relay server address.</summary>
    public string RustDeskRelayServer { get; set; } = "69.197.142.77";

    /// <summary>RustDesk ID/rendezvous server address.</summary>
    public string RustDeskIdServer { get; set; } = "69.197.142.77";

    /// <summary>RustDesk relay port (default 21117).</summary>
    public int RustDeskRelayPort { get; set; } = 21117;

    /// <summary>RustDesk ID server port (default 21116).</summary>
    public int RustDeskIdPort { get; set; } = 21116;

    /// <summary>RustDesk API port (default 21118).</summary>
    public int RustDeskApiPort { get; set; } = 21118;

    /// <summary>Whether the VPS configuration has been saved.</summary>
    public bool IsConfigured { get; set; } = true;

    /// <summary>Whether the VPS host is currently reachable.</summary>
    public bool IsReachable { get; set; }

    /// <summary>Whether the RustDesk relay server is reachable.</summary>
    public bool IsRelayReachable { get; set; }

    /// <summary>Whether the RustDesk ID server is reachable.</summary>
    public bool IsIdServerReachable { get; set; }

    /// <summary>Last time connectivity was tested.</summary>
    public DateTime? LastTestedAt { get; set; }
}
