using Q2Browser.Core.Services;

namespace Q2Browser.Wpf.Services;

public class CoreLoggerAdapter : ILogger
{
    public void LogInfo(string message, string? details = null)
    {
        DiagnosticLogger.Instance.LogInfo(message, details);
    }

    public void LogWarning(string message, string? details = null)
    {
        DiagnosticLogger.Instance.LogWarning(message, details);
    }

    public void LogError(string message, string? details = null)
    {
        DiagnosticLogger.Instance.LogError(message, details);
    }

    public void LogDebug(string message, string? details = null)
    {
        DiagnosticLogger.Instance.LogDebug(message, details);
    }
}










