using System.IO;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqViewBindingTests
{
    [Fact]
    public void ViewContainsRequiredCommandsAndNoResourceDeleteActions()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "NacosSyncTool.Windows", "Modules", "RabbitMq", "Views", "RabbitMqModuleView.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(path));

        foreach (var command in new[]
                 {
                     "TestSourceConnectionCommand", "TestTargetConnectionCommand",
                     "BuildPlanCommand", "ExecutePlanCommand", "TestPurgeConnectionCommand",
                     "RefreshPurgeQueuesCommand", "PurgeAllReadyMessagesCommand"
                 })
            Assert.Contains(command, xaml);

        Assert.DoesNotContain("DeleteQueueCommand", xaml);
        Assert.DoesNotContain("DeleteVirtualHostCommand", xaml);
        Assert.DoesNotContain("删除 Queue", xaml);
        Assert.DoesNotContain("删除 Virtual Host", xaml);
    }
}
