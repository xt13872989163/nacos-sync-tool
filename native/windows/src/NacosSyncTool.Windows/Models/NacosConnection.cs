namespace NacosSyncTool.Windows.Models;

/// <summary>
/// Nacos 连接配置
/// </summary>
public class NacosConnection
{
    public string Address { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? SelectedNamespace { get; set; }
}
