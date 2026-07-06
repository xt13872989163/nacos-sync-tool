using System.Globalization;
using System.Windows.Data;
using System.Windows;
using NacosSyncTool.Windows.Models;

namespace NacosSyncTool.Windows.Converters;

/// <summary>
/// SyncMode 转布尔值（用于 RadioButton 绑定）
/// ConverterParameter: Namespace / File / Key
/// </summary>
public class SyncModeToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SyncMode mode && parameter is string paramStr
            && Enum.TryParse<SyncMode>(paramStr, out var paramMode))
        {
            return mode == paramMode;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr
            && Enum.TryParse<SyncMode>(paramStr, out var paramMode))
        {
            return paramMode;
        }
        return Binding.DoNothing;
    }
}

/// <summary>
/// SyncMode 转可见性（用于根据模式显示/隐藏控件）
/// ConverterParameter: Namespace / File / Key
/// 当当前模式等于参数时返回 Visible，否则 Collapsed
/// </summary>
public class SyncModeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SyncMode mode && parameter is string paramStr
            && Enum.TryParse<SyncMode>(paramStr, out var paramMode))
        {
            return mode == paramMode ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// KeySyncStrategy 转换器（用于 ComboBox 绑定枚举）
/// </summary>
public class KeySyncStrategyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
