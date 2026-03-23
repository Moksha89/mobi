using System.Windows.Input;
using MobileControlHub.UI.ViewModels.Base;

namespace MobileControlHub.UI.ViewModels;

/// <summary>
/// Main ViewModel that manages navigation between pages via the sidebar.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private ViewModelBase _currentPage = null!;
    private string _currentPageTitle = "Dashboard";

    public DashboardViewModel DashboardVm { get; }
    public DevicesViewModel DevicesVm { get; }
    public SessionsViewModel SessionsVm { get; }
    public VpsSettingsViewModel VpsSettingsVm { get; }
    public LogsViewModel LogsVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        set => SetProperty(ref _currentPageTitle, value);
    }

    public ICommand NavigateCommand { get; }

    public MainViewModel(
        DashboardViewModel dashboardVm,
        DevicesViewModel devicesVm,
        SessionsViewModel sessionsVm,
        VpsSettingsViewModel vpsSettingsVm,
        LogsViewModel logsVm,
        SettingsViewModel settingsVm)
    {
        DashboardVm = dashboardVm;
        DevicesVm = devicesVm;
        SessionsVm = sessionsVm;
        VpsSettingsVm = vpsSettingsVm;
        LogsVm = logsVm;
        SettingsVm = settingsVm;

        NavigateCommand = new RelayCommand(NavigateTo);
        CurrentPage = DashboardVm;
    }

    private void NavigateTo(object? parameter)
    {
        var page = parameter?.ToString();
        switch (page)
        {
            case "Dashboard":
                CurrentPage = DashboardVm;
                CurrentPageTitle = "Dashboard";
                break;
            case "Devices":
                CurrentPage = DevicesVm;
                CurrentPageTitle = "Device Manager";
                break;
            case "Sessions":
                CurrentPage = SessionsVm;
                CurrentPageTitle = "scrcpy Sessions";
                break;
            case "VPS":
                CurrentPage = VpsSettingsVm;
                CurrentPageTitle = "VPS / Remote Access";
                break;
            case "Logs":
                CurrentPage = LogsVm;
                CurrentPageTitle = "Log Viewer";
                _ = LogsVm.RefreshLogsAsync();
                break;
            case "Settings":
                CurrentPage = SettingsVm;
                CurrentPageTitle = "Settings";
                _ = SettingsVm.LoadSettingsAsync();
                break;
        }
    }

    /// <summary>
    /// Initialize all child ViewModels and start monitoring.
    /// </summary>
    public async Task InitializeAsync()
    {
        await DashboardVm.InitializeAsync();
        await DevicesVm.InitializeAsync();
    }
}
