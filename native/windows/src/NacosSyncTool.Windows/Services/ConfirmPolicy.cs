using NacosSyncTool.Windows.Models;

namespace NacosSyncTool.Windows.Services;

/// <summary>
/// 确认策略构建器
/// </summary>
public static class ConfirmPolicy
{
    /// <summary>
    /// 构建 Namespace 同步的双重确认请求
    /// </summary>
    public static List<ConfirmationRequest> BuildNamespaceConfirmations(
        int sourceFileCount,
        List<NacosConfigItem> existingTargetConfigs)
    {
        var requests = new List<ConfirmationRequest>
        {
            new ConfirmationRequest
            {
                Kind = ConfirmationKind.NamespaceStart,
                Title = "确认同步整个 Namespace",
                Body = $"本次将同步源 Namespace 下的 {sourceFileCount} 个配置文件。",
                AffectedFileCount = sourceFileCount,
                AffectedConfigs = new List<NacosConfigItem>()
            }
        };

        if (existingTargetConfigs.Count > 0)
        {
            requests.Add(new ConfirmationRequest
            {
                Kind = ConfirmationKind.OverwriteExistingFiles,
                Title = "确认覆盖目标已有配置",
                Body = $"目标 Namespace 中已有 {existingTargetConfigs.Count} 个同名 DataId + Group，确认后将覆盖这些配置文件。",
                AffectedFileCount = existingTargetConfigs.Count,
                AffectedConfigs = existingTargetConfigs
            });
        }

        return requests;
    }

    /// <summary>
    /// 构建文件覆盖确认请求
    /// </summary>
    public static ConfirmationRequest BuildFileOverwriteConfirmation(NacosConfigItem config)
    {
        return new ConfirmationRequest
        {
            Kind = ConfirmationKind.OverwriteFile,
            Title = "确认覆盖配置文件",
            Body = $"目标已存在 {config.DataId} / {config.Group}，确认后将覆盖整个配置文件。",
            AffectedFileCount = 1,
            AffectedConfigs = new List<NacosConfigItem> { config }
        };
    }

    /// <summary>
    /// 构建 Key 覆盖确认请求
    /// </summary>
    public static ConfirmationRequest BuildKeyOverwriteConfirmation(NacosConfigItem config, string keyPath)
    {
        return new ConfirmationRequest
        {
            Kind = ConfirmationKind.OverwriteKey,
            Title = "确认覆盖当前 Key",
            Body = $"目标配置 {config.DataId} / {config.Group} 已存在 {keyPath}，确认后仅覆盖该 Key 的值。",
            AffectedFileCount = 1,
            AffectedConfigs = new List<NacosConfigItem> { config }
        };
    }
}
