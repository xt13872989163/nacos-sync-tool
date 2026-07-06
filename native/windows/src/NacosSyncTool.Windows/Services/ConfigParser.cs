using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NacosSyncTool.Windows.Services;

/// <summary>
/// 配置格式
/// </summary>
public enum ConfigFormat
{
    Yaml,
    Json,
    Properties
}

/// <summary>
/// Key 值匹配结果
/// </summary>
public class KeyValueMatch
{
    public string KeyPath { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 配置解析器
/// 支持 YAML / JSON / Properties 三种格式
/// </summary>
public static class ConfigParser
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .Build();

    /// <summary>
    /// 检测配置格式
    /// </summary>
    public static ConfigFormat DetectConfigFormat(string dataId, string? type)
    {
        var normalizedType = type?.Trim().ToLowerInvariant();
        var normalizedDataId = dataId.ToLowerInvariant();

        if (normalizedType == "yaml" || normalizedType == "yml"
            || normalizedDataId.EndsWith(".yml") || normalizedDataId.EndsWith(".yaml"))
        {
            return ConfigFormat.Yaml;
        }

        if (normalizedType == "json" || normalizedDataId.EndsWith(".json"))
        {
            return ConfigFormat.Json;
        }

        return ConfigFormat.Properties;
    }

    /// <summary>
    /// 在配置内容中查找指定 Key
    /// </summary>
    public static KeyValueMatch? FindKeyValue(string content, string keyPath, string dataId, string? type)
    {
        var format = DetectConfigFormat(dataId, type);

        if (format == ConfigFormat.Properties)
        {
            var properties = ParseProperties(content);
            return properties.TryGetValue(keyPath, out var propValue)
                ? new KeyValueMatch { KeyPath = keyPath, Value = propValue }
                : null;
        }

        var parsed = ParseStructuredContent(content, format);
        var nestedValue = GetNestedValue(parsed, keyPath);

        return nestedValue == null ? null : new KeyValueMatch { KeyPath = keyPath, Value = nestedValue };
    }

    /// <summary>
    /// 插入或更新指定 Key 的值
    /// </summary>
    public static string UpsertKeyValue(string content, string keyPath, string value, string dataId, string? type)
    {
        var format = DetectConfigFormat(dataId, type);

        if (format == ConfigFormat.Properties)
        {
            return UpsertPropertiesValue(content, keyPath, value);
        }

        var parsed = ParseStructuredContent(content, format);
        var dict = parsed as Dictionary<string, object> ?? new Dictionary<string, object>();
        SetNestedValue(dict, keyPath, value);

        if (format == ConfigFormat.Json)
        {
            return JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        }

        return YamlSerializer.Serialize(dict);
    }

    /// <summary>
    /// 解析结构化内容（YAML/JSON）
    /// </summary>
    private static object? ParseStructuredContent(string content, ConfigFormat format)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            if (format == ConfigFormat.Json)
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(content);
            }

            return YamlDeserializer.Deserialize<Dictionary<string, object>>(content);
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 获取嵌套值（支持 a.b.c 路径）
    /// </summary>
    private static string? GetNestedValue(object? input, string keyPath)
    {
        var current = input;
        foreach (var segment in keyPath.Split('.'))
        {
            if (current is not Dictionary<string, object> dict || !dict.TryGetValue(segment, out var value))
            {
                return null;
            }
            current = value;
        }

        return current?.ToString();
    }

    /// <summary>
    /// 设置嵌套值
    /// </summary>
    private static void SetNestedValue(Dictionary<string, object> input, string keyPath, string value)
    {
        var segments = keyPath.Split('.');
        var current = input;

        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            if (i == segments.Length - 1)
            {
                current[segment] = value;
                return;
            }

            if (!current.TryGetValue(segment, out var existing)
                || existing is not Dictionary<string, object>)
            {
                var nested = new Dictionary<string, object>();
                current[segment] = nested;
                current = nested;
            }
            else
            {
                current = (Dictionary<string, object>)existing;
            }
        }
    }

    /// <summary>
    /// 解析 Properties 格式
    /// </summary>
    private static Dictionary<string, string> ParseProperties(string content)
    {
        var properties = new Dictionary<string, string>();

        foreach (var line in content.Split('\n', '\r'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith('!'))
            {
                continue;
            }

            var separatorIndex = FindPropertySeparatorIndex(line);
            if (separatorIndex == -1)
            {
                continue;
            }

            var key = line.Substring(0, separatorIndex).Trim();
            var value = line.Substring(separatorIndex + 1).Trim();
            properties[key] = value;
        }

        return properties;
    }

    /// <summary>
    /// 插入或更新 Properties 值
    /// </summary>
    private static string UpsertPropertiesValue(string content, string keyPath, string value)
    {
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
        bool updated = false;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith('!'))
            {
                continue;
            }

            var separatorIndex = FindPropertySeparatorIndex(line);
            if (separatorIndex == -1)
            {
                continue;
            }

            var key = line.Substring(0, separatorIndex).Trim();
            if (key == keyPath)
            {
                lines[i] = $"{keyPath}={value}";
                updated = true;
            }
        }

        if (!updated)
        {
            if (lines.Count > 0 && lines[lines.Count - 1] != "")
            {
                lines.Add($"{keyPath}={value}");
            }
            else if (lines.Count > 0)
            {
                lines[lines.Count - 1] = $"{keyPath}={value}";
            }
            else
            {
                lines.Add($"{keyPath}={value}");
            }
        }

        return string.Join("\n", lines) + "\n";
    }

    /// <summary>
    /// 查找 Properties 分隔符位置（= 或 :）
    /// </summary>
    private static int FindPropertySeparatorIndex(string line)
    {
        int equalIndex = line.IndexOf('=');
        int colonIndex = line.IndexOf(':');

        if (equalIndex == -1) return colonIndex;
        if (colonIndex == -1) return equalIndex;
        return Math.Min(equalIndex, colonIndex);
    }
}
