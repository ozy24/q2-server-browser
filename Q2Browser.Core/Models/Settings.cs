namespace Q2Browser.Core.Models;

public class Settings
{
    public string MasterServerAddress { get; set; } = "master.quake2.com";
    public int MasterServerPort { get; set; } = 27900;
    public bool UseHttpMasterServer { get; set; } = true;
    public string? HttpMasterServerUrl { get; set; } = "http://q2servers.com/?raw=2";
    public bool EnableLanBroadcast { get; set; } = true;
    public bool RefreshOnStartup { get; set; } = true;
    public int MaxConcurrentProbes { get; set; } = 75;
    public int ProbeTimeoutMs { get; set; } = 3000;
    public string Q2ProExecutablePath { get; set; } = string.Empty;
    public int UiUpdateIntervalMs { get; set; } = 150;
}

