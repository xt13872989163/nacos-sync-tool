using NacosSyncTool.Windows.Models;

namespace NacosSyncTool.Windows.Services;

/// <summary>
/// 同步服务客户端接口（用于解耦）
/// </summary>
public interface ISyncNacosClient
{
    Task<List<NacosConfigItem>> ListConfigsAsync(string namespaceId);
    Task<string?> GetConfigAsync(string namespaceId, string dataId, string group);
    Task<bool> PublishConfigAsync(string namespaceId, NacosConfigItem item);
    Task<List<NacosNamespace>> GetNamespacesAsync();
}

/// <summary>
/// NacosApiService 的适配器，实现 ISyncNacosClient
/// </summary>
public class SyncNacosClientAdapter : ISyncNacosClient
{
    private readonly NacosApiService _apiService;

    public SyncNacosClientAdapter(NacosApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<List<NacosConfigItem>> ListConfigsAsync(string namespaceId)
    {
        var result = await _apiService.ListConfigsAsync(namespaceId);
        return result.Success && result.Data != null ? result.Data : new List<NacosConfigItem>();
    }

    public async Task<string?> GetConfigAsync(string namespaceId, string dataId, string group)
    {
        var result = await _apiService.GetConfigAsync(namespaceId, dataId, group);
        return result.Success ? result.Data : null;
    }

    public async Task<bool> PublishConfigAsync(string namespaceId, NacosConfigItem item)
    {
        var result = await _apiService.PublishConfigAsync(namespaceId, item);
        return result.Success;
    }

    public async Task<List<NacosNamespace>> GetNamespacesAsync()
    {
        var result = await _apiService.GetNamespacesAsync();
        return result.Success && result.Data != null ? result.Data : new List<NacosNamespace>();
    }
}

/// <summary>
/// 确认处理委托
/// </summary>
/// <param name="request">确认请求</param>
/// <returns>确认决策</returns>
public delegate Task<ConfirmDecision> ConfirmHandler(ConfirmationRequest request);

/// <summary>
/// 同步服务
/// 实现 Namespace 级别、文件级别、Key 级别同步和 Key 扫描
/// </summary>
public class SyncService
{
    private readonly ISyncNacosClient _sourceClient;
    private readonly ISyncNacosClient _targetClient;
    private readonly ConfirmHandler _confirm;
    private readonly Action<string>? _log;

    public SyncService(
        ISyncNacosClient sourceClient,
        ISyncNacosClient targetClient,
        ConfirmHandler confirm,
        Action<string>? log = null)
    {
        _sourceClient = sourceClient;
        _targetClient = targetClient;
        _confirm = confirm;
        _log = log;
    }

    /// <summary>
    /// Namespace 级别同步（双重确认）
    /// </summary>
    public async Task<SyncSummary> SyncNamespaceAsync(string sourceNamespaceId, string targetNamespaceId)
    {
        var summary = new SyncSummary();
        _log?.Invoke($"开始 Namespace 同步：{sourceNamespaceId} -> {targetNamespaceId}");

        var sourceFiles = await _sourceClient.ListConfigsAsync(sourceNamespaceId);
        _log?.Invoke($"源 Namespace 共 {sourceFiles.Count} 个配置文件");

        if (sourceFiles.Count == 0)
        {
            summary.Messages.Add("源 Namespace 没有配置文件");
            _log?.Invoke("源 Namespace 没有配置文件");
            return summary;
        }

        // 检查目标已存在的配置
        var targetContentById = new Dictionary<string, string?>();
        var existingTargetConfigs = new List<NacosConfigItem>();

        foreach (var file in sourceFiles)
        {
            var targetContent = await _targetClient.GetConfigAsync(targetNamespaceId, file.DataId, file.Group);
            targetContentById[file.Key] = targetContent;

            if (targetContent != null)
            {
                existingTargetConfigs.Add(file);
            }
        }

        // 第一次确认
        var confirmations = ConfirmPolicy.BuildNamespaceConfirmations(sourceFiles.Count, existingTargetConfigs);
        var firstDecision = await _confirm(confirmations[0]);

        if (firstDecision != ConfirmDecision.Confirm)
        {
            summary.Skipped = sourceFiles.Count;
            summary.Messages.Add("Namespace 同步已取消");
            _log?.Invoke("Namespace 同步已取消");
            return summary;
        }

        // 第二次确认（如果有覆盖）
        var overwriteDecision = ConfirmDecision.Confirm;
        if (confirmations.Count > 1 && existingTargetConfigs.Count > 0)
        {
            overwriteDecision = await _confirm(confirmations[1]);
        }

        // 执行同步
        foreach (var file in sourceFiles)
        {
            var targetContent = targetContentById.GetValueOrDefault(file.Key);

            if (targetContent == null)
            {
                // 目标不存在，直接创建
                var ok = await _targetClient.PublishConfigAsync(targetNamespaceId, file);
                if (ok) { summary.Created++; _log?.Invoke($"创建：{file.DataId} / {file.Group}"); }
                else { summary.Failed++; _log?.Invoke($"创建失败：{file.DataId} / {file.Group}"); }
                continue;
            }

            // 目标已存在
            if (overwriteDecision == ConfirmDecision.Confirm)
            {
                var ok = await _targetClient.PublishConfigAsync(targetNamespaceId, file);
                if (ok) { summary.Updated++; _log?.Invoke($"覆盖：{file.DataId} / {file.Group}"); }
                else { summary.Failed++; _log?.Invoke($"覆盖失败：{file.DataId} / {file.Group}"); }
            }
            else
            {
                summary.Skipped++;
                _log?.Invoke($"跳过：{file.DataId} / {file.Group}");
            }
        }

        _log?.Invoke($"Namespace 同步完成：{summary}");
        return summary;
    }

    /// <summary>
    /// 文件级别同步
    /// </summary>
    public async Task<SyncSummary> SyncFilesAsync(string targetNamespaceId, List<NacosConfigItem> files)
    {
        var summary = new SyncSummary();
        _log?.Invoke($"开始文件级别同步：{files.Count} 个文件 -> {targetNamespaceId}");

        foreach (var file in files)
        {
            var targetContent = await _targetClient.GetConfigAsync(targetNamespaceId, file.DataId, file.Group);

            if (targetContent == null)
            {
                // 目标不存在，直接创建
                var ok = await _targetClient.PublishConfigAsync(targetNamespaceId, file);
                if (ok) { summary.Created++; _log?.Invoke($"创建：{file.DataId} / {file.Group}"); }
                else { summary.Failed++; _log?.Invoke($"创建失败：{file.DataId} / {file.Group}"); }
                continue;
            }

            // 目标已存在，弹窗确认
            var decision = await _confirm(ConfirmPolicy.BuildFileOverwriteConfirmation(file));
            if (decision == ConfirmDecision.Confirm)
            {
                var ok = await _targetClient.PublishConfigAsync(targetNamespaceId, file);
                if (ok) { summary.Updated++; _log?.Invoke($"覆盖：{file.DataId} / {file.Group}"); }
                else { summary.Failed++; _log?.Invoke($"覆盖失败：{file.DataId} / {file.Group}"); }
            }
            else
            {
                summary.Skipped++;
                _log?.Invoke($"跳过：{file.DataId} / {file.Group}");
            }
        }

        _log?.Invoke($"文件同步完成：{summary}");
        return summary;
    }

    /// <summary>
    /// Key 扫描
    /// </summary>
    public async Task<List<KeyScanResult>> ScanKeyAsync(string sourceNamespaceId, string keyName)
    {
        _log?.Invoke($"开始扫描 Key：{keyName}，Namespace：{sourceNamespaceId}");
        var sourceFiles = await _sourceClient.ListConfigsAsync(sourceNamespaceId);
        var results = new List<KeyScanResult>();

        foreach (var file in sourceFiles)
        {
            var match = ConfigParser.FindKeyValue(file.Content, keyName, file.DataId, file.Type);
            if (match == null) continue;

            results.Add(new KeyScanResult
            {
                Id = $"{file.Group}:{file.DataId}:{match.KeyPath}",
                DataId = file.DataId,
                Group = file.Group,
                KeyPath = match.KeyPath,
                Value = match.Value,
                Type = file.Type,
                SyncStrategy = KeySyncStrategy.KeyOnly,
                IsSelected = false
            });
        }

        _log?.Invoke($"Key 扫描完成，共找到 {results.Count} 条结果");
        return results;
    }

    /// <summary>
    /// Key 级别同步
    /// </summary>
    public async Task<SyncSummary> SyncKeyResultsAsync(
        string sourceNamespaceId,
        string targetNamespaceId,
        List<KeyScanResult> results)
    {
        var summary = new SyncSummary();
        _log?.Invoke($"开始 Key 级别同步：{results.Count} 条结果 -> {targetNamespaceId}");

        foreach (var result in results)
        {
            var sourceContent = await _sourceClient.GetConfigAsync(sourceNamespaceId, result.DataId, result.Group);

            if (sourceContent == null)
            {
                summary.Failed++;
                summary.Messages.Add($"源配置缺失：{result.DataId} / {result.Group}");
                _log?.Invoke($"源配置缺失：{result.DataId} / {result.Group}");
                continue;
            }

            if (result.SyncStrategy == KeySyncStrategy.FullFile)
            {
                await SyncFullFileFromKeyResultAsync(targetNamespaceId, result, sourceContent, summary);
            }
            else
            {
                await SyncSingleKeyAsync(targetNamespaceId, result, sourceContent, summary);
            }
        }

        _log?.Invoke($"Key 同步完成：{summary}");
        return summary;
    }

    /// <summary>
    /// 从 Key 结果同步整个文件
    /// </summary>
    private async Task SyncFullFileFromKeyResultAsync(
        string targetNamespaceId,
        KeyScanResult result,
        string sourceContent,
        SyncSummary summary)
    {
        var targetContent = await _targetClient.GetConfigAsync(targetNamespaceId, result.DataId, result.Group);
        var sourceFile = new NacosConfigItem
        {
            DataId = result.DataId,
            Group = result.Group,
            Content = sourceContent,
            Type = result.Type
        };

        if (targetContent == null)
        {
            var ok = await _targetClient.PublishConfigAsync(targetNamespaceId, sourceFile);
            if (ok) { summary.Created++; _log?.Invoke($"创建文件：{result.DataId} / {result.Group}"); }
            else { summary.Failed++; _log?.Invoke($"创建文件失败：{result.DataId} / {result.Group}"); }
            return;
        }

        var decision = await _confirm(ConfirmPolicy.BuildFileOverwriteConfirmation(sourceFile));
        if (decision == ConfirmDecision.Confirm)
        {
            var ok = await _targetClient.PublishConfigAsync(targetNamespaceId, sourceFile);
            if (ok) { summary.Updated++; _log?.Invoke($"覆盖文件：{result.DataId} / {result.Group}"); }
            else { summary.Failed++; _log?.Invoke($"覆盖文件失败：{result.DataId} / {result.Group}"); }
        }
        else
        {
            summary.Skipped++;
            _log?.Invoke($"跳过文件：{result.DataId} / {result.Group}");
        }
    }

    /// <summary>
    /// 同步单个 Key
    /// </summary>
    private async Task SyncSingleKeyAsync(
        string targetNamespaceId,
        KeyScanResult result,
        string sourceContent,
        SyncSummary summary)
    {
        var targetContent = await _targetClient.GetConfigAsync(targetNamespaceId, result.DataId, result.Group);

        if (targetContent == null)
        {
            // 目标文件不存在，创建新文件并写入 Key
            var newContent = ConfigParser.UpsertKeyValue(string.Empty, result.KeyPath, result.Value, result.DataId, result.Type);
            var newItem = new NacosConfigItem
            {
                DataId = result.DataId,
                Group = result.Group,
                Content = newContent,
                Type = result.Type
            };

            var ok = await _targetClient.PublishConfigAsync(targetNamespaceId, newItem);
            if (ok) { summary.Created++; _log?.Invoke($"创建文件并写入 Key：{result.DataId} / {result.Group}"); }
            else { summary.Failed++; _log?.Invoke($"创建文件失败：{result.DataId} / {result.Group}"); }
            return;
        }

        // 检查目标是否已存在该 Key
        var existingKey = ConfigParser.FindKeyValue(targetContent, result.KeyPath, result.DataId, result.Type);

        if (existingKey != null)
        {
            // 弹窗确认是否覆盖
            var decision = await _confirm(ConfirmPolicy.BuildKeyOverwriteConfirmation(
                new NacosConfigItem { DataId = result.DataId, Group = result.Group },
                result.KeyPath));

            if (decision != ConfirmDecision.Confirm)
            {
                summary.Skipped++;
                _log?.Invoke($"跳过 Key：{result.DataId} / {result.Group} / {result.KeyPath}");
                return;
            }
        }

        // 插入或更新 Key
        var updatedContent = ConfigParser.UpsertKeyValue(targetContent, result.KeyPath, result.Value, result.DataId, result.Type);
        var updatedItem = new NacosConfigItem
        {
            DataId = result.DataId,
            Group = result.Group,
            Content = updatedContent,
            Type = result.Type
        };

        var ok2 = await _targetClient.PublishConfigAsync(targetNamespaceId, updatedItem);
        if (ok2) { summary.Updated++; _log?.Invoke($"更新 Key：{result.DataId} / {result.Group} / {result.KeyPath}"); }
        else { summary.Failed++; _log?.Invoke($"更新 Key 失败：{result.DataId} / {result.Group} / {result.KeyPath}"); }
    }
}
