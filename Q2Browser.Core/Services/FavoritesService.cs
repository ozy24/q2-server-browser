using System.Text.Json;
using Q2Browser.Core.Models;

namespace Q2Browser.Core.Services;

public class FavoritesService
{
    private readonly string _favoritesPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public FavoritesService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "Q2ServerBrowser");
        Directory.CreateDirectory(configDir);
        _favoritesPath = Path.Combine(configDir, "favorites.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    public async Task<List<string>> LoadFavoritesAsync()
    {
        if (!File.Exists(_favoritesPath))
            return new List<string>();

        try
        {
            var json = await File.ReadAllTextAsync(_favoritesPath);
            var favorites = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions);
            return favorites ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task SaveFavoritesAsync(List<string> favorites)
    {
        try
        {
            var json = JsonSerializer.Serialize(favorites, _jsonOptions);
            await File.WriteAllTextAsync(_favoritesPath, json);
        }
        catch
        {
            // Log error if needed
        }
    }

    public async Task<Settings> LoadSettingsAsync()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "Q2ServerBrowser");
        var settingsPath = Path.Combine(configDir, "settings.json");
        
        if (!File.Exists(settingsPath))
            return new Settings();

        try
        {
            var json = await File.ReadAllTextAsync(settingsPath);
            var settings = JsonSerializer.Deserialize<Settings>(json, _jsonOptions);
            return settings ?? new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "Q2ServerBrowser");
        Directory.CreateDirectory(configDir);
        var settingsPath = Path.Combine(configDir, "settings.json");
        
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(settingsPath, json);
        }
        catch
        {
            // Log error if needed
        }
    }
}

