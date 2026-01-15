using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Q2Connect.Core.Models;

namespace Q2Connect.Wpf.Services;

public class LauncherService
{
    private readonly Settings _settings;

    public LauncherService(Settings settings)
    {
        _settings = settings;
    }

    public void LaunchGame(ServerEntry server)
    {
        if (string.IsNullOrEmpty(_settings.Q2ExecutablePath))
        {
            throw new InvalidOperationException("Quake 2 executable path is not configured");
        }

        if (!File.Exists(_settings.Q2ExecutablePath))
        {
            throw new FileNotFoundException($"Quake 2 executable not found: {_settings.Q2ExecutablePath}");
        }

        var arguments = $"+connect {server.Address}:{server.Port}";

        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.Q2ExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(_settings.Q2ExecutablePath)
        };

        Process.Start(startInfo);
    }

    public void LaunchGameWithAddress(string address)
    {
        if (string.IsNullOrEmpty(_settings.Q2ExecutablePath))
        {
            throw new InvalidOperationException("Quake 2 executable path is not configured");
        }

        if (!File.Exists(_settings.Q2ExecutablePath))
        {
            throw new FileNotFoundException($"Quake 2 executable not found: {_settings.Q2ExecutablePath}");
        }

        // Pass the address directly to Quake 2 - it will handle parsing
        var arguments = $"+connect {address}";

        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.Q2ExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(_settings.Q2ExecutablePath)
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

