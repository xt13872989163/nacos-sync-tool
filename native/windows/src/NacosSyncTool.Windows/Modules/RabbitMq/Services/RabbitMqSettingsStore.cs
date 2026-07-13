using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NacosSyncTool.Windows.Modules.RabbitMq.Models;

namespace NacosSyncTool.Windows.Modules.RabbitMq.Services;

public sealed record RabbitMqSavedConnection(
    string Address,
    string Username,
    string Password,
    int TimeoutSeconds,
    bool ValidateServerCertificate,
    string? SelectedVirtualHost);

public sealed class RabbitMqSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RabbitMqSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NacosSyncTool",
            "rabbitmq-settings.json");
    }

    public async Task<RabbitMqSavedConnection?> LoadAsync(
        RabbitMqClientRole role,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            if (!document.Connections.TryGetValue(role.ToString(), out var stored)) return null;
            return new RabbitMqSavedConnection(
                stored.Address,
                stored.Username,
                Decrypt(stored.EncryptedPassword),
                stored.TimeoutSeconds,
                stored.ValidateServerCertificate,
                stored.SelectedVirtualHost);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        RabbitMqClientRole role,
        RabbitMqSavedConnection connection,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            document.Connections[role.ToString()] = new StoredConnection
            {
                Address = connection.Address,
                Username = connection.Username,
                EncryptedPassword = Encrypt(connection.Password),
                TimeoutSeconds = connection.TimeoutSeconds,
                ValidateServerCertificate = connection.ValidateServerCertificate,
                SelectedVirtualHost = connection.SelectedVirtualHost
            };
            await WriteDocumentAsync(document, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(RabbitMqClientRole role, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            document.Connections.Remove(role.ToString());
            await WriteDocumentAsync(document, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SettingsDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return new SettingsDocument();
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<SettingsDocument>(stream, JsonOptions, cancellationToken)
            ?? new SettingsDocument();
    }

    private async Task WriteDocumentAsync(
        SettingsDocument document,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        }
        File.Move(temporaryPath, _path, true);
    }

    private static string Encrypt(string value)
    {
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(value),
            null,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string Decrypt(string value)
    {
        var unprotectedBytes = ProtectedData.Unprotect(
            Convert.FromBase64String(value),
            null,
            DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(unprotectedBytes);
    }

    private sealed class SettingsDocument
    {
        public SettingsDocument() { }

        public Dictionary<string, StoredConnection> Connections { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class StoredConnection
    {
        public StoredConnection() { }

        public string Address { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string EncryptedPassword { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 15;
        public bool ValidateServerCertificate { get; set; } = true;
        public string? SelectedVirtualHost { get; set; }
    }
}
