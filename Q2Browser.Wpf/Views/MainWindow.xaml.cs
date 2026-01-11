using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Q2Browser.Wpf.ViewModels;

namespace Q2Browser.Wpf.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Only connect on double-click if it's not on the checkbox column
        if (e.OriginalSource is FrameworkElement element)
        {
            // Check if the click was on a checkbox
            if (element is CheckBox || element.Parent is CheckBox)
            {
                return; // Don't trigger connect when clicking checkbox
            }
        }

        if (DataContext is MainViewModel viewModel && viewModel.SelectedServer != null)
        {
            viewModel.ConnectCommand.Execute(null);
        }
    }

    private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Prevent double-click from triggering when clicking checkbox
        if (e.OriginalSource is FrameworkElement element)
        {
            if (element is CheckBox || element.Parent is CheckBox)
            {
                e.Handled = false; // Allow checkbox to handle the event
            }
        }
    }
}

