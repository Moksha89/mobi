using Microsoft.AspNetCore.SignalR;

namespace MobileControlHub.WebApi.Hubs;

/// <summary>
/// SignalR hub for real-time device and session updates.
/// Clients connect to receive push notifications about device state changes.
/// </summary>
public class DeviceHub : Hub
{
    private readonly ILogger<DeviceHub> _logger;

    public DeviceHub(ILogger<DeviceHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Request an immediate device refresh from the client.</summary>
    public async Task RequestRefresh()
    {
        _logger.LogInformation("Client {ConnectionId} requested device refresh", Context.ConnectionId);
        await Clients.Caller.SendAsync("RefreshRequested");
    }
}
