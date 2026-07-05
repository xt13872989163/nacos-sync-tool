using System.Windows;

namespace NacosSyncTool.Windows.Services;

/// <summary>
/// 日志服务
/// </summary>
public class LogService
{
    public event Action<LogEntry>? LogAdded;

    public void Info(string message)
    {
        AddLog(LogLevel.Info, message);
    }

    public void Success(string message)
    {
        AddLog(LogLevel.Success, message);
    }

    public void Warning(string message)
    {
        AddLog(LogLevel.Warning, message);
    }

    public void Error(string message)
    {
        AddLog(LogLevel.Error, message);
    }

    private void AddLog(LogLevel level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        // 在 UI 线程触发事件
        Application.Current?.Dispatcher.Invoke(() =>
        {
            LogAdded?.Invoke(entry);
        });
    }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;

    public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
}

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}
