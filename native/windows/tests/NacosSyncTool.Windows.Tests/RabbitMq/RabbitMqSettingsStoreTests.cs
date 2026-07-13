using System.IO;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;
using NacosSyncTool.Windows.Modules.RabbitMq.Services;
using Xunit;

namespace NacosSyncTool.Windows.Tests.RabbitMq;

public sealed class RabbitMqSettingsStoreTests
{
    [Fact]
    public async Task StoresRolesSeparatelyAndNeverWritesPlaintextPasswords()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), $"rabbitmq-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        var store = new RabbitMqSettingsStore(path);
        try
        {
            await store.SaveAsync(
                RabbitMqClientRole.SourceReadOnly,
                Connection("http://source:15672", "source-secret"),
                cancellationToken);
            await store.SaveAsync(
                RabbitMqClientRole.QueuePurge,
                Connection("http://purge:15672", "purge-secret"),
                cancellationToken);

            var source = await store.LoadAsync(RabbitMqClientRole.SourceReadOnly, cancellationToken);
            var purge = await store.LoadAsync(RabbitMqClientRole.QueuePurge, cancellationToken);
            var json = await File.ReadAllTextAsync(path);

            Assert.Equal("http://source:15672", source!.Address);
            Assert.Equal("source-secret", source.Password);
            Assert.Equal("http://purge:15672", purge!.Address);
            Assert.DoesNotContain("source-secret", json);
            Assert.DoesNotContain("purge-secret", json);

            await store.ClearAsync(RabbitMqClientRole.QueuePurge, cancellationToken);
            Assert.NotNull(await store.LoadAsync(RabbitMqClientRole.SourceReadOnly, cancellationToken));
            Assert.Null(await store.LoadAsync(RabbitMqClientRole.QueuePurge, cancellationToken));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static RabbitMqSavedConnection Connection(string address, string password) =>
        new(address, "admin", password, 15, true, "/");
}
