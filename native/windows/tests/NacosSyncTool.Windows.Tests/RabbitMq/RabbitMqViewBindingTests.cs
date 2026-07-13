using System.IO;
using System.Xml.Linq;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqViewBindingTests
{
    [Fact]
    public void MainWindowBindsRabbitVisibilityToShellOnWindow()
    {
        var xaml = ReadProjectFile("MainWindow.xaml");

        Assert.Contains("DataContext=\"{Binding RabbitMq}\"", xaml);
        Assert.Contains(
            "Visibility=\"{Binding DataContext.IsRabbitMqSelected, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BoolToVisibility}}\"",
            xaml);
    }

    [Fact]
    public void ResourceColumnsAreReadOnlyButSelectionColumnsRemainEditable()
    {
        var document = XDocument.Parse(ReadRabbitView());

        foreach (var itemsSource in new[]
                 {
                     "{Binding Exchanges}", "{Binding Queues}",
                     "{Binding Bindings}", "{Binding Policies}"
                 })
        {
            var dataGrid = Assert.Single(document.Descendants().Where(
                element => element.Name.LocalName == "DataGrid"
                           && (string?)element.Attribute("ItemsSource") == itemsSource));
            var columns = dataGrid.Descendants().Where(
                element => element.Name.LocalName is "DataGridCheckBoxColumn" or "DataGridTextColumn").ToArray();
            var selectionColumn = Assert.Single(columns.Where(
                column => (string?)column.Attribute("Binding") == "{Binding IsSelected}"));

            Assert.Equal("False", (string?)selectionColumn.Attribute("IsReadOnly"));

            var resourceColumns = columns.Where(column => column != selectionColumn).ToArray();
            Assert.NotEmpty(resourceColumns);
            Assert.All(resourceColumns, column =>
            {
                Assert.StartsWith("{Binding Resource.", (string?)column.Attribute("Binding"));
                Assert.Equal("True", (string?)column.Attribute("IsReadOnly"));
            });
        }
    }

    [Fact]
    public void BusyOverlayCoversRootGridAndProvidesProgressAndCancellation()
    {
        var document = XDocument.Parse(ReadRabbitView());
        var rootGrid = Assert.Single(document.Root!.Elements().Where(
            element => element.Name.LocalName == "Grid"));
        var purgeGrid = Assert.Single(rootGrid.Elements().Where(
            element => element.Name.LocalName == "Grid"
                       && element.Elements().Any(child =>
                           child.Name.LocalName == "DataGrid"
                           && (string?)child.Attribute("ItemsSource") == "{Binding PurgeQueues}")));
        var overlay = Assert.Single(document.Descendants().Where(
            element => element.Name.LocalName == "Grid"
                       && (string?)element.Attribute("Visibility")
                       == "{Binding IsBusy, Converter={StaticResource BoolToVisibility}}"));

        Assert.Single(purgeGrid.Descendants().Where(
            element => element.Name.LocalName == "Button"
                       && (string?)element.Attribute("Command") == "{Binding CancelCommand}"));
        Assert.Same(rootGrid, overlay.Parent);
        Assert.Equal("4", (string?)overlay.Attribute("Grid.RowSpan"));
        Assert.Equal("100", (string?)overlay.Attribute("Panel.ZIndex"));
        Assert.Equal("True", (string?)overlay.Attribute("IsHitTestVisible"));
        Assert.False(string.IsNullOrWhiteSpace((string?)overlay.Attribute("Background")));
        Assert.Single(overlay.Descendants().Where(
            element => (string?)element.Attribute("Text") == "{Binding BusyText}"));
        Assert.Single(overlay.Descendants().Where(
            element => (string?)element.Attribute("Text") == "{Binding ProgressText}"));
        Assert.Single(overlay.Descendants().Where(
            element => element.Name.LocalName == "Button"
                       && (string?)element.Attribute("Command") == "{Binding CancelCommand}"));
    }

    [Fact]
    public void ViewContainsRequiredCommandsAndNoResourceDeleteActions()
    {
        var xaml = ReadRabbitView();

        foreach (var command in new[]
                 {
                     "TestSourceConnectionCommand", "TestTargetConnectionCommand",
                     "BuildPlanCommand", "ExecutePlanCommand", "TestPurgeConnectionCommand",
                     "RefreshPurgeQueuesCommand", "PurgeAllReadyMessagesCommand", "CancelCommand"
                 })
            Assert.Contains(command, xaml);

        foreach (var resource in new[] { "VirtualHost", "Queue", "Exchange", "Binding", "Policy" })
            Assert.DoesNotContain($"Delete{resource}Command", xaml);

        foreach (var resource in new[] { "Virtual Host", "Queue", "Exchange", "Binding", "Policy" })
            Assert.DoesNotContain($"删除 {resource}", xaml);
    }

    private static string ReadProjectFile(string relativePath)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "NacosSyncTool.Windows", relativePath);
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string ReadRabbitView() => ReadProjectFile(
        Path.Combine("Modules", "RabbitMq", "Views", "RabbitMqModuleView.xaml"));
}
