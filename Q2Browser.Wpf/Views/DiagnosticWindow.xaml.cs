using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Q2Browser.Wpf.Services;

namespace Q2Browser.Wpf.Views;

    public partial class DiagnosticWindow : Window
    {
        public DiagnosticWindow()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("DiagnosticWindow: Starting constructor...");
                
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("DiagnosticWindow: InitializeComponent complete");
                
                DataContext = DiagnosticLogger.Instance;
                System.Diagnostics.Debug.WriteLine("DiagnosticWindow: DataContext set");
                
                Loaded += DiagnosticWindow_Loaded;
                System.Diagnostics.Debug.WriteLine("DiagnosticWindow: Constructor complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DiagnosticWindow constructor error: {ex}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    DiagnosticLogger.Instance.LogError($"DiagnosticWindow constructor failed: {ex.Message}", ex.ToString());
                }
                catch
                {
                    // Can't even log, write to debug
                }
                
                throw; // Re-throw so we can see it
            }
        }

        private void DiagnosticWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("DiagnosticWindow: Loaded event fired");
                
                // Auto-scroll to bottom when new entries are added
                DiagnosticLogger.Instance.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
                System.Diagnostics.Debug.WriteLine("DiagnosticWindow: CollectionChanged handler attached");
                
                // Scroll to bottom on initial load
                if (LogDataGrid != null && LogDataGrid.Items.Count > 0)
                {
                    LogDataGrid.ScrollIntoView(LogDataGrid.Items[LogDataGrid.Items.Count - 1]);
                    System.Diagnostics.Debug.WriteLine("DiagnosticWindow: Scrolled to bottom");
                }
                
                System.Diagnostics.Debug.WriteLine("DiagnosticWindow: Loaded event complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DiagnosticWindow_Loaded error: {ex}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    DiagnosticLogger.Instance.LogError($"DiagnosticWindow Loaded failed: {ex.Message}", ex.ToString());
                }
                catch
                {
                    // Can't log
                }
            }
        }

        private void LogEntries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null && e.NewItems.Count > 0 && LogDataGrid != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (LogDataGrid.Items.Count > 0)
                        {
                            LogDataGrid.ScrollIntoView(LogDataGrid.Items[LogDataGrid.Items.Count - 1]);
                        }
                    }
                    catch
                    {
                        // Ignore errors during scrolling
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void LogDataGrid_LoadingRow(object? sender, System.Windows.Controls.DataGridRowEventArgs e)
        {
            // Auto-scroll to bottom on load
            try
            {
                if (e.Row != null && LogDataGrid != null && e.Row.GetIndex() == LogDataGrid.Items.Count - 1)
                {
                    LogDataGrid.ScrollIntoView(e.Row.Item);
                }
            }
            catch
            {
                // Ignore errors during scrolling
            }
        }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticLogger.Instance.Clear();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logEntries = DiagnosticLogger.Instance.LogEntries;
            var lines = logEntries.Select(entry => 
                $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] {entry.Message}" +
                (string.IsNullOrEmpty(entry.Details) ? "" : $" | {entry.Details}")
            );

            var logText = string.Join(Environment.NewLine, lines);
            
            if (string.IsNullOrEmpty(logText))
            {
                MessageBox.Show("Log is empty. Nothing to copy.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Clipboard.SetText(logText);
            MessageBox.Show($"Copied {logEntries.Count} log entries to clipboard.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error copying log to clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = "txt",
            FileName = $"q2browser-log-{System.DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var logEntries = DiagnosticLogger.Instance.LogEntries;
                var lines = logEntries.Select(entry => 
                    $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] {entry.Message}" +
                    (string.IsNullOrEmpty(entry.Details) ? "" : $" | {entry.Details}")
                );

                File.WriteAllLines(dialog.FileName, lines);
                MessageBox.Show($"Log saved to {dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving log: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

