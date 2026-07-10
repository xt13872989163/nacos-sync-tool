using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using NacosSyncTool.Windows.Services;

namespace NacosSyncTool.Windows.Converters;

/// <summary>
/// 日志级别转颜色转换器
/// </summary>
public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => new SolidColorBrush(Color.FromRgb(101, 112, 102)), // MutedBrush
                LogLevel.Success => new SolidColorBrush(Color.FromRgb(47, 111, 94)), // AccentBrush
                LogLevel.Warning => new SolidColorBrush(Color.FromRgb(143, 91, 18)), // WarnBrush
                LogLevel.Error => new SolidColorBrush(Color.FromRgb(163, 58, 52)), // DangerBrush
                _ => new SolidColorBrush(Colors.Black)
            };
        }

        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
