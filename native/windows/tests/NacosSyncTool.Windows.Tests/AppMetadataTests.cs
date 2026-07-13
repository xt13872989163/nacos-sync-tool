using NacosSyncTool.Windows.Models;
using Xunit;

namespace NacosSyncTool.Windows.Tests;

public sealed class AppMetadataTests
{
    [Fact]
    public void ProductNameIdentifiesNativeWindowsEdition()
    {
        Assert.Equal("Nacos / RabbitMQ Sync Tool Native Windows", AppMetadata.ProductName);
    }

    [Fact]
    public void VersionLabelIncludesCurrentPackageVersion()
    {
        Assert.Contains(AppMetadata.Version, AppMetadata.VersionLabel);
    }
}
