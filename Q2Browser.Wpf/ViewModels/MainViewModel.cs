using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Q2Browser.Core.Models;
using Q2Browser.Core.Networking;
using Q2Browser.Core.Services;
using Q2Browser.Wpf.Services;

namespace Q2Browser.Wpf.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private MasterServerClient? _masterServerClient;
    private HttpMasterServerClient? _httpMasterServerClient;
    private LanBroadcastClient? _lanBroadcastClient;
    private GameServerProbe? _gameServerProbe;
    private readonly FavoritesService _favoritesService;
    private LauncherService? _launcherService;
    private readonly ThrottledObservableCollection<ServerRowViewModel> _servers;
    private readonly ListCollectionView _serversView;
    private readonly ObservableCollection<PlayerInfo> _players = new();
    private readonly ListCollectionView _playersView;
    private readonly HashSet<string> _favoriteAddresses = new();
    private Settings _currentSettings = new();

    private string _searchText = string.Empty;
    private bool _isRefreshing;
    private string _statusText = "Ready";
    private int _serversFound;
    private bool _includePlayerNamesInSearch;
    private CancellationTokenSource? _refreshCancellation;
    private bool _isInitialized;

    public MainViewModel()
    {
        var logger = new CoreLoggerAdapter();
        _favoritesService = new FavoritesService(logger);
        _servers = new ThrottledObservableCollection<ServerRowViewModel>(150);
        
        // Create a ListCollectionView for sorting and filtering
        _serversView = new ListCollectionView(_servers);
        
        // Set default sort: favorites first, then by player count descending
        _serversView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
        _serversView.SortDescriptions.Add(new SortDescription("CurrentPlayers", ListSortDirection.Descending));
        
        // Initialize filter (initially no filter - shows all servers)
        UpdateFilter();
        
        // Create a ListCollectionView for players with sorting by score descending
        _playersView = new ListCollectionView(_players);
        var playerSortDescription = new SortDescription("Score", ListSortDirection.Descending);
        _playersView.SortDescriptions.Add(playerSortDescription);
        
        RefreshCommand = new RelayCommand(async _ => await RefreshServersAsync(), _ => !IsRefreshing && _isInitialized);
        ConnectCommand = new RelayCommand(ConnectToServer, _ => SelectedServer != null);
        ToggleFavoriteCommand = new RelayCommand(async _ => await ToggleFavoriteAsync(), _ => SelectedServer != null);
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        OpenLogCommand = new RelayCommand(_ => OpenLog());
        OpenAboutCommand = new RelayCommand(_ => OpenAbout());
        CopyServerNameCommand = new RelayCommand(CopyServerName, _ => SelectedServer != null);
        CopyIpAddressCommand = new RelayCommand(CopyIpAddress, _ => SelectedServer != null);
        CopyServerDetailsCommand = new RelayCommand(CopyServerDetails, _ => SelectedServer != null);
        
        // Initialize asynchronously with proper error handling
        _ = InitializeAsync().ContinueWith(task =>
        {
            if (task.IsFaulted && task.Exception != null)
            {
                DiagnosticLogger.Instance.LogError($"Initialization failed: {task.Exception.GetBaseException().Message}", 
                    task.Exception.ToString());
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusText = $"Initialization error: {task.Exception.GetBaseException().Message}";
                });
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }


    private async Task InitializeAsync()
    {
        var logger = new CoreLoggerAdapter();
        DiagnosticLogger.Instance.LogInfo("Application initializing...");
        
        _currentSettings = await _favoritesService.LoadSettingsAsync();
        
        // Set the minimum log level based on settings
        DiagnosticLogger.Instance.SetMinimumLogLevel(_currentSettings.LogLevel);
        
        DiagnosticLogger.Instance.LogInfo($"Loaded settings: Master={_currentSettings.MasterServerAddress}:{_currentSettings.MasterServerPort}");
        
        
        _masterServerClient = new MasterServerClient(_currentSettings, logger);
        _httpMasterServerClient = new HttpMasterServerClient(_currentSettings, logger);
        _lanBroadcastClient = new LanBroadcastClient(_currentSettings, logger);
        _gameServerProbe = new GameServerProbe(_currentSettings, logger);
        _launcherService = new LauncherService(_currentSettings);

        var favorites = await _favoritesService.LoadFavoritesAsync();
        foreach (var fav in favorites)
        {
            _favoriteAddresses.Add(fav);
        }

        _isInitialized = true;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // Initialize search settings
            IncludePlayerNamesInSearch = _currentSettings.IncludePlayerNamesInSearch;
            
            ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            
            // Auto-refresh on startup if enabled
            if (_currentSettings.RefreshOnStartup)
            {
                StatusText = "Ready - Auto-refreshing servers...";
                _ = RefreshServersAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted && task.Exception != null)
                    {
                        DiagnosticLogger.Instance.LogError($"Auto-refresh failed: {task.Exception.GetBaseException().Message}", 
                            task.Exception.ToString());
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
            else
            {
                StatusText = "Ready - Click Refresh to query servers";
            }
        });
        
        DiagnosticLogger.Instance.LogInfo("Initialization complete");
    }

    public async Task ReloadSettingsAsync()
    {
        var logger = new CoreLoggerAdapter();
        _currentSettings = await _favoritesService.LoadSettingsAsync();
        
        // Update the minimum log level based on settings
        DiagnosticLogger.Instance.SetMinimumLogLevel(_currentSettings.LogLevel);
        
        // Dispose old HTTP client before creating new one
        _httpMasterServerClient?.Dispose();
        
        _masterServerClient = new MasterServerClient(_currentSettings, logger);
        _httpMasterServerClient = new HttpMasterServerClient(_currentSettings, logger);
        _lanBroadcastClient = new LanBroadcastClient(_currentSettings, logger);
        _gameServerProbe = new GameServerProbe(_currentSettings, logger);
        _launcherService = new LauncherService(_currentSettings);
        
        // Update search settings on UI thread
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IncludePlayerNamesInSearch = _currentSettings.IncludePlayerNamesInSearch;
        });

    }

    private void OpenSettings()
    {
        var settingsWindow = new Views.SettingsWindow
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (settingsWindow.ShowDialog() == true)
        {
            _ = ReloadSettingsAsync();
            StatusText = "Settings saved. Click Refresh to apply.";
        }
    }

    private void OpenLog()
    {
        try
        {
            DiagnosticLogger.Instance.LogInfo("Opening log window...");
            
            var logWindow = new Views.LogWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            
            DiagnosticLogger.Instance.LogDebug("Log window created, showing...");
            logWindow.Show();
            DiagnosticLogger.Instance.LogInfo("Log window opened successfully");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Instance.LogError($"Failed to open log window: {ex.Message}", ex.ToString());
            System.Windows.MessageBox.Show(
                $"Error opening log window:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OpenAbout()
    {
        var aboutWindow = new Views.AboutWindow
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        aboutWindow.ShowDialog();
    }

    public ICollectionView Servers => _serversView;

    private ServerRowViewModel? _selectedServer;
    public ServerRowViewModel? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (_selectedServer != value)
            {
                // Unsubscribe from old server's property changes
                if (_selectedServer != null)
                {
                    _selectedServer.PropertyChanged -= SelectedServer_PropertyChanged;
                }
                
                _selectedServer = value;
                
                // Subscribe to new server's property changes
                if (_selectedServer != null)
                {
                    _selectedServer.PropertyChanged += SelectedServer_PropertyChanged;
                }
                
                OnPropertyChanged();
                OnPropertyChanged(nameof(ToggleFavoriteText));
                OnPropertyChanged(nameof(ToggleFavoriteMenuText));
                UpdatePlayers();
                ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ToggleFavoriteCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CopyServerNameCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CopyIpAddressCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CopyServerDetailsCommand).RaiseCanExecuteChanged();
            }
        }
    }

    private void SelectedServer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServerRowViewModel.IsFavorite))
        {
            OnPropertyChanged(nameof(ToggleFavoriteText));
        }
    }

    public string ToggleFavoriteText => SelectedServer?.IsFavorite == true ? "Unfavorite" : "Favorite";
    public string ToggleFavoriteMenuText => SelectedServer?.IsFavorite == true ? "Remove from Favorites" : "Add to Favorites";

    public ICollectionView Players => _playersView;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                UpdateFilter();
            }
        }
    }

    public bool IncludePlayerNamesInSearch
    {
        get => _includePlayerNamesInSearch;
        set
        {
            if (_includePlayerNamesInSearch != value)
            {
                _includePlayerNamesInSearch = value;
                _currentSettings.IncludePlayerNamesInSearch = value;
                OnPropertyChanged();
                UpdateFilter();
                
                // Save settings asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _favoritesService.SaveSettingsAsync(_currentSettings);
                    }
                    catch (Exception ex)
                    {
                        DiagnosticLogger.Instance.LogError($"Failed to save settings: {ex.Message}", ex.ToString());
                    }
                });
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing != value)
            {
                _isRefreshing = value;
                OnPropertyChanged();
                ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public int ServersFound
    {
        get => _serversFound;
        set
        {
            if (_serversFound != value)
            {
                _serversFound = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand OpenAboutCommand { get; }
    public ICommand CopyServerNameCommand { get; }
    public ICommand CopyIpAddressCommand { get; }
    public ICommand CopyServerDetailsCommand { get; }

    private async Task RefreshServersAsync()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = new CancellationTokenSource();

        DiagnosticLogger.Instance.LogInfo("=== Starting server refresh ===");
        IsRefreshing = true;
        StatusText = "Querying master server...";
        _servers.Clear();
        ServersFound = 0;

        try
        {
            if (_gameServerProbe == null)
            {
                StatusText = "Initializing... Please wait.";
                await Task.Delay(1000, _refreshCancellation.Token);
                if (_gameServerProbe == null)
                {
                    StatusText = "Error: Services not initialized";
                    return;
                }
            }

            var serverEndPoints = new List<IPEndPoint>();

            // Try HTTP master server first if configured
            if (_currentSettings.UseHttpMasterServer && _httpMasterServerClient != null && 
                !string.IsNullOrEmpty(_currentSettings.HttpMasterServerUrl))
            {
                DiagnosticLogger.Instance.LogInfo("Querying HTTP master server...");
                var httpServers = await _httpMasterServerClient.QueryServersAsync(_refreshCancellation.Token);
                serverEndPoints.AddRange(httpServers);
                DiagnosticLogger.Instance.LogInfo($"HTTP master server returned {httpServers.Count} server(s)");
            }

            // Try UDP master server as fallback or if HTTP not configured
            if (!_currentSettings.UseHttpMasterServer && _masterServerClient != null)
            {
                DiagnosticLogger.Instance.LogInfo("Querying UDP master server...");
                var udpServers = await _masterServerClient.QueryServersAsync(_refreshCancellation.Token);
                serverEndPoints.AddRange(udpServers);
                DiagnosticLogger.Instance.LogInfo($"UDP master server returned {udpServers.Count} server(s)");
            }

            // Add LAN broadcast discovery if enabled
            if (_currentSettings.EnableLanBroadcast && _lanBroadcastClient != null)
            {
                DiagnosticLogger.Instance.LogInfo("Discovering LAN servers...");
                var lanServers = await _lanBroadcastClient.DiscoverServersAsync(_refreshCancellation.Token);
                serverEndPoints.AddRange(lanServers);
                DiagnosticLogger.Instance.LogInfo($"LAN broadcast discovered {lanServers.Count} server(s)");
            }

            // Remove duplicates
            var uniqueServers = serverEndPoints
                .GroupBy(s => $"{s.Address}:{s.Port}")
                .Select(g => g.First())
                .ToList();

            StatusText = $"Found {uniqueServers.Count} servers. Probing...";

            var progress = new Progress<ServerEntry>(entry =>
            {
                var existing = _servers.FirstOrDefault(s => s.FullAddress == entry.FullAddress);
                if (existing != null)
                {
                    _servers.Remove(existing);
                }

                entry.IsFavorite = _favoriteAddresses.Contains(entry.FullAddress);
                var viewModel = new ServerRowViewModel(entry);
                _servers.AddThrottled(viewModel);
                ServersFound = _servers.Count;
            });

            await _gameServerProbe.ProbeServersAsync(
                uniqueServers,
                progress,
                _refreshCancellation.Token
            );

            StatusText = $"Found {ServersFound} active servers";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Refresh cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void UpdateFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            _serversView.Filter = null;
        }
        else
        {
            // Check if search text is wrapped in quotes for exact matching
            var trimmedSearch = SearchText.Trim();
            bool isExactMatch = false;
            string searchTerm = trimmedSearch;
            
            if (trimmedSearch.Length >= 2 &&
                ((trimmedSearch.StartsWith('"') && trimmedSearch.EndsWith('"')) ||
                 (trimmedSearch.StartsWith('\'') && trimmedSearch.EndsWith('\''))))
            {
                // Extract text between quotes
                isExactMatch = true;
                searchTerm = trimmedSearch.Substring(1, trimmedSearch.Length - 2).Trim();
            }
            
            // If search term is empty after processing, show all
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _serversView.Filter = null;
                return;
            }
            
            var searchLower = searchTerm.ToLowerInvariant();
            
            _serversView.Filter = item =>
            {
                if (item is ServerRowViewModel server)
                {
                    // Helper function to check if text matches
                    Func<string, string, bool> matches = (text, search) =>
                    {
                        var textLower = text.ToLowerInvariant();
                        return isExactMatch 
                            ? textLower == search 
                            : textLower.Contains(search);
                    };
                    
                    // Search in server properties
                    if (matches(server.Hostname, searchLower) ||
                        matches(server.Map, searchLower) ||
                        matches(server.Mod, searchLower))
                    {
                        return true;
                    }
                    
                    // Search in player names if enabled
                    if (IncludePlayerNamesInSearch)
                    {
                        if (isExactMatch)
                        {
                            if (server.ServerEntry.Players.Any(player => 
                                player.Name.ToLowerInvariant() == searchLower))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            if (server.ServerEntry.Players.Any(player => 
                                player.Name.ToLowerInvariant().Contains(searchLower)))
                            {
                                return true;
                            }
                        }
                    }
                    
                    return false;
                }
                return false;
            };
        }
    }

    private void ConnectToServer(object? parameter)
    {
        if (SelectedServer == null || _launcherService == null) return;
        
        try
        {
            _launcherService.LaunchGame(SelectedServer.ServerEntry);
        }
        catch (InvalidOperationException ex)
        {
            System.Windows.MessageBox.Show(
                $"Cannot connect to server:\n\n{ex.Message}\n\nPlease configure the Quake 2 executable path in Settings.",
                "Configuration Required",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch (FileNotFoundException ex)
        {
            System.Windows.MessageBox.Show(
                $"Cannot connect to server:\n\n{ex.Message}\n\nPlease check the Quake 2 executable path in Settings.",
                "File Not Found",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error launching game:\n\n{ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            DiagnosticLogger.Instance.LogError($"Error launching game: {ex.Message}", ex.ToString());
        }
    }

    private async Task ToggleFavoriteAsync()
    {
        if (SelectedServer == null) return;

        SelectedServer.IsFavorite = !SelectedServer.IsFavorite;
        
        if (SelectedServer.IsFavorite)
        {
            _favoriteAddresses.Add(SelectedServer.FullAddress);
        }
        else
        {
            _favoriteAddresses.Remove(SelectedServer.FullAddress);
        }

        try
        {
            await _favoritesService.SaveFavoritesAsync(_favoriteAddresses.ToList(), _currentSettings.PortableMode);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Instance.LogError($"Failed to save favorites: {ex.Message}", ex.ToString());
            // Continue execution - favorites are still in memory
        }
        
        // Refresh the view to update sorting
        _serversView.Refresh();
    }

    private void CopyServerName(object? parameter)
    {
        if (SelectedServer == null) return;
        
        try
        {
            Clipboard.SetText(SelectedServer.Hostname);
            StatusText = $"Copied server name: {SelectedServer.Hostname}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error copying server name: {ex.Message}";
            DiagnosticLogger.Instance.LogError($"Error copying server name: {ex.Message}", ex.ToString());
        }
    }

    private void CopyIpAddress(object? parameter)
    {
        if (SelectedServer == null) return;
        
        try
        {
            Clipboard.SetText(SelectedServer.FullAddress);
            StatusText = $"Copied IP address: {SelectedServer.FullAddress}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error copying IP address: {ex.Message}";
            DiagnosticLogger.Instance.LogError($"Error copying IP address: {ex.Message}", ex.ToString());
        }
    }

    private void CopyServerDetails(object? parameter)
    {
        if (SelectedServer == null) return;
        
        try
        {
            var details = $"Server Name: {SelectedServer.Hostname}\n" +
                         $"Address: {SelectedServer.FullAddress}\n" +
                         $"Map: {SelectedServer.Map}\n" +
                         $"Mod: {SelectedServer.Mod}\n" +
                         $"Players: {SelectedServer.PlayersText}\n" +
                         $"Ping: {SelectedServer.PingText}";
            
            Clipboard.SetText(details);
            StatusText = "Copied server details to clipboard";
        }
        catch (Exception ex)
        {
            StatusText = $"Error copying server details: {ex.Message}";
            DiagnosticLogger.Instance.LogError($"Error copying server details: {ex.Message}", ex.ToString());
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void UpdatePlayers()
    {
        _players.Clear();
        if (SelectedServer?.ServerEntry?.Players != null)
        {
            foreach (var player in SelectedServer.ServerEntry.Players)
            {
                _players.Add(player);
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        // Dispose HTTP client
        _httpMasterServerClient?.Dispose();
        
        // Dispose game server probe (and its semaphore)
        _gameServerProbe?.Dispose();
        
        // Dispose throttled collection (and its timer)
        _servers?.Dispose();
        
        // Dispose cancellation token source
        _refreshCancellation?.Dispose();
        
        // Unsubscribe from selected server events
        if (_selectedServer != null)
        {
            _selectedServer.PropertyChanged -= SelectedServer_PropertyChanged;
        }
    }
}

public class RelayCommand : ICommand
{
    private readonly Func<object?, Task>? _asyncExecute;
    private readonly Action<object?>? _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Func<object?, Task> asyncExecute, Func<object?, bool>? canExecute = null)
    {
        _asyncExecute = asyncExecute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    public void Execute(object? parameter)
    {
        if (_asyncExecute != null)
        {
            _ = _asyncExecute(parameter);
        }
        else
        {
            _execute?.Invoke(parameter);
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}


