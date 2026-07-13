using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using NacosSyncTool.Windows.Services;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public sealed class RabbitMqLogService
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NacosSyncTool",
        "logs");

    private static readonly string LogFile = Path.Combine(LogDirectory, "rabbitmq.log");
    private static readonly Regex SensitiveValuePattern = new(
        @"(?i)(password|passwd|authorization)\s*[:=]\s*[^\s,;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public event Action<RabbitMqLogEntry>? LogAdded;

    public void Info(string message) => Add(LogLevel.Info, message);
    public void Success(string message) => Add(LogLevel.Success, message);
    public void Warning(string message) => Add(LogLevel.Warning, message);
    public void Error(string message) => Add(LogLevel.Error, message);

    private void Add(LogLevel level, string message)
    {
        var safeMessage = Sanitize(message);
        var entry = new RabbitMqLogEntry(DateTime.Now, level, safeMessage);
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(
                LogFile,
                $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{level}] {safeMessage}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // 文件日志失败不影响界面操作。
        }

        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.Invoke(() => LogAdded?.Invoke(entry));
        else
            LogAdded?.Invoke(entry);
    }

    public static string Sanitize(string message) =>
        SensitiveValuePattern.Replace(message ?? string.Empty, "$1=***");
}

public sealed record RabbitMqLogEntry(DateTime Timestamp, LogLevel Level, string Message)
{
    public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
}
