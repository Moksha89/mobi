using System.Collections.ObjectModel;
using System.Windows.Input;
using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.UI.ViewModels.Base;

namespace MobileControlHub.UI.ViewModels;

/// <summary>
/// ViewModel for the Device Manager page.
/// Shows connected devices, supports actions, search/filter, and bulk operations.
/// </summary>
public class DevicesViewModel : ViewModelBase
{
    private readonly IAdbService _adbService;
    private readonly IScrcpyService _scrcpyService;
    private readonly IDeviceMonitorService _monitorService;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;

    private string _searchFilter = string.Empty;
    private AndroidDevice? _selectedDevice;
    private bool _isRefreshing;
    private string _statusMessage = string.Empty;

    public ObservableCollection<AndroidDevice> Devices { get; } = new();
    public ObservableCollection<AndroidDevice> FilteredDevices { get; } = new();

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
                ApplyFilter();
        }
    }

    public AndroidDevice? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // Commands
    public AsyncRelayCommand RefreshDevicesCommand { get; }
    public AsyncRelayCommand<AndroidDevice> OpenScrcpyCommand { get; }
    public AsyncRelayCommand<AndroidDevice> CloseScrcpyCommand { get; }
    public AsyncRelayCommand<AndroidDevice> ReconnectCommand { get; }
    public AsyncRelayCommand<AndroidDevice> RebootCommand { get; }
    public AsyncRelayCommand<AndroidDevice> ScreenshotCommand { get; }
    public AsyncRelayCommand OpenAllSelectedCommand { get; }
    public RelayCommand<AndroidDevice> CopyInfoCommand { get; }
    public AsyncRelayCommand<AndroidDevice> SetFriendlyNameCommand { get; }

    public DevicesViewModel(
        IAdbService adbService,
        IScrcpyService scrcpyService,
        IDeviceMonitorService monitorService,
        IConfigurationService configService,
        ILogService logService)
    {
        _adbService = adbService;
        _scrcpyService = scrcpyService;
        _monitorService = monitorService;
        _configService = configService;
        _logService = logService;

        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync);
        OpenScrcpyCommand = new AsyncRelayCommand<AndroidDevice>(OpenScrcpyAsync);
        CloseScrcpyCommand = new AsyncRelayCommand<AndroidDevice>(CloseScrcpyAsync);
        ReconnectCommand = new AsyncRelayCommand<AndroidDevice>(ReconnectAsync);
        RebootCommand = new AsyncRelayCommand<AndroidDevice>(RebootAsync);
        ScreenshotCommand = new AsyncRelayCommand<AndroidDevice>(ScreenshotAsync);
        OpenAllSelectedCommand = new AsyncRelayCommand(OpenAllSelectedAsync);
        CopyInfoCommand = new RelayCommand<AndroidDevice>(CopyInfo);
        SetFriendlyNameCommand = new AsyncRelayCommand<AndroidDevice>(SetFriendlyNameAsync);

        _monitorService.DevicesChanged += OnDevicesChanged;
    }

    public async Task InitializeAsync()
    {
        await RefreshDevicesAsync();
    }

    private async Task RefreshDevicesAsync()
    {
        IsRefreshing = true;
        StatusMessage = "Scanning devices...";

        try
        {
            await _monitorService.RefreshNowAsync();
            var devices = _monitorService.GetCurrentDevices();

            DispatchToUI(() =>
            {
                Devices.Clear();
                foreach (var device in devices)
                    Devices.Add(device);
                ApplyFilter();
            });

            StatusMessage = $"Found {devices.Count} device(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            await _logService.LogErrorAsync($"Failed to refresh devices: {ex.Message}", source: "DevicesVM");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task OpenScrcpyAsync(AndroidDevice? device)
    {
        if (device == null) return;

        try
        {
            StatusMessage = $"Opening scrcpy for {device.DisplayName}...";
            var session = await _scrcpyService.LaunchSessionAsync(device.SerialNumber);
            device.HasActiveSession = session.State == SessionState.Running;
            StatusMessage = session.State == SessionState.Running
                ? $"scrcpy opened for {device.DisplayName}"
                : $"Failed to open scrcpy: {session.LastError}";
            OnPropertyChanged(nameof(Devices));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task CloseScrcpyAsync(AndroidDevice? device)
    {
        if (device == null) return;

        var session = _scrcpyService.GetSessionForDevice(device.SerialNumber);
        if (session != null)
        {
            await _scrcpyService.StopSessionAsync(session.SessionId);
            device.HasActiveSession = false;
            StatusMessage = $"scrcpy closed for {device.DisplayName}";
            OnPropertyChanged(nameof(Devices));
        }
    }

    private async Task ReconnectAsync(AndroidDevice? device)
    {
        if (device == null) return;

        StatusMessage = $"Reconnecting {device.DisplayName}...";
        var result = await _adbService.ReconnectDeviceAsync(device.SerialNumber);
        StatusMessage = result.Success
            ? $"Reconnected {device.DisplayName}"
            : $"Reconnect failed: {result.StandardError}";
    }

    private async Task RebootAsync(AndroidDevice? device)
    {
        if (device == null) return;

        StatusMessage = $"Rebooting {device.DisplayName}...";
        var result = await _adbService.RebootDeviceAsync(device.SerialNumber);
        StatusMessage = result.Success
            ? $"Reboot command sent to {device.DisplayName}"
            : $"Reboot failed: {result.StandardError}";
    }

    private async Task ScreenshotAsync(AndroidDevice? device)
    {
        if (device == null) return;

        var savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "MobileControlHub",
            $"screenshot_{device.SerialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

        StatusMessage = $"Capturing screenshot from {device.DisplayName}...";
        var result = await _adbService.CaptureScreenshotAsync(device.SerialNumber, savePath);
        StatusMessage = result.Success
            ? $"Screenshot saved: {savePath}"
            : $"Screenshot failed: {result.StandardError}";
    }

    private async Task OpenAllSelectedAsync()
    {
        var selected = Devices.Where(d => d.IsSelected && d.ConnectionState == DeviceConnectionState.Online).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "No online devices selected";
            return;
        }

        StatusMessage = $"Opening scrcpy for {selected.Count} device(s)...";
        foreach (var device in selected)
        {
            await OpenScrcpyAsync(device);
            await Task.Delay(500); // Brief delay between launches
        }
        StatusMessage = $"Opened scrcpy for {selected.Count} device(s)";
    }

    private void CopyInfo(AndroidDevice? device)
    {
        if (device == null) return;

        var info = $"Serial: {device.SerialNumber}\n"
            + $"Model: {device.Model}\n"
            + $"Manufacturer: {device.Manufacturer}\n"
            + $"Android: {device.AndroidVersion}\n"
            + $"State: {device.ConnectionState}\n"
            + $"Battery: {device.BatteryLevel}%";

        try
        {
            System.Windows.Clipboard.SetText(info);
            StatusMessage = "Device info copied to clipboard";
        }
        catch
        {
            StatusMessage = "Failed to copy to clipboard";
        }
    }

    private async Task SetFriendlyNameAsync(AndroidDevice? device)
    {
        // In a real app this would show a dialog; for MVP we set a placeholder
        if (device == null) return;
        // The UI will need to handle the input dialog
        await Task.CompletedTask;
    }

    private void ApplyFilter()
    {
        FilteredDevices.Clear();
        var filter = _searchFilter.Trim().ToLowerInvariant();

        foreach (var device in Devices)
        {
            if (string.IsNullOrEmpty(filter)
                || device.SerialNumber.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || device.Model.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || device.Manufacturer.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || device.FriendlyName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || device.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredDevices.Add(device);
            }
        }
    }

    private void OnDevicesChanged(object? sender, List<AndroidDevice> devices)
    {
        DispatchToUI(() =>
        {
            Devices.Clear();
            foreach (var device in devices)
                Devices.Add(device);
            ApplyFilter();
        });
    }
}

/// <summary>
/// Typed async relay command for device actions.
/// </summary>
public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) =>
        !_isExecuting && (_canExecute?.Invoke(parameter is T t ? t : default) ?? true);

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute(parameter is T t ? t : default);
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}

/// <summary>
/// Typed relay command for device actions.
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) =>
        _canExecute?.Invoke(parameter is T t ? t : default) ?? true;

    public void Execute(object? parameter) =>
        _execute(parameter is T t ? t : default);
}
