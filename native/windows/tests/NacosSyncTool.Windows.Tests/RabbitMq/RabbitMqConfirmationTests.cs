using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqConfirmationTests
{
    [Fact]
    public void PurgeConfirmationContainsCountsWithoutTypedNameRequirement()
    {
        var request = RabbitMqConfirmation.BuildPurge(
            "cluster-a",
            "http://mq:15672",
            "/dev",
            4,
            120,
            8);

        Assert.Contains("/dev", request.Body);
        Assert.Contains("120", request.Body);
        Assert.Contains("8", request.Body);
        Assert.False(request.RequiresTypedConfirmation);
        Assert.Equal("确认清空", request.ConfirmText);
    }

    [Fact]
    public void LogSanitizerRemovesPasswordsAndAuthorizationValues()
    {
        var sanitized = RabbitMqLogService.Sanitize(
            "password=secret Authorization:Basic-abc address=http://mq");

        Assert.DoesNotContain("secret", sanitized);
        Assert.DoesNotContain("Basic-abc", sanitized);
        Assert.Contains("address=http://mq", sanitized);
    }
}
