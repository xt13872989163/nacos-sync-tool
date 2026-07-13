namespace NacosSyncTool.Windows.Modules.RabbitMq.Models;

public sealed record RabbitMqConfirmationRequest(
    string Title,
    string Body,
    string ConfirmText,
    bool IsDangerous,
    bool RequiresTypedConfirmation = false);

public static class RabbitMqConfirmation
{
    public static RabbitMqConfirmationRequest BuildSync(RabbitMqSyncPlan plan) => new(
        "确认执行 RabbitMQ 同步",
        $"Virtual Host：{plan.VirtualHost}\n待创建：{plan.PendingCount}\n已存在跳过：{plan.SkippedCount}\n异常：{plan.ProblemCount}\n\n同步只补充缺失资源，不覆盖或删除目标资源。",
        "开始同步",
        false);

    public static RabbitMqConfirmationRequest BuildPurge(
        string cluster,
        string address,
        string virtualHost,
        int queueCount,
        long ready,
        long unacked) => new(
        "确认清空 Queue Ready 消息",
        $"集群：{cluster}\n地址：{address}\nVirtual Host：{virtualHost}\nQueue：{queueCount}\nReady：{ready}\nUnacked：{unacked}（不会清除）\n\n操作不可恢复，生产者仍可能继续写入。",
        "确认清空",
        true);
}
