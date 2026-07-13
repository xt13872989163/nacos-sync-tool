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

        foreach (var resource in new[] { "VirtualHost", "Queue", "Exchange", "Binding", "Policy" })
            Assert.DoesNotContain($"Delete{resource}Command", xaml);

        foreach (var resource in new[] { "Virtual Host", "Queue", "Exchange", "Binding", "Policy" })
            Assert.DoesNotContain($"删除 {resource}", xaml);
    }
}
