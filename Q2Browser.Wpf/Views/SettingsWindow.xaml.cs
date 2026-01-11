using System.Windows;
using Q2Browser.Wpf.ViewModels;

namespace Q2Browser.Wpf.Views;

public partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow()
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel();
        DataContext = ViewModel;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

