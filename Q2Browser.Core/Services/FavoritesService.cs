using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Q2Connect.Core.Models;
using Q2Connect.Core.Protocol;

namespace Q2Connect.Core.Services;

public class FavoritesService
{
    private string _favoritesPath = string.Empty;
    private string _settingsPath = string.Empty;
    private string _addressBookPath = string.Empty;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger? _logger;

    public FavoritesService(ILogger? logger = null)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        // Initialize paths - will be updated after settings are loaded
        UpdatePaths(portableMode: true);
    }

    private void UpdatePaths(bool portableMode)
    {
        if (portableMode)
        {
            // Portable mode: use exe directory
            var exePath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(exePath))
            {
                // Fallback if entry assembly is not available
                exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            }
            
            if (!string.IsNullOrEmpty(exePath))
            {
                var exeDir = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrEmpty(exeDir))
                {
                    _favoritesPath = Path.Combine(exeDir, "favorites.json");
                    _settingsPath = Path.Combine(exeDir, "settings.json");
                    _addressBookPath = Path.Combine(exeDir, "addressbook.json");
                    return;
                }
            }
        }
        
        // AppData mode (fallback or explicit)
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "Q2Connect");
        Directory.CreateDirectory(configDir);
        _favoritesPath = Path.Combine(configDir, "favorites.json");
        _settingsPath = Path.Combine(configDir, "settings.json");
        _addressBookPath = Path.Combine(configDir, "addressbook.json");
    }

    public async Task<List<string>> LoadFavoritesAsync()
    {
        if (!File.Exists(_favoritesPath))
            return new List<string>();

        try
        {
            var json = await File.ReadAllTextAsync(_favoritesPath).ConfigureAwait(false);
            var favorites = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions);
            return favorites ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load favorites from {_favoritesPath}: {ex.Message}", ex.StackTrace);
            return new List<string>();
        }
    }

    public async Task SaveFavoritesAsync(List<string> favorites, bool? portableMode = null)
    {
        // Update paths if portable mode is specified
        if (portableMode.HasValue)
        {
            UpdatePaths(portableMode.Value);
        }
        
        // Ensure directory exists
        var favoritesDir = Path.GetDirectoryName(_favoritesPath);
        if (!string.IsNullOrEmpty(favoritesDir) && !Directory.Exists(favoritesDir))
        {
            Directory.CreateDirectory(favoritesDir);
        }
        
        try
        {
            var json = JsonSerializer.Serialize(favorites, _jsonOptions);
            await File.WriteAllTextAsync(_favoritesPath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to save favorites to {_favoritesPath}: {ex.Message}", ex.StackTrace);
            throw; // Re-throw to allow caller to handle
        }
    }

    public async Task<Settings> LoadSettingsAsync()
    {
        // Try portable location first, then AppData
        var portableSettingsPath = GetPortableSettingsPath();
        var appDataSettingsPath = GetAppDataSettingsPath();
        
        string? settingsPath = null;
        
        if (File.Exists(portableSettingsPath))
        {
            settingsPath = portableSettingsPath;
        }
        else if (File.Exists(appDataSettingsPath))
        {
            settingsPath = appDataSettingsPath;
        }
        
        if (settingsPath == null)
        {
            // No settings file found, return defaults (portable mode = true)
            var defaultSettings = new Settings();
            UpdatePaths(defaultSettings.PortableMode);
            return defaultSettings;
        }

        try
        {
            var json = await File.ReadAllTextAsync(settingsPath);
            var settings = JsonSerializer.Deserialize<Settings>(json, _jsonOptions);
            
            if (settings == null)
            {
                var defaultSettings = new Settings();
                UpdatePaths(defaultSettings.PortableMode);
                return defaultSettings;
            }
            
            // Update paths based on loaded settings
            UpdatePaths(settings.PortableMode);
            
            // Validate and fix invalid URLs if present
            if (!string.IsNullOrWhiteSpace(settings.HttpMasterServerUrl) && !UrlValidator.IsValidHttpUrl(settings.HttpMasterServerUrl))
            {
                _logger?.LogWarning($"Invalid URL in settings file, resetting to default: {settings.HttpMasterServerUrl}");
                // Reset to default if invalid
                settings.HttpMasterServerUrl = "http://q2servers.com/?raw=2";
            }
            
            return settings;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load settings from {settingsPath}: {ex.Message}", ex.StackTrace);
            var defaultSettings = new Settings();
            UpdatePaths(defaultSettings.PortableMode);
            return defaultSettings;
        }
    }

    private static string GetPortableSettingsPath()
    {
        var exePath = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrEmpty(exePath))
        {
            exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        }
        
        if (!string.IsNullOrEmpty(exePath))
        {
            var exeDir = Path.GetDirectoryName(exePath);
            if (!string.IsNullOrEmpty(exeDir))
            {
                return Path.Combine(exeDir, "settings.json");
            }
        }
        
        return string.Empty;
    }

    private static string GetAppDataSettingsPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "Q2Connect");
        return Path.Combine(configDir, "settings.json");
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        // Update paths based on portable mode setting
        UpdatePaths(settings.PortableMode);
        
        // Ensure directory exists
        var settingsDir = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
        {
            Directory.CreateDirectory(settingsDir);
        }
        
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
            
            // If switching modes, optionally clean up old location
            // (We'll leave the old file in case user wants to switch back)
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to save settings to {_settingsPath}: {ex.Message}", ex.StackTrace);
            throw; // Re-throw to allow caller to handle
        }
    }

    public async Task<List<AddressBookEntry>> LoadAddressBookAsync()
    {
        if (!File.Exists(_addressBookPath))
            return new List<AddressBookEntry>();

        try
        {
            var json = await File.ReadAllTextAsync(_addressBookPath).ConfigureAwait(false);
            var entries = JsonSerializer.Deserialize<List<AddressBookEntry>>(json, _jsonOptions);
            return entries ?? new List<AddressBookEntry>();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load address book from {_addressBookPath}: {ex.Message}", ex.StackTrace);
            return new List<AddressBookEntry>();
        }
    }

    public async Task SaveAddressBookAsync(List<AddressBookEntry> entries, bool? portableMode = null)
    {
        // Update paths if portable mode is specified
        if (portableMode.HasValue)
        {
            UpdatePaths(portableMode.Value);
        }
        
        // Ensure directory exists
        var addressBookDir = Path.GetDirectoryName(_addressBookPath);
        if (!string.IsNullOrEmpty(addressBookDir) && !Directory.Exists(addressBookDir))
        {
            Directory.CreateDirectory(addressBookDir);
        }
        
        try
        {
            var json = JsonSerializer.Serialize(entries, _jsonOptions);
            await File.WriteAllTextAsync(_addressBookPath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to save address book to {_addressBookPath}: {ex.Message}", ex.StackTrace);
            throw; // Re-throw to allow caller to handle
        }
    }
}



