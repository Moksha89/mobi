using System.Collections.ObjectModel;
using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.UI.ViewModels.Base;

namespace MobileControlHub.UI.ViewModels;

/// <summary>
/// ViewModel for the Log Viewer page with filtering and real-time updates.
/// </summary>
public class LogsViewModel : ViewModelBase
{
    private readonly ILogService _logService;

    private AppLogLevel? _selectedLevel;
    private string _categoryFilter = string.Empty;
    private string _deviceFilter = string.Empty;
    private bool _autoScroll = true;
    private int _logCount;

    public ObservableCollection<LogEntry> Logs { get; } = new();

    public AppLogLevel? SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            if (SetProperty(ref _selectedLevel, value))
                _ = RefreshLogsAsync();
        }
    }

    public string CategoryFilter
    {
        get => _categoryFilter;
        set
        {
            if (SetProperty(ref _categoryFilter, value))
                _ = RefreshLogsAsync();
        }
    }

    public string DeviceFilter
    {
        get => _deviceFilter;
        set
        {
            if (SetProperty(ref _deviceFilter, value))
                _ = RefreshLogsAsync();
        }
    }

    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    public int LogCount
    {
        get => _logCount;
        set => SetProperty(ref _logCount, value);
    }

    // Filter preset commands
    public RelayCommand ShowAllCommand { get; }
    public RelayCommand ShowInfoCommand { get; }
    public RelayCommand ShowWarningsCommand { get; }
    public RelayCommand ShowErrorsCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ClearLogsCommand { get; }

    public LogsViewModel(ILogService logService)
    {
        _logService = logService;

        ShowAllCommand = new RelayCommand(() => SelectedLevel = null);
        ShowInfoCommand = new RelayCommand(() => SelectedLevel = AppLogLevel.Info);
        ShowWarningsCommand = new RelayCommand(() => SelectedLevel = AppLogLevel.Warning);
        ShowErrorsCommand = new RelayCommand(() => SelectedLevel = AppLogLevel.Error);
        RefreshCommand = new AsyncRelayCommand(RefreshLogsAsync);
        ClearLogsCommand = new AsyncRelayCommand(ClearLogsAsync);

        // Subscribe to real-time log updates
        _logService.LogEntryAdded += OnLogEntryAdded;
    }

    public async Task RefreshLogsAsync()
    {
        var logs = await _logService.GetLogsAsync(
            minLevel: _selectedLevel,
            category: string.IsNullOrWhiteSpace(_categoryFilter) ? null : _categoryFilter,
            deviceSerial: string.IsNullOrWhiteSpace(_deviceFilter) ? null : _deviceFilter,
            limit: 500);

        DispatchToUI(() =>
        {
            Logs.Clear();
            foreach (var log in logs)
                Logs.Add(log);
            LogCount = Logs.Count;
        });
    }

    private async Task ClearLogsAsync()
    {
        await _logService.ClearLogsAsync();
        await RefreshLogsAsync();
    }

    private void OnLogEntryAdded(object? sender, LogEntry entry)
    {
        // Check if entry matches current filters
        if (_selectedLevel.HasValue && entry.Level < _selectedLevel.Value)
            return;

        if (!string.IsNullOrWhiteSpace(_categoryFilter)
            && !entry.Category.Contains(_categoryFilter, StringComparison.OrdinalIgnoreCase))
            return;

        if (!string.IsNullOrWhiteSpace(_deviceFilter)
            && entry.DeviceSerial != _deviceFilter)
            return;

        DispatchToUI(() =>
        {
            Logs.Insert(0, entry);
            LogCount = Logs.Count;

            // Keep list size manageable
            while (Logs.Count > 500)
                Logs.RemoveAt(Logs.Count - 1);
        });
    }
}
