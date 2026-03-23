using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.UI.ViewModels.Base;

namespace MobileControlHub.UI.ViewModels;

/// <summary>
/// ViewModel for VPS and RustDesk remote access configuration.
/// </summary>
public class VpsSettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configService;
    private readonly IRustDeskService _rustDeskService;
    private readonly ILogService _logService;

    private string _vpsHost = string.Empty;
    private int _sshPort = 22;
    private string _relayServer = string.Empty;
    private string _idServer = string.Empty;
    private int _relayPort = 21117;
    private int _idPort = 21116;
    private int _apiPort = 21118;
    private bool _isVpsReachable;
    private bool _isRelayReachable;
    private bool _isIdServerReachable;
    private bool _isTesting;
    private string _statusMessage = string.Empty;
    private string _rustDeskStatus = "Unknown";

    public string VpsHost { get => _vpsHost; set => SetProperty(ref _vpsHost, value); }
    public int SshPort { get => _sshPort; set => SetProperty(ref _sshPort, value); }
    public string RelayServer { get => _relayServer; set => SetProperty(ref _relayServer, value); }
    public string IdServer { get => _idServer; set => SetProperty(ref _idServer, value); }
    public int RelayPort { get => _relayPort; set => SetProperty(ref _relayPort, value); }
    public int IdPort { get => _idPort; set => SetProperty(ref _idPort, value); }
    public int ApiPort { get => _apiPort; set => SetProperty(ref _apiPort, value); }
    public bool IsVpsReachable { get => _isVpsReachable; set => SetProperty(ref _isVpsReachable, value); }
    public bool IsRelayReachable { get => _isRelayReachable; set => SetProperty(ref _isRelayReachable, value); }
    public bool IsIdServerReachable { get => _isIdServerReachable; set => SetProperty(ref _isIdServerReachable, value); }
    public bool IsTesting { get => _isTesting; set => SetProperty(ref _isTesting, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string RustDeskStatus { get => _rustDeskStatus; set => SetProperty(ref _rustDeskStatus, value); }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand TestConnectionCommand { get; }
    public AsyncRelayCommand LaunchRustDeskCommand { get; }
    public AsyncRelayCommand LoadCommand { get; }

    public VpsSettingsViewModel(
        IConfigurationService configService,
        IRustDeskService rustDeskService,
        ILogService logService)
    {
        _configService = configService;
        _rustDeskService = rustDeskService;
        _logService = logService;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        LaunchRustDeskCommand = new AsyncRelayCommand(LaunchRustDeskAsync);
        LoadCommand = new AsyncRelayCommand(LoadAsync);

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var config = await _configService.LoadAsync();
        var vps = config.VpsConfig;

        VpsHost = vps.Host;
        SshPort = vps.SshPort;
        RelayServer = vps.RustDeskRelayServer;
        IdServer = vps.RustDeskIdServer;
        RelayPort = vps.RustDeskRelayPort;
        IdPort = vps.RustDeskIdPort;
        ApiPort = vps.RustDeskApiPort;
        IsVpsReachable = vps.IsReachable;
        IsRelayReachable = vps.IsRelayReachable;
        IsIdServerReachable = vps.IsIdServerReachable;

        RustDeskStatus = _rustDeskService.GetStatusDescription();
    }

    private async Task SaveAsync()
    {
        try
        {
            var config = await _configService.LoadAsync();
            config.VpsConfig = new VpsConfiguration
            {
                Host = VpsHost,
                SshPort = SshPort,
                RustDeskRelayServer = RelayServer,
                RustDeskIdServer = IdServer,
                RustDeskRelayPort = RelayPort,
                RustDeskIdPort = IdPort,
                RustDeskApiPort = ApiPort,
                IsConfigured = !string.IsNullOrWhiteSpace(VpsHost),
                IsReachable = IsVpsReachable,
                IsRelayReachable = IsRelayReachable,
                IsIdServerReachable = IsIdServerReachable
            };

            await _configService.SaveAsync(config);
            StatusMessage = "Configuration saved successfully";
            await _logService.LogInfoAsync("VPS configuration saved", category: "VPS");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private async Task TestConnectionAsync()
    {
        IsTesting = true;
        StatusMessage = "Testing connections...";

        try
        {
            // Test VPS host
            if (!string.IsNullOrWhiteSpace(VpsHost))
            {
                IsVpsReachable = await _rustDeskService.TestVpsConnectivityAsync(VpsHost, SshPort);
                StatusMessage = IsVpsReachable ? "VPS reachable" : "VPS unreachable";
            }

            // Test relay server
            var relayHost = !string.IsNullOrWhiteSpace(RelayServer) ? RelayServer : VpsHost;
            if (!string.IsNullOrWhiteSpace(relayHost))
            {
                IsRelayReachable = await _rustDeskService.TestRelayConnectivityAsync(relayHost, RelayPort);
            }

            // Test ID server
            var idHost = !string.IsNullOrWhiteSpace(IdServer) ? IdServer : VpsHost;
            if (!string.IsNullOrWhiteSpace(idHost))
            {
                IsIdServerReachable = await _rustDeskService.TestIdServerConnectivityAsync(idHost, IdPort);
            }

            StatusMessage = $"VPS: {(IsVpsReachable ? "OK" : "FAIL")} | "
                + $"Relay: {(IsRelayReachable ? "OK" : "FAIL")} | "
                + $"ID Server: {(IsIdServerReachable ? "OK" : "FAIL")}";

            await _logService.LogInfoAsync($"Connection test results - {StatusMessage}", category: "VPS");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Test failed: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    private async Task LaunchRustDeskAsync()
    {
        var result = await _rustDeskService.LaunchAsync();
        RustDeskStatus = _rustDeskService.GetStatusDescription();
        StatusMessage = result ? "RustDesk launched" : "Failed to launch RustDesk";
    }
}
