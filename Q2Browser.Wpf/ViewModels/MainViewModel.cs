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
using System.Windows.Data;
using System.Windows.Input;
using Q2Browser.Core.Models;
using Q2Browser.Core.Networking;
using Q2Browser.Core.Services;
using Q2Browser.Wpf.Services;

namespace Q2Browser.Wpf.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private MasterServerClient? _masterServerClient;
    private HttpMasterServerClient? _httpMasterServerClient;
    private LanBroadcastClient? _lanBroadcastClient;
    private GameServerProbe? _gameServerProbe;
    private readonly FavoritesService _favoritesService;
    private LauncherService? _launcherService;
    private readonly ThrottledObservableCollection<ServerRowViewModel> _servers;
    private readonly ObservableCollection<ServerRowViewModel> _filteredServers;
    private readonly ListCollectionView _serversView;
    private readonly HashSet<string> _favoriteAddresses = new();
    private Settings _currentSettings = new();

    private string _searchText = string.Empty;
    private bool _isRefreshing;
    private string _statusText = "Ready";
    private int _serversFound;
    private CancellationTokenSource? _refreshCancellation;
    private bool _isInitialized;

    public MainViewModel()
    {
        _favoritesService = new FavoritesService();
        _servers = new ThrottledObservableCollection<ServerRowViewModel>(150);
        _filteredServers = new ObservableCollection<ServerRowViewModel>();
        
        // Create a ListCollectionView for sorting
        _serversView = new ListCollectionView(_filteredServers);
        
        // Set default sort: player count descending
        var sortDescription = new SortDescription("CurrentPlayers", ListSortDirection.Descending);
        _serversView.SortDescriptions.Add(sortDescription);
        
        _servers.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (ServerRowViewModel item in e.NewItems)
                {
                    if (ShouldIncludeInFilter(item))
                    {
                        _filteredServers.Add(item);
                    }
                }
            }
            if (e.OldItems != null)
            {
                foreach (ServerRowViewModel item in e.OldItems)
                {
                    _filteredServers.Remove(item);
                }
            }
        };
        
        RefreshCommand = new RelayCommand(async _ => await RefreshServersAsync(), _ => !IsRefreshing && _isInitialized);
        ConnectCommand = new RelayCommand(ConnectToServer, _ => SelectedServer != null);
        ToggleFavoriteCommand = new RelayCommand(ToggleFavorite, _ => SelectedServer != null);
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        OpenDiagnosticsCommand = new RelayCommand(_ => OpenDiagnostics());
        
        _ = InitializeAsync();
    }

    private bool ShouldIncludeInFilter(ServerRowViewModel server)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var searchLower = SearchText.ToLowerInvariant();
        return server.Hostname.ToLowerInvariant().Contains(searchLower) ||
               server.Map.ToLowerInvariant().Contains(searchLower) ||
               server.Mod.ToLowerInvariant().Contains(searchLower);
    }

    private async Task InitializeAsync()
    {
        var logger = new CoreLoggerAdapter();
        DiagnosticLogger.Instance.LogInfo("Application initializing...");
        
        _currentSettings = await _favoritesService.LoadSettingsAsync();
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
            ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            
            // Auto-refresh on startup if enabled
            if (_currentSettings.RefreshOnStartup)
            {
                StatusText = "Ready - Auto-refreshing servers...";
                _ = RefreshServersAsync();
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
        _masterServerClient = new MasterServerClient(_currentSettings, logger);
        _httpMasterServerClient = new HttpMasterServerClient(_currentSettings, logger);
        _lanBroadcastClient = new LanBroadcastClient(_currentSettings, logger);
        _gameServerProbe = new GameServerProbe(_currentSettings, logger);
        _launcherService = new LauncherService(_currentSettings);
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

    private void OpenDiagnostics()
    {
        try
        {
            DiagnosticLogger.Instance.LogInfo("Opening diagnostic window...");
            
            var diagnosticWindow = new Views.DiagnosticWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            
            DiagnosticLogger.Instance.LogDebug("Diagnostic window created, showing...");
            diagnosticWindow.Show();
            DiagnosticLogger.Instance.LogInfo("Diagnostic window opened successfully");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Instance.LogError($"Failed to open diagnostic window: {ex.Message}", ex.ToString());
            System.Windows.MessageBox.Show(
                $"Error opening diagnostic window:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public ICollectionView Servers => _serversView;

    public ServerRowViewModel? SelectedServer { get; set; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                FilterServers();
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
    public ICommand OpenDiagnosticsCommand { get; }

    private async Task RefreshServersAsync()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation = new CancellationTokenSource();

        DiagnosticLogger.Instance.LogInfo("=== Starting server refresh ===");
        IsRefreshing = true;
        StatusText = "Querying master server...";
        _servers.Clear();
        _filteredServers.Clear();
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
            FilterServers();
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

    private void FilterServers()
    {
        _filteredServers.Clear();
        
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var server in _servers)
            {
                _filteredServers.Add(server);
            }
        }
        else
        {
            var searchLower = SearchText.ToLowerInvariant();
            foreach (var server in _servers)
            {
                if (server.Hostname.ToLowerInvariant().Contains(searchLower) ||
                    server.Map.ToLowerInvariant().Contains(searchLower) ||
                    server.Mod.ToLowerInvariant().Contains(searchLower))
                {
                    _filteredServers.Add(server);
                }
            }
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
                $"Cannot connect to server:\n\n{ex.Message}\n\nPlease configure the Q2Pro executable path in Settings.",
                "Configuration Required",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch (FileNotFoundException ex)
        {
            System.Windows.MessageBox.Show(
                $"Cannot connect to server:\n\n{ex.Message}\n\nPlease check the Q2Pro executable path in Settings.",
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

    private async void ToggleFavorite(object? parameter)
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

        await _favoritesService.SaveFavoritesAsync(_favoriteAddresses.ToList());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

