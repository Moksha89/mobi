using System.Collections.ObjectModel;
using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.UI.ViewModels.Base;

namespace MobileControlHub.UI.ViewModels;

/// <summary>
/// Dashboard ViewModel showing system health, connected devices overview,
/// active sessions, and recent logs.
/// </summary>
public class DashboardViewModel : ViewModelBase
{
    private readonly IAdbService _adbService;
    private readonly IScrcpyService _scrcpyService;
    private readonly IRustDeskService _rustDeskService;
    private readonly IDeviceMonitorService _monitorService;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;

    private bool _isAdbAvailable;
    private bool _isScrcpyAvailable;
    private bool _isRustDeskRunning;
    private bool _isVpsConfigured;
    private bool _isMonitorRunning;
    private int _connectedDeviceCount;
    private int _activeSessionCount;
    private string _adbVersion = "Checking...";
    private string _scrcpyVersion = "Checking...";
    private string _rustDeskStatus = "Checking...";

    public bool IsAdbAvailable { get => _isAdbAvailable; set => SetProperty(ref _isAdbAvailable, value); }
    public bool IsScrcpyAvailable { get => _isScrcpyAvailable; set => SetProperty(ref _isScrcpyAvailable, value); }
    public bool IsRustDeskRunning { get => _isRustDeskRunning; set => SetProperty(ref _isRustDeskRunning, value); }
    public bool IsVpsConfigured { get => _isVpsConfigured; set => SetProperty(ref _isVpsConfigured, value); }
    public bool IsMonitorRunning { get => _isMonitorRunning; set => SetProperty(ref _isMonitorRunning, value); }
    public int ConnectedDeviceCount { get => _connectedDeviceCount; set => SetProperty(ref _connectedDeviceCount, value); }
    public int ActiveSessionCount { get => _activeSessionCount; set => SetProperty(ref _activeSessionCount, value); }
    public string AdbVersion { get => _adbVersion; set => SetProperty(ref _adbVersion, value); }
    public string ScrcpyVersion { get => _scrcpyVersion; set => SetProperty(ref _scrcpyVersion, value); }
    public string RustDeskStatus { get => _rustDeskStatus; set => SetProperty(ref _rustDeskStatus, value); }

    public ObservableCollection<LogEntry> RecentLogs { get; } = new();
    public ObservableCollection<string> Alerts { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand StartMonitorCommand { get; }
    public AsyncRelayCommand StopMonitorCommand { get; }

    public DashboardViewModel(
        IAdbService adbService,
        IScrcpyService scrcpyService,
        IRustDeskService rustDeskService,
        IDeviceMonitorService monitorService,
        IConfigurationService configService,
        ILogService logService)
    {
        _adbService = adbService;
        _scrcpyService = scrcpyService;
        _rustDeskService = rustDeskService;
        _monitorService = monitorService;
        _configService = configService;
        _logService = logService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        StartMonitorCommand = new AsyncRelayCommand(StartMonitorAsync);
        StopMonitorCommand = new AsyncRelayCommand(StopMonitorAsync);

        // Listen for device changes
        _monitorService.DevicesChanged += OnDevicesChanged;
        _logService.LogEntryAdded += OnLogEntryAdded;
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();

        // Auto-start monitoring if configured
        var config = await _configService.LoadAsync();
        if (config.StartMonitoringOnLaunch)
        {
            await StartMonitorAsync();
        }
    }

    private async Task RefreshAsync()
    {
        Alerts.Clear();

        // Check ADB
        IsAdbAvailable = await _adbService.IsAvailableAsync();
        AdbVersion = IsAdbAvailable ? await _adbService.GetVersionAsync() : "Not Found";
        if (!IsAdbAvailable) Alerts.Add("ADB executable not found. Configure path in Settings.");

        // Check scrcpy
        IsScrcpyAvailable = await _scrcpyService.IsAvailableAsync();
        ScrcpyVersion = IsScrcpyAvailable ? await _scrcpyService.GetVersionAsync() : "Not Found";
        if (!IsScrcpyAvailable) Alerts.Add("scrcpy executable not found. Configure path in Settings.");

        // Check RustDesk
        IsRustDeskRunning = _rustDeskService.IsRunning();
        RustDeskStatus = _rustDeskService.GetStatusDescription();

        // Check VPS config
        var config = await _configService.LoadAsync();
        IsVpsConfigured = config.VpsConfig.IsConfigured;
        if (!IsVpsConfigured) Alerts.Add("VPS not configured. Set up in VPS / Remote Access.");

        // Device & session counts
        ConnectedDeviceCount = _monitorService.GetCurrentDevices().Count;
        ActiveSessionCount = _scrcpyService.GetActiveSessions().Count;

        // Monitor status
        IsMonitorRunning = _monitorService.IsRunning;

        // Load recent logs
        await RefreshRecentLogsAsync();
    }

    private async Task RefreshRecentLogsAsync()
    {
        var logs = await _logService.GetRecentLogsAsync(20);
        DispatchToUI(() =>
        {
            RecentLogs.Clear();
            foreach (var log in logs)
                RecentLogs.Add(log);
        });
    }

    private async Task StartMonitorAsync()
    {
        await _monitorService.StartAsync();
        IsMonitorRunning = true;
    }

    private async Task StopMonitorAsync()
    {
        await _monitorService.StopAsync();
        IsMonitorRunning = false;
    }

    private void OnDevicesChanged(object? sender, List<AndroidDevice> devices)
    {
        DispatchToUI(() =>
        {
            ConnectedDeviceCount = devices.Count;
            ActiveSessionCount = _scrcpyService.GetActiveSessions().Count;
        });
    }

    private void OnLogEntryAdded(object? sender, LogEntry entry)
    {
        DispatchToUI(() =>
        {
            RecentLogs.Insert(0, entry);
            while (RecentLogs.Count > 20)
                RecentLogs.RemoveAt(RecentLogs.Count - 1);

            // Add critical errors as alerts
            if (entry.Level >= AppLogLevel.Error)
            {
                Alerts.Insert(0, $"[{entry.Timestamp:HH:mm:ss}] {entry.Message}");
                while (Alerts.Count > 10)
                    Alerts.RemoveAt(Alerts.Count - 1);
            }
        });
    }
}
