using System;
using System.Windows;
using System.Windows.Threading;
using Q2Browser.Wpf.Services;

namespace Q2Browser.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Global exception handlers
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        
        try
        {
            DiagnosticLogger.Instance.LogInfo("Application starting...");
            
            // Register URI scheme (requires admin on first run)
            LauncherService.RegisterUriScheme();
            
            DiagnosticLogger.Instance.LogInfo("Application startup complete");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Instance.LogError($"Error during startup: {ex.Message}", ex.ToString());
        }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var message = exception?.ToString() ?? e.ExceptionObject?.ToString() ?? "Unknown error";
        
        DiagnosticLogger.Instance.LogError($"Unhandled Exception (Domain): {exception?.Message ?? "Unknown"}", message);
        
        try
        {
            MessageBox.Show(
                $"An unhandled error occurred:\n\n{exception?.Message ?? "Unknown error"}\n\nCheck the Diagnostics window for details.",
                "Application Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // If we can't show a message box, at least log it
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DiagnosticLogger.Instance.LogError($"Unhandled Exception (Dispatcher): {e.Exception.Message}", e.Exception.ToString());
        
        try
        {
            var result = MessageBox.Show(
                $"An error occurred:\n\n{e.Exception.Message}\n\nCheck the Diagnostics window for details.\n\nDo you want to continue?",
                "Application Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);
            
            e.Handled = result == MessageBoxResult.Yes;
        }
        catch
        {
            e.Handled = false; // Let it crash if we can't handle it
        }
    }
}

