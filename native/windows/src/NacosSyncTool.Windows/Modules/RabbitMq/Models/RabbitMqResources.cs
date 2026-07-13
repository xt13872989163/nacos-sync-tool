using CommunityToolkit.Mvvm.ComponentModel;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Models;

public enum RabbitMqBindingDestination
{
    Queue,
    Exchange
}

public sealed record RabbitMqVirtualHost(
    string Name,
    string? Description,
    IReadOnlyList<string> Tags,
    string? DefaultQueueType);

public sealed record RabbitMqExchange(
    string Name,
    string Type,
    bool Durable,
    bool AutoDelete,
    bool Internal,
    Dictionary<string, object?> Arguments);

public sealed record RabbitMqQueue(
    string Name,
    bool Durable,
    bool AutoDelete,
    bool Exclusive,
    Dictionary<string, object?> Arguments,
    long MessagesReady,
    long MessagesUnacknowledged,
    int Consumers)
{
    public long MessagesTotal => MessagesReady + MessagesUnacknowledged;
}

public sealed record RabbitMqPolicy(
    string Name,
    string Pattern,
    Dictionary<string, object?> Definition,
    int Priority,
    string ApplyTo);

public sealed record RabbitMqBinding(
    string Source,
    string Destination,
    RabbitMqBindingDestination DestinationType,
    string RoutingKey,
    Dictionary<string, object?> Arguments)
{
    public string Identity => $"{Source}\u001f{Destination}\u001f{DestinationType}\u001f{RoutingKey}";
}

public sealed record RabbitMqTopologySnapshot(
    RabbitMqVirtualHost VirtualHost,
    IReadOnlyList<RabbitMqExchange> Exchanges,
    IReadOnlyList<RabbitMqPolicy> Policies,
    IReadOnlyList<RabbitMqQueue> Queues,
    IReadOnlyList<RabbitMqBinding> Bindings);

public partial class RabbitMqSelectionItem<T> : ObservableObject
{
    public T Resource { get; }
    public string Identity { get; }

    [ObservableProperty]
    private bool _isSelected;

    public RabbitMqSelectionItem(T resource, string identity, bool isSelected = true)
    {
        Resource = resource;
        Identity = identity;
        _isSelected = isSelected;
    }
}
