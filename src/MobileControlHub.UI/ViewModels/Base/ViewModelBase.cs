using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace MobileControlHub.UI.ViewModels.Base;

/// <summary>
/// Base class for all ViewModels. Provides INotifyPropertyChanged
/// and helper methods for UI thread dispatch.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Dispatch an action to the UI thread.
    /// </summary>
    protected static void DispatchToUI(Action action)
    {
        if (Application.Current?.Dispatcher is Dispatcher dispatcher)
        {
            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Dispatch an action to the UI thread asynchronously.
    /// </summary>
    protected static async Task DispatchToUIAsync(Action action)
    {
        if (Application.Current?.Dispatcher is Dispatcher dispatcher)
        {
            if (dispatcher.CheckAccess())
                action();
            else
                await dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }
}
