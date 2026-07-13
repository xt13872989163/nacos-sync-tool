using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public sealed class RabbitMqManagementClient : IRabbitMqManagementClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex PurgePathPattern = new(
        @"^api/queues/[^/]+/[^/]+/contents$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly RabbitMqClientRole _role;
    private readonly HttpClient _httpClient;

    public RabbitMqManagementClient(
        RabbitMqConnectionSettings settings,
        RabbitMqClientRole role,
        HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _role = role;

        var baseAddress = NormalizeBaseAddress(settings.Address);
        handler ??= CreateHandler(settings.ValidateServerCertificate);
        _httpClient = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = baseAddress,
            Timeout = settings.Timeout
        };

        var credential = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credential);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<RabbitMqConnectionInfo> TestConnectionAsync(CancellationToken cancellationToken) =>
        GetOverviewAsync(cancellationToken);

    public async Task<IReadOnlyList<RabbitMqVirtualHost>> GetVirtualHostsAsync(
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<List<VirtualHostDto>>("api/vhosts", cancellationToken);
        return response.Select(item => new RabbitMqVirtualHost(
                item.Name ?? string.Empty,
                item.Description,
                ParseTags(item.Tags),
                item.DefaultQueueType))
            .ToList();
    }

    public async Task<IReadOnlyList<RabbitMqExchange>> GetExchangesAsync(
        string virtualHost,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<List<ExchangeDto>>(
            $"api/exchanges/{Segment(virtualHost)}",
            cancellationToken);
        return response.Select(item => new RabbitMqExchange(
                item.Name ?? string.Empty,
                item.Type ?? string.Empty,
                item.Durable,
                item.AutoDelete,
                item.Internal,
                item.Arguments ?? new Dictionary<string, object?>()))
            .ToList();
    }

    public async Task<IReadOnlyList<RabbitMqQueue>> GetQueuesAsync(
        string virtualHost,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<List<QueueDto>>(
            $"api/queues/{Segment(virtualHost)}",
            cancellationToken);
        return response.Select(item => new RabbitMqQueue(
                item.Name ?? string.Empty,
                item.Durable,
                item.AutoDelete,
                item.Exclusive,
                item.Arguments ?? new Dictionary<string, object?>(),
                item.MessagesReady,
                item.MessagesUnacknowledged,
                item.Consumers))
            .ToList();
    }

    public async Task<IReadOnlyList<RabbitMqBinding>> GetBindingsAsync(
        string virtualHost,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<List<BindingDto>>(
            $"api/bindings/{Segment(virtualHost)}",
            cancellationToken);
        return response.Select(item => new RabbitMqBinding(
                item.Source ?? string.Empty,
                item.Destination ?? string.Empty,
                string.Equals(item.DestinationType, "exchange", StringComparison.OrdinalIgnoreCase)
                    ? RabbitMqBindingDestination.Exchange
                    : RabbitMqBindingDestination.Queue,
                item.RoutingKey ?? string.Empty,
                item.Arguments ?? new Dictionary<string, object?>()))
            .ToList();
    }

    public async Task<IReadOnlyList<RabbitMqPolicy>> GetPoliciesAsync(
        string virtualHost,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<List<PolicyDto>>(
            $"api/policies/{Segment(virtualHost)}",
            cancellationToken);
        return response.Select(item => new RabbitMqPolicy(
                item.Name ?? string.Empty,
                item.Pattern ?? string.Empty,
                item.Definition ?? new Dictionary<string, object?>(),
                item.Priority,
                item.ApplyTo ?? "all"))
            .ToList();
    }

    public async Task CreateVirtualHostAsync(
        RabbitMqVirtualHost virtualHost,
        CancellationToken cancellationToken)
    {
        EnsureTopologyWriter();
        var body = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(virtualHost.Description))
            body["description"] = virtualHost.Description;
        if (virtualHost.Tags.Count > 0)
            body["tags"] = string.Join(',', virtualHost.Tags);
        if (!string.IsNullOrWhiteSpace(virtualHost.DefaultQueueType))
            body["default_queue_type"] = virtualHost.DefaultQueueType;

        await SendJsonAsync(
            HttpMethod.Put,
            $"api/vhosts/{Segment(virtualHost.Name)}",
            body,
            cancellationToken);
    }

    public Task CreateExchangeAsync(
        string virtualHost,
        RabbitMqExchange exchange,
        CancellationToken cancellationToken)
    {
        EnsureTopologyWriter();
        return SendJsonAsync(
            HttpMethod.Put,
            $"api/exchanges/{Segment(virtualHost)}/{Segment(exchange.Name)}",
            new
            {
                type = exchange.Type,
                durable = exchange.Durable,
                auto_delete = exchange.AutoDelete,
                @internal = exchange.Internal,
                arguments = exchange.Arguments
            },
            cancellationToken);
    }

    public Task CreateQueueAsync(
        string virtualHost,
        RabbitMqQueue queue,
        CancellationToken cancellationToken)
    {
        EnsureTopologyWriter();
        return SendJsonAsync(
            HttpMethod.Put,
            $"api/queues/{Segment(virtualHost)}/{Segment(queue.Name)}",
            new
            {
                durable = queue.Durable,
                auto_delete = queue.AutoDelete,
                arguments = queue.Arguments
            },
            cancellationToken);
    }

    public Task CreateBindingAsync(
        string virtualHost,
        RabbitMqBinding binding,
        CancellationToken cancellationToken)
    {
        EnsureTopologyWriter();
        var destinationType = binding.DestinationType == RabbitMqBindingDestination.Queue ? "q" : "e";
        return SendJsonAsync(
            HttpMethod.Post,
            $"api/bindings/{Segment(virtualHost)}/e/{Segment(binding.Source)}/{destinationType}/{Segment(binding.Destination)}",
            new
            {
                routing_key = binding.RoutingKey,
                arguments = binding.Arguments
            },
            cancellationToken);
    }

    public Task CreatePolicyAsync(
        string virtualHost,
        RabbitMqPolicy policy,
        CancellationToken cancellationToken)
    {
        EnsureTopologyWriter();
        var body = new Dictionary<string, object?>
        {
            ["pattern"] = policy.Pattern,
            ["definition"] = policy.Definition,
            ["priority"] = policy.Priority,
            ["apply-to"] = policy.ApplyTo
        };
        return SendJsonAsync(
            HttpMethod.Put,
            $"api/policies/{Segment(virtualHost)}/{Segment(policy.Name)}",
            body,
            cancellationToken);
    }

    public Task PurgeQueueAsync(
        string virtualHost,
        string queueName,
        CancellationToken cancellationToken)
    {
        var path = $"api/queues/{Segment(virtualHost)}/{Segment(queueName)}/contents";
        return SendPurgeAsync(path, cancellationToken);
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<RabbitMqConnectionInfo> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var overview = await GetAsync<OverviewDto>("api/overview", cancellationToken);
        return new RabbitMqConnectionInfo(
            overview.RabbitMqVersion ?? string.Empty,
            overview.ClusterName ?? string.Empty);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return value ?? throw new RabbitMqApiException(
            response.StatusCode,
            path,
            "Response body was empty.");
    }

    private async Task SendJsonAsync(
        HttpMethod method,
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);
    }

    private async Task SendPurgeAsync(string path, CancellationToken cancellationToken)
    {
        if (_role != RabbitMqClientRole.QueuePurge || !PurgePathPattern.IsMatch(path))
        {
            throw new InvalidOperationException("Only Queue /contents purge is allowed.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);
    }

    private void EnsureTopologyWriter()
    {
        if (_role != RabbitMqClientRole.TargetTopology)
        {
            throw new InvalidOperationException(
                $"RabbitMQ client role {_role} cannot create topology resources.");
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var summary = body.Length <= 1000 ? body : body[..1000];
        throw new RabbitMqApiException(response.StatusCode, path, summary);
    }

    private static Uri NormalizeBaseAddress(string address)
    {
        if (!Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("RabbitMQ Management API address must be an absolute HTTP or HTTPS URL.", nameof(address));
        }

        var normalized = uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri.AbsoluteUri
            : uri.AbsoluteUri + "/";
        return new Uri(normalized, UriKind.Absolute);
    }

    private static HttpMessageHandler CreateHandler(bool validateServerCertificate)
    {
        var handler = new HttpClientHandler();
        if (!validateServerCertificate)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    }

    private static string Segment(string value) => Uri.EscapeDataString(value ?? string.Empty);

    private static IReadOnlyList<string> ParseTags(JsonElement tags)
    {
        if (tags.ValueKind == JsonValueKind.Array)
        {
            return tags.EnumerateArray()
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToList();
        }

        if (tags.ValueKind == JsonValueKind.String)
        {
            return (tags.GetString() ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return Array.Empty<string>();
    }

    private sealed class OverviewDto
    {
        [JsonPropertyName("rabbitmq_version")]
        public string? RabbitMqVersion { get; set; }

        [JsonPropertyName("cluster_name")]
        public string? ClusterName { get; set; }
    }

    private sealed class VirtualHostDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("tags")]
        public JsonElement Tags { get; set; }

        [JsonPropertyName("default_queue_type")]
        public string? DefaultQueueType { get; set; }
    }

    private sealed class ExchangeDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("durable")]
        public bool Durable { get; set; }

        [JsonPropertyName("auto_delete")]
        public bool AutoDelete { get; set; }

        [JsonPropertyName("internal")]
        public bool Internal { get; set; }

        [JsonPropertyName("arguments")]
        public Dictionary<string, object?>? Arguments { get; set; }
    }

    private sealed class QueueDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("durable")]
        public bool Durable { get; set; }

        [JsonPropertyName("auto_delete")]
        public bool AutoDelete { get; set; }

        [JsonPropertyName("exclusive")]
        public bool Exclusive { get; set; }

        [JsonPropertyName("arguments")]
        public Dictionary<string, object?>? Arguments { get; set; }

        [JsonPropertyName("messages_ready")]
        public long MessagesReady { get; set; }

        [JsonPropertyName("messages_unacknowledged")]
        public long MessagesUnacknowledged { get; set; }

        [JsonPropertyName("consumers")]
        public int Consumers { get; set; }
    }

    private sealed class BindingDto
    {
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("destination")]
        public string? Destination { get; set; }

        [JsonPropertyName("destination_type")]
        public string? DestinationType { get; set; }

        [JsonPropertyName("routing_key")]
        public string? RoutingKey { get; set; }

        [JsonPropertyName("arguments")]
        public Dictionary<string, object?>? Arguments { get; set; }
    }

    private sealed class PolicyDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("pattern")]
        public string? Pattern { get; set; }

        [JsonPropertyName("definition")]
        public Dictionary<string, object?>? Definition { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("apply-to")]
        public string? ApplyTo { get; set; }
    }
}
