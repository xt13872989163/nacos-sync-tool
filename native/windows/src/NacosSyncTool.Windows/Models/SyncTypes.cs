namespace NacosSyncTool.Windows.Models;

/// <summary>
/// Key 扫描结果
/// </summary>
public class KeyScanResult
{
    public string Id { get; set; } = string.Empty;
    public string DataId { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string KeyPath { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Type { get; set; }

    /// <summary>
    /// 同步策略：KeyOnly=仅同步当前 Key，FullFile=同步整个文件
    /// </summary>
    public KeySyncStrategy SyncStrategy { get; set; } = KeySyncStrategy.KeyOnly;

    /// <summary>
    /// 是否被选中（用于多选）
    /// </summary>
    public bool IsSelected { get; set; }
}

public enum KeySyncStrategy
{
    KeyOnly,
    FullFile
}

/// <summary>
/// 同步模式
/// </summary>
public enum SyncMode
{
    Namespace,
    File,
    Key
}

/// <summary>
/// 同步结果摘要
/// </summary>
public class SyncSummary
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Messages { get; set; } = new();

    public override string ToString()
    {
        return $"新增 {Created}，更新 {Updated}，跳过 {Skipped}，失败 {Failed}";
    }
}

/// <summary>
/// 确认决策
/// </summary>
public enum ConfirmDecision
{
    Confirm,
    Skip,
    Cancel
}

/// <summary>
/// 确认请求类型
/// </summary>
public enum ConfirmationKind
{
    NamespaceStart,
    OverwriteExistingFiles,
    OverwriteFile,
    OverwriteKey
}

/// <summary>
/// 确认请求
/// </summary>
public class ConfirmationRequest
{
    public ConfirmationKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int AffectedFileCount { get; set; }
    public List<NacosConfigItem> AffectedConfigs { get; set; } = new();
}
