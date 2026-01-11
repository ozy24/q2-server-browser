namespace Q2Browser.Core.Services;

public interface ILogger
{
    void LogInfo(string message, string? details = null);
    void LogWarning(string message, string? details = null);
    void LogError(string message, string? details = null);
    void LogDebug(string message, string? details = null);
}

