using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Q2Browser.Core.Models;

namespace Q2Browser.Wpf.Services;

public class LauncherService
{
    private readonly Settings _settings;

    public LauncherService(Settings settings)
    {
        _settings = settings;
    }

    public void LaunchGame(ServerEntry server)
    {
        if (string.IsNullOrEmpty(_settings.Q2ProExecutablePath))
        {
            throw new InvalidOperationException("Q2Pro executable path is not configured");
        }

        if (!File.Exists(_settings.Q2ProExecutablePath))
        {
            throw new FileNotFoundException($"Q2Pro executable not found: {_settings.Q2ProExecutablePath}");
        }

        var arguments = $"+connect {server.Address}:{server.Port}";

        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.Q2ProExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(_settings.Q2ProExecutablePath)
        };

        Process.Start(startInfo);
    }

    public static void RegisterUriScheme()
    {
        // Register quake2:// URI scheme in Windows Registry
        // This requires admin privileges
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath)) return;

        try
        {
            using var key = Registry.ClassesRoot.CreateSubKey("quake2");
            key.SetValue("", "URL:Quake II Protocol");
            key.SetValue("URL Protocol", "");

            using var commandKey = key.CreateSubKey(@"shell\open\command");
            commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
        }
        catch (UnauthorizedAccessException)
        {
            // Admin rights required - silently fail
        }
    }
}

