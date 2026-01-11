using System.Collections.Generic;

namespace Q2Browser.Core.Models;

public class ServerEntry
{
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public int? Ping { get; set; }
    public Dictionary<string, string> Cvars { get; set; } = new();
    public List<PlayerInfo> Players { get; set; } = new();
    public bool IsFavorite { get; set; }
    public string? CountryCode { get; set; }

    public string FullAddress => $"{Address}:{Port}";
    
    public string Hostname => Cvars.GetValueOrDefault("hostname", "Unknown Server");
    public string Map => Cvars.GetValueOrDefault("mapname", "Unknown");
    public string Mod => Cvars.GetValueOrDefault("game", "baseq2");
    public int MaxClients => int.TryParse(Cvars.GetValueOrDefault("maxclients", "0"), out var max) ? max : 0;
    public int CurrentPlayers => Players.Count;
}

public class PlayerInfo
{
    public int Score { get; set; }
    public int Ping { get; set; }
    public string Name { get; set; } = string.Empty;
}

