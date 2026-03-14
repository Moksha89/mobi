using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.Infrastructure.Services;
using MobileControlHub.UI.ViewModels.Base;

namespace MobileControlHub.UI.ViewModels;

/// <summary>
/// ViewModel for the central Settings page.
/// Manages tool paths, monitoring settings, scrcpy defaults, startup, and config export/import.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;
    private readonly StartupService _startupService;

    // Tool paths
    private string _adbPath = string.Empty;
    private string _scrcpyPath = string.Empty;
    private string _rustDeskPath = string.Empty;

    // Monitoring
    private int _pollInterval = 5;
    private int _adbTimeout = 10;
    private int _maxRetries = 3;
    private int _retryDelay = 2;

    // Auto-reconnect
    private bool _autoReconnect = true;
    private bool _autoRestartScrcpy;

    // Startup
    private bool _startOnBoot;
    private bool _startMonitorOnLaunch = true;
    private bool _restoreSessionsOnLaunch;

    // scrcpy
    private string _defaultScrcpyArgs = "--max-size=1024 --max-fps=30";

    // Status
    private string _statusMessage = string.Empty;
    private bool _isSaving;

    public string AdbPath { get => _adbPath; set => SetProperty(ref _adbPath, value); }
    public string ScrcpyPath { get => _scrcpyPath; set => SetProperty(ref _scrcpyPath, value); }
    public string RustDeskPath { get => _rustDeskPath; set => SetProperty(ref _rustDeskPath, value); }
    public int PollInterval { get => _pollInterval; set => SetProperty(ref _pollInterval, value); }
    public int AdbTimeout { get => _adbTimeout; set => SetProperty(ref _adbTimeout, value); }
    public int MaxRetries { get => _maxRetries; set => SetProperty(ref _maxRetries, value); }
    public int RetryDelay { get => _retryDelay; set => SetProperty(ref _retryDelay, value); }
    public bool AutoReconnect { get => _autoReconnect; set => SetProperty(ref _autoReconnect, value); }
    public bool AutoRestartScrcpy { get => _autoRestartScrcpy; set => SetProperty(ref _autoRestartScrcpy, value); }
    public bool StartOnBoot { get => _startOnBoot; set => SetProperty(ref _startOnBoot, value); }
    public bool StartMonitorOnLaunch { get => _startMonitorOnLaunch; set => SetProperty(ref _startMonitorOnLaunch, value); }
    public bool RestoreSessionsOnLaunch { get => _restoreSessionsOnLaunch; set => SetProperty(ref _restoreSessionsOnLaunch, value); }
    public string DefaultScrcpyArgs { get => _defaultScrcpyArgs; set => SetProperty(ref _defaultScrcpyArgs, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public bool IsSaving { get => _isSaving; set => SetProperty(ref _isSaving, value); }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ExportConfigCommand { get; }
    public AsyncRelayCommand ImportConfigCommand { get; }
    public RelayCommand BrowseAdbCommand { get; }
    public RelayCommand BrowseScrcpyCommand { get; }
    public RelayCommand BrowseRustDeskCommand { get; }

    public SettingsViewModel(
        IConfigurationService configService,
        ILogService logService,
        StartupService startupService)
    {
        _configService = configService;
        _logService = logService;
        _startupService = startupService;

        SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ExportConfigCommand = new AsyncRelayCommand(ExportConfigAsync);
        ImportConfigCommand = new AsyncRelayCommand(ImportConfigAsync);
        BrowseAdbCommand = new RelayCommand(() => BrowseFile("ADB", path => AdbPath = path));
        BrowseScrcpyCommand = new RelayCommand(() => BrowseFile("scrcpy", path => ScrcpyPath = path));
        BrowseRustDeskCommand = new RelayCommand(() => BrowseFile("RustDesk", path => RustDeskPath = path));

        _ = LoadSettingsAsync();
    }

    public async Task LoadSettingsAsync()
    {
        var config = await _configService.LoadAsync();

        AdbPath = config.AdbPath;
        ScrcpyPath = config.ScrcpyPath;
        RustDeskPath = config.RustDeskPath;
        PollInterval = config.DevicePollIntervalSeconds;
        AdbTimeout = config.AdbTimeoutSeconds;
        MaxRetries = config.MaxRetryAttempts;
        RetryDelay = config.RetryDelaySeconds;
        AutoReconnect = config.AutoReconnectDevices;
        AutoRestartScrcpy = config.AutoRestartScrcpy;
        StartOnBoot = config.StartOnWindowsBoot;
        StartMonitorOnLaunch = config.StartMonitoringOnLaunch;
        RestoreSessionsOnLaunch = config.RestoreSessionsOnLaunch;
        DefaultScrcpyArgs = config.DefaultScrcpyArgs;
    }

    private async Task SaveSettingsAsync()
    {
        IsSaving = true;
        try
        {
            var config = await _configService.LoadAsync();

            config.AdbPath = AdbPath;
            config.ScrcpyPath = ScrcpyPath;
            config.RustDeskPath = RustDeskPath;
            config.DevicePollIntervalSeconds = PollInterval;
            config.AdbTimeoutSeconds = AdbTimeout;
            config.MaxRetryAttempts = MaxRetries;
            config.RetryDelaySeconds = RetryDelay;
            config.AutoReconnectDevices = AutoReconnect;
            config.AutoRestartScrcpy = AutoRestartScrcpy;
            config.StartOnWindowsBoot = StartOnBoot;
            config.StartMonitoringOnLaunch = StartMonitorOnLaunch;
            config.RestoreSessionsOnLaunch = RestoreSessionsOnLaunch;
            config.DefaultScrcpyArgs = DefaultScrcpyArgs;

            await _configService.SaveAsync(config);

            // Update startup registration
            await _startupService.SetStartOnBootAsync(StartOnBoot);

            StatusMessage = "Settings saved successfully";
            await _logService.LogInfoAsync("Settings saved", category: "Settings");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
            await _logService.LogErrorAsync($"Failed to save settings: {ex.Message}", source: "SettingsVM");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task ExportConfigAsync()
    {
        try
        {
            var exportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "mch_config_export.json");

            await _configService.ExportToJsonAsync(exportPath);
            StatusMessage = $"Config exported to: {exportPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private async Task ImportConfigAsync()
    {
        // In a full implementation, this would use a file dialog
        // For MVP, we look for a known file path
        try
        {
            var importPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "mch_config_export.json");

            if (!File.Exists(importPath))
            {
                StatusMessage = "No config file found on Desktop (mch_config_export.json)";
                return;
            }

            await _configService.ImportFromJsonAsync(importPath);
            await LoadSettingsAsync();
            StatusMessage = "Config imported successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    private void BrowseFile(string toolName, Action<string> setter)
    {
        // WPF file dialog - in actual app this opens the file picker
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Select {toolName} executable",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            setter(dialog.FileName);
        }
    }
}
