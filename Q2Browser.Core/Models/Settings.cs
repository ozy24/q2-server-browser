using System;
using Q2Browser.Core.Protocol;

namespace Q2Browser.Core.Models;

public class Settings
{
    private string _masterServerAddress = "master.quake2.com";
    private int _masterServerPort = 27900;
    private string? _httpMasterServerUrl = "http://q2servers.com/?raw=2";
    private int _maxConcurrentProbes = 75;
    private int _probeTimeoutMs = 3000;
    private int _uiUpdateIntervalMs = 150;

    public string MasterServerAddress
    {
        get => _masterServerAddress;
        set => _masterServerAddress = value ?? throw new ArgumentNullException(nameof(value));
    }

    public int MasterServerPort
    {
        get => _masterServerPort;
        set => _masterServerPort = value is >= 1 and <= 65535
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Port must be between 1 and 65535");
    }

    public bool UseHttpMasterServer { get; set; } = true;

    public string? HttpMasterServerUrl
    {
        get => _httpMasterServerUrl;
        set
        {
            if (value != null && !UrlValidator.IsValidHttpUrl(value))
            {
                throw new ArgumentException("URL must be a valid HTTP or HTTPS URL", nameof(value));
            }
            _httpMasterServerUrl = value;
        }
    }

    public bool EnableLanBroadcast { get; set; } = true;
    public bool RefreshOnStartup { get; set; } = true;
    public bool PortableMode { get; set; } = true;
    public bool IncludePlayerNamesInSearch { get; set; } = false;
    public string LogLevel { get; set; } = "Warning";

    public int MaxConcurrentProbes
    {
        get => _maxConcurrentProbes;
        set => _maxConcurrentProbes = value is > 0 and <= 200
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "MaxConcurrentProbes must be between 1 and 200");
    }

    public int ProbeTimeoutMs
    {
        get => _probeTimeoutMs;
        set => _probeTimeoutMs = value is > 0 and <= 60000
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "ProbeTimeoutMs must be between 1 and 60000");
    }

    public string Q2ExecutablePath { get; set; } = string.Empty;

    public int UiUpdateIntervalMs
    {
        get => _uiUpdateIntervalMs;
        set => _uiUpdateIntervalMs = value is > 0 and <= 10000
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "UiUpdateIntervalMs must be between 1 and 10000");
    }

}

