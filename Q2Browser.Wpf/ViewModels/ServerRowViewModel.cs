using System.ComponentModel;
using System.Runtime.CompilerServices;
using Q2Browser.Core.Models;

namespace Q2Browser.Wpf.ViewModels;

public class ServerRowViewModel : INotifyPropertyChanged
{
    private readonly ServerEntry _serverEntry;

    public ServerRowViewModel(ServerEntry serverEntry)
    {
        _serverEntry = serverEntry;
    }

    public string Hostname => _serverEntry.Hostname;
    public string Map => _serverEntry.Map;
    public string Mod => _serverEntry.Mod;
    public int CurrentPlayers => _serverEntry.CurrentPlayers;
    public int MaxClients => _serverEntry.MaxClients;
    public int? Ping => _serverEntry.Ping;
    public string FullAddress => _serverEntry.FullAddress;
    public bool IsFavorite
    {
        get => _serverEntry.IsFavorite;
        set
        {
            if (_serverEntry.IsFavorite != value)
            {
                _serverEntry.IsFavorite = value;
                OnPropertyChanged();
            }
        }
    }

    public string PlayersText => $"{CurrentPlayers}/{MaxClients}";
    public string PingText => Ping.HasValue ? $"{Ping} ms" : "N/A";

    public ServerEntry ServerEntry => _serverEntry;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

