using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NacosSyncTool.Windows.Models;

namespace NacosSyncTool.Windows.Services;

/// <summary>
/// Nacos API 服务
/// 支持 Nacos 1.x 和 2.x 版本
/// </summary>
public class NacosApiService
{
    private readonly HttpClient _httpClient;
    private string? _accessToken;

    public NacosApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// 测试连接并登录
    /// </summary>
    public async Task<ApiResult<string>> TestConnectionAsync(string address, string username, string password)
    {
        try
        {
            var normalizedAddress = NormalizeAddress(address);
            var loginResult = await LoginAsync(normalizedAddress, username, password);

            if (!loginResult.Success)
            {
                return ApiResult<string>.Fail(loginResult.Message ?? "登录失败");
            }

            return ApiResult<string>.Ok($"连接成功：{normalizedAddress}");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<string>.Fail($"网络错误：{ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ApiResult<string>.Fail("连接超时，请检查地址是否正确");
        }
        catch (Exception ex)
        {
            return ApiResult<string>.Fail($"连接失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 登录获取 AccessToken
    /// </summary>
    public async Task<ApiResult> LoginAsync(string address, string username, string password)
    {
        try
        {
            var normalizedAddress = NormalizeAddress(address);
            var loginUrl = $"{normalizedAddress}/nacos/v1/auth/login";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            });

            var response = await _httpClient.PostAsync(loginUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ApiResult.Fail($"登录失败：HTTP {response.StatusCode}");
            }

            var loginResponse = JsonSerializer.Deserialize<NacosLoginResponse>(responseBody);

            if (loginResponse?.AccessToken != null)
            {
                _accessToken = loginResponse.AccessToken;
                return ApiResult.Ok();
            }

            return ApiResult.Fail("登录失败：未获取到有效 Token");
        }
        catch (Exception ex)
        {
            return ApiResult.Fail($"登录异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有 Namespace 列表
    /// </summary>
    public async Task<ApiResult<List<NacosNamespace>>> GetNamespacesAsync(string address)
    {
        try
        {
            var normalizedAddress = NormalizeAddress(address);
            var url = $"{normalizedAddress}/nacos/v1/console/namespaces";

            if (!string.IsNullOrEmpty(_accessToken))
            {
                url += $"?accessToken={_accessToken}";
            }

            var response = await _httpClient.GetAsync(url);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<List<NacosNamespace>>.Fail($"获取 Namespace 失败：HTTP {response.StatusCode}");
            }

            var namespaceResponse = JsonSerializer.Deserialize<NacosNamespaceResponse>(responseBody);

            if (namespaceResponse?.Data == null)
            {
                return ApiResult<List<NacosNamespace>>.Fail("获取 Namespace 失败：返回数据为空");
            }

            var namespaces = namespaceResponse.Data.Select(ns => new NacosNamespace
            {
                NamespaceId = ns.Namespace ?? string.Empty,
                NamespaceName = ns.NamespaceShowName ?? string.Empty,
                NamespaceDesc = ns.NamespaceDesc
            }).ToList();

            return ApiResult<List<NacosNamespace>>.Ok(namespaces);
        }
        catch (Exception ex)
        {
            return ApiResult<List<NacosNamespace>>.Fail($"获取 Namespace 异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 创建新的 Namespace
    /// </summary>
    public async Task<ApiResult> CreateNamespaceAsync(string address, string namespaceId, string namespaceName, string? namespaceDesc = null)
    {
        try
        {
            var normalizedAddress = NormalizeAddress(address);
            var url = $"{normalizedAddress}/nacos/v1/console/namespaces";

            if (!string.IsNullOrEmpty(_accessToken))
            {
                url += $"?accessToken={_accessToken}";
            }

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("customNamespaceId", namespaceId),
                new KeyValuePair<string, string>("namespaceName", namespaceName),
                new KeyValuePair<string, string>("namespaceDesc", namespaceDesc ?? string.Empty)
            });

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ApiResult.Fail($"创建 Namespace 失败：HTTP {response.StatusCode}");
            }

            // Nacos 创建成功返回 true
            if (responseBody.Contains("true"))
            {
                return ApiResult.Ok();
            }

            return ApiResult.Fail($"创建 Namespace 失败：{responseBody}");
        }
        catch (Exception ex)
        {
            return ApiResult.Fail($"创建 Namespace 异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 规范化地址格式
    /// </summary>
    private string NormalizeAddress(string address)
    {
        address = address.Trim();

        if (!address.StartsWith("http://") && !address.StartsWith("https://"))
        {
            address = "http://" + address;
        }

        return address.TrimEnd('/');
    }

    // ===== 内部响应模型 =====

    private class NacosLoginResponse
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("tokenTtl")]
        public int TokenTtl { get; set; }

        [JsonPropertyName("globalAdmin")]
        public bool GlobalAdmin { get; set; }
    }

    private class NacosNamespaceResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public List<NamespaceData>? Data { get; set; }
    }

    private class NamespaceData
    {
        [JsonPropertyName("namespace")]
        public string? Namespace { get; set; }

        [JsonPropertyName("namespaceShowName")]
        public string? NamespaceShowName { get; set; }

        [JsonPropertyName("namespaceDesc")]
        public string? NamespaceDesc { get; set; }

        [JsonPropertyName("quota")]
        public int Quota { get; set; }

        [JsonPropertyName("configCount")]
        public int ConfigCount { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }
    }
}
