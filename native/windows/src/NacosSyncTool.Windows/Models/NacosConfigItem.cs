using CommunityToolkit.Mvvm.ComponentModel;

namespace NacosSyncTool.Windows.Models;

/// <summary>
/// Nacos 配置项
/// </summary>
public class NacosConfigItem : ObservableObject
{
    private bool _isSelected;

    public string DataId { get; set; } = string.Empty;
    public string Group { get; set; } = "DEFAULT_GROUP";
    public string Content { get; set; } = string.Empty;
    public string? Type { get; set; }

    /// <summary>
    /// 是否被选中（用于多选）
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// 唯一标识：group:dataId
    /// </summary>
    public string Key => $"{Group}:{DataId}";
}
