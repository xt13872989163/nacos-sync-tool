using System.Net;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqQueuePurgeServiceTests
{
    [Fact]
    public async Task PurgesReadyMessagesAndReportsUnackedSeparately()
    {
        var before = new[] { Queue("orders", 10, 3), Queue("payments", 5, 2) };
        var after = new[] { Queue("orders", 0, 3), Queue("payments", 0, 2) };
        var client = new FakeRabbitMqManagementClient();
        client.QueueResponses.Enqueue(before);
        client.QueueResponses.Enqueue(after);
        var service = new RabbitMqQueuePurgeService(client);

        var preview = await service.BuildPreviewAsync("/dev", CancellationToken.None);
        var result = await service.ExecuteAsync(preview, null, CancellationToken.None);

        Assert.Equal(15, preview.ReadyTotal);
        Assert.Equal(5, preview.UnackedTotal);
        Assert.Equal(2, result.SucceededCount);
        Assert.True(result.StatisticsRefreshed);
        Assert.Equal(0, result.ReadyAfter);
        Assert.Equal(5, result.UnackedAfter);
        Assert.Equal(2, client.Calls.Count(call => call.StartsWith("PurgeQueue", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task RefreshFailureReportsUnknownCountsInsteadOfZero()
    {
        var client = new FakeRabbitMqManagementClient
        {
            FailureForCall = call => call.StartsWith("GetQueues", StringComparison.Ordinal)
                ? new HttpRequestException("refresh failed")
                : null
        };
        var preview = new RabbitMqQueuePurgePreview(
            "/",
            [Queue("orders", 4, 2)],
            4,
            2);

        var result = await new RabbitMqQueuePurgeService(client)
            .ExecuteAsync(preview, null, CancellationToken.None);

        Assert.Equal(1, result.SucceededCount);
        Assert.False(result.StatisticsRefreshed);
        Assert.Null(result.ReadyAfter);
        Assert.Null(result.UnackedAfter);
    }

    [Fact]
    public async Task MissingQueueDoesNotStopFollowingQueues()
    {
        var firstPurge = true;
        var client = new FakeRabbitMqManagementClient
        {
            Queues = [],
            FailureForCall = call => call.StartsWith("PurgeQueue", StringComparison.Ordinal) && firstPurge
                ? MissingOnce()
                : null
        };
        Exception MissingOnce()
        {
            firstPurge = false;
            return new RabbitMqApiException(HttpStatusCode.NotFound, "queue", "missing");
        }
        var preview = new RabbitMqQueuePurgePreview(
            "/",
            [Queue("one", 1, 0), Queue("two", 1, 0)],
            2,
            0);

        var result = await new RabbitMqQueuePurgeService(client)
            .ExecuteAsync(preview, null, CancellationToken.None);

        Assert.Equal(2, client.Calls.Count(call => call.StartsWith("PurgeQueue", StringComparison.Ordinal)));
        Assert.Contains(result.Items, item => item.Status == RabbitMqExecutionStatus.Missing);
        Assert.Contains(result.Items, item => item.Status == RabbitMqExecutionStatus.Succeeded);
    }

    private static RabbitMqQueue Queue(string name, long ready, long unacked) =>
        new(name, true, false, false, [], ready, unacked, 1);
}
