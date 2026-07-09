using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NacosSyncTool.Windows.Converters;

/// <summary>
/// 甯冨皵鍊煎彇鍙嶈浆鎹㈠櫒
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

/// <summary>
/// 婧愮杩炴帴鐘舵€佽浆鏂囨湰杞崲鍣?/// </summary>
public class SourceConnectingTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isConnecting)
        {
            return isConnecting ? "杩炴帴涓?.." : "娴嬭瘯杩炴帴";
        }
        return "娴嬭瘯杩炴帴";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 鐩爣绔繛鎺ョ姸鎬佽浆鏂囨湰杞崲鍣?/// </summary>
public class TargetConnectingTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isConnecting)
        {
            return isConnecting ? "杩炴帴涓?.." : "娴嬭瘯杩炴帴";
        }
        return "娴嬭瘯杩炴帴";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 布尔值取反转可见性：true -> Collapsed, false -> Visible
/// 用于"未连接"药丸在已连接时隐藏。
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

