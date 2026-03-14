using System.Windows;
using MobileControlHub.UI.ViewModels;

namespace MobileControlHub.UI;

/// <summary>
/// Main application window with sidebar navigation.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
