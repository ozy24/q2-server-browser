using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

        // Sanitize address and port to prevent command injection
        var sanitizedAddress = SanitizeAddress(server.Address);
        var arguments = $"+connect {sanitizedAddress}:{server.Port}";

        var workingDir = Path.GetDirectoryName(_settings.Q2ExecutablePath);
        if (string.IsNullOrEmpty(workingDir))
        {
            throw new InvalidOperationException("Cannot determine working directory for Quake 2 executable");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.Q2ExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = workingDir
        };

        try
        {
            var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Quake 2 process");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Failed to launch Quake 2: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error launching Quake 2: {ex.Message}", ex);
        }
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

        // Sanitize address to prevent command injection
        var sanitizedAddress = SanitizeAddress(address);
        var arguments = $"+connect {sanitizedAddress}";

        var workingDir = Path.GetDirectoryName(_settings.Q2ExecutablePath);
        if (string.IsNullOrEmpty(workingDir))
        {
            throw new InvalidOperationException("Cannot determine working directory for Quake 2 executable");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.Q2ExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = workingDir
        };

        try
        {
            var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Quake 2 process");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Failed to launch Quake 2: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error launching Quake 2: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sanitizes an address string to prevent command injection.
    /// Only allows alphanumeric characters, dots, colons, hyphens, and brackets (for IPv6).
    /// </summary>
    private static string SanitizeAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Address cannot be null or empty", nameof(address));
        }

        // Remove any characters that could be used for command injection
        // Allow: alphanumeric, dots (.), colons (:), hyphens (-), brackets ([] for IPv6)
        var sanitized = Regex.Replace(address, @"[^a-zA-Z0-9.:\[\]-]", string.Empty);
        
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException("Address contains only invalid characters", nameof(address));
        }

        return sanitized;
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

