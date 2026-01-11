using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using Q2Browser.Core.Models;
using Q2Browser.Core.Services;

namespace Q2Browser.Wpf.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly FavoritesService _favoritesService;
    private Settings _settings;

    public SettingsViewModel()
    {
        _favoritesService = new FavoritesService();
        _settings = new Settings();
        
        SaveCommand = new RelayCommand(async _ => await SaveSettingsAsync());
        BrowseQ2ProCommand = new RelayCommand(_ => BrowseQ2ProExecutable());
        CancelCommand = new RelayCommand(_ => { });
        
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _favoritesService.LoadSettingsAsync();
        OnPropertyChanged(nameof(MasterServerAddress));
        OnPropertyChanged(nameof(MasterServerPort));
        OnPropertyChanged(nameof(UseHttpMasterServer));
        OnPropertyChanged(nameof(HttpMasterServerUrl));
        OnPropertyChanged(nameof(EnableLanBroadcast));
        OnPropertyChanged(nameof(RefreshOnStartup));
        OnPropertyChanged(nameof(Q2ProExecutablePath));
        OnPropertyChanged(nameof(MaxConcurrentProbes));
        OnPropertyChanged(nameof(ProbeTimeoutMs));
    }

    public string MasterServerAddress
    {
        get => _settings.MasterServerAddress;
        set
        {
            if (_settings.MasterServerAddress != value)
            {
                _settings.MasterServerAddress = value;
                OnPropertyChanged();
            }
        }
    }

    public string MasterServerPort
    {
        get => _settings.MasterServerPort.ToString();
        set
        {
            if (int.TryParse(value, out var port) && port > 0 && port < 65536)
            {
                if (_settings.MasterServerPort != port)
                {
                    _settings.MasterServerPort = port;
                    OnPropertyChanged();
                }
            }
        }
    }

    public bool UseHttpMasterServer
    {
        get => _settings.UseHttpMasterServer;
        set
        {
            if (_settings.UseHttpMasterServer != value)
            {
                _settings.UseHttpMasterServer = value;
                OnPropertyChanged();
            }
        }
    }

    public string? HttpMasterServerUrl
    {
        get => _settings.HttpMasterServerUrl;
        set
        {
            if (_settings.HttpMasterServerUrl != value)
            {
                _settings.HttpMasterServerUrl = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableLanBroadcast
    {
        get => _settings.EnableLanBroadcast;
        set
        {
            if (_settings.EnableLanBroadcast != value)
            {
                _settings.EnableLanBroadcast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool RefreshOnStartup
    {
        get => _settings.RefreshOnStartup;
        set
        {
            if (_settings.RefreshOnStartup != value)
            {
                _settings.RefreshOnStartup = value;
                OnPropertyChanged();
            }
        }
    }

    public string Q2ProExecutablePath
    {
        get => _settings.Q2ProExecutablePath;
        set
        {
            if (_settings.Q2ProExecutablePath != value)
            {
                _settings.Q2ProExecutablePath = value;
                OnPropertyChanged();
            }
        }
    }

    public string MaxConcurrentProbes
    {
        get => _settings.MaxConcurrentProbes.ToString();
        set
        {
            if (int.TryParse(value, out var probes) && probes > 0 && probes <= 200)
            {
                if (_settings.MaxConcurrentProbes != probes)
                {
                    _settings.MaxConcurrentProbes = probes;
                    OnPropertyChanged();
                }
            }
        }
    }

    public string ProbeTimeoutMs
    {
        get => _settings.ProbeTimeoutMs.ToString();
        set
        {
            if (int.TryParse(value, out var timeout) && timeout > 0 && timeout <= 30000)
            {
                if (_settings.ProbeTimeoutMs != timeout)
                {
                    _settings.ProbeTimeoutMs = timeout;
                    OnPropertyChanged();
                }
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand BrowseQ2ProCommand { get; }
    public ICommand CancelCommand { get; }

    private void BrowseQ2ProExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select Q2Pro Executable"
        };

        if (dialog.ShowDialog() == true)
        {
            Q2ProExecutablePath = dialog.FileName;
        }
    }

    private async Task SaveSettingsAsync()
    {
        await _favoritesService.SaveSettingsAsync(_settings);
    }

    public Settings GetSettings() => _settings;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

