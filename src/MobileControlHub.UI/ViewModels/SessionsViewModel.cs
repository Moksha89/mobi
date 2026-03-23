using System.Collections.ObjectModel;
using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.UI.ViewModels.Base;

namespace MobileControlHub.UI.ViewModels;

/// <summary>
/// ViewModel for managing scrcpy sessions across all devices.
/// </summary>
public class SessionsViewModel : ViewModelBase
{
    private readonly IScrcpyService _scrcpyService;
    private readonly ILogService _logService;
    private string _statusMessage = string.Empty;

    public ObservableCollection<ScrcpySession> Sessions { get; } = new();

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand<ScrcpySession> StopSessionCommand { get; }
    public AsyncRelayCommand<ScrcpySession> RestartSessionCommand { get; }
    public AsyncRelayCommand StopAllCommand { get; }

    public SessionsViewModel(IScrcpyService scrcpyService, ILogService logService)
    {
        _scrcpyService = scrcpyService;
        _logService = logService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        StopSessionCommand = new AsyncRelayCommand<ScrcpySession>(StopSessionAsync);
        RestartSessionCommand = new AsyncRelayCommand<ScrcpySession>(RestartSessionAsync);
        StopAllCommand = new AsyncRelayCommand(StopAllAsync);

        _scrcpyService.SessionStateChanged += OnSessionStateChanged;
    }

    private Task RefreshAsync()
    {
        DispatchToUI(() =>
        {
            Sessions.Clear();
            foreach (var session in _scrcpyService.GetActiveSessions())
                Sessions.Add(session);
            StatusMessage = $"{Sessions.Count} active session(s)";
        });
        return Task.CompletedTask;
    }

    private async Task StopSessionAsync(ScrcpySession? session)
    {
        if (session == null) return;

        await _scrcpyService.StopSessionAsync(session.SessionId);
        StatusMessage = $"Stopped session for {session.DeviceSerial}";
        await RefreshAsync();
    }

    private async Task RestartSessionAsync(ScrcpySession? session)
    {
        if (session == null) return;

        await _scrcpyService.RestartSessionAsync(session.SessionId);
        StatusMessage = $"Restarted session for {session.DeviceSerial}";
        await RefreshAsync();
    }

    private async Task StopAllAsync()
    {
        await _scrcpyService.StopAllSessionsAsync();
        StatusMessage = "All sessions stopped";
        await RefreshAsync();
    }

    private void OnSessionStateChanged(object? sender, ScrcpySession session)
    {
        DispatchToUI(() =>
        {
            // Update or add the session in the list
            var existing = Sessions.FirstOrDefault(s => s.SessionId == session.SessionId);
            if (existing != null)
            {
                var index = Sessions.IndexOf(existing);
                Sessions[index] = session;
            }
            else if (session.State == SessionState.Running)
            {
                Sessions.Add(session);
            }

            // Remove stopped sessions
            var toRemove = Sessions.Where(s => s.State == SessionState.Stopped).ToList();
            foreach (var s in toRemove)
                Sessions.Remove(s);

            StatusMessage = $"{Sessions.Count} active session(s)";
        });
    }
}
