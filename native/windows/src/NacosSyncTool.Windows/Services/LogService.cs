using System.IO;
using System.Windows;

namespace NacosSyncTool.Windows.Services;

/// <summary>
/// 日志服务：同时输出到 UI 和本地文件
/// </summary>
public class LogService
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NacosSyncTool", "logs");

    private static readonly string LogFile = Path.Combine(LogDirectory, "app.log");

    public event Action<LogEntry>? LogAdded;

    public void Info(string message) => AddLog(LogLevel.Info, message);
    public void Success(string message) => AddLog(LogLevel.Success, message);
    public void Warning(string message) => AddLog(LogLevel.Warning, message);
    public void Error(string message) => AddLog(LogLevel.Error, message);

    private void AddLog(LogLevel level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        // 写文件日志（便于排查问题）
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
            File.AppendAllText(LogFile, line, System.Text.Encoding.UTF8);
        }
        catch
        {
            // 文件日志失败不影响 UI
        }

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