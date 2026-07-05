namespace NacosSyncTool.Windows.Models;

/// <summary>
/// Nacos Namespace 信息
/// </summary>
public class NacosNamespace
{
    public string NamespaceId { get; set; } = string.Empty;
    public string NamespaceName { get; set; } = string.Empty;
    public string? NamespaceDesc { get; set; }

    public string DisplayText => string.IsNullOrEmpty(NamespaceName)
        ? NamespaceId
        : $"{NamespaceName} ({NamespaceId})";
}
