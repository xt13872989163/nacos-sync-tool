using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using NacosSyncTool.Windows.Models;

namespace NacosSyncTool.Windows.Services;

/// <summary>
/// Nacos API 服务
/// 支持 Nacos 1.x 和 2.x 版本
/// 每个实例对应一个 Nacos 连接（源端或目标端）
/// </summary>
public class NacosApiService
{
    private readonly HttpClient _httpClient;
    private string? _accessToken;
    private string _address = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;

    public NacosApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// 设置连接信息
    /// </summary>
    public void SetConnection(string address, string username, string password)
    {
        _address = NormalizeAddress(address);
        _username = username;
        _password = password;
        _accessToken = null;
    }

    /// <summary>
    /// 测试连接并登录
    /// </summary>
    public async Task<ApiResult<string>> TestConnectionAsync(string address, string username, string password)
    {
        try
        {
            SetConnection(address, username, password);
            var loginResult = await LoginAsync();

            if (!loginResult.Success)
            {
                return ApiResult<string>.Fail(loginResult.Message ?? "登录失败");
            }

            // 尝试拉取 Namespace 验证连接
            var nsResult = await GetNamespacesAsync();
            if (!nsResult.Success)
            {
                return ApiResult<string>.Fail($"连接成功但获取 Namespace 失败：{nsResult.Message}");
            }

            return ApiResult<string>.Ok($"连接成功：{_address}");
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
    public async Task<ApiResult> LoginAsync()
    {
        try
        {
            var loginUrl = $"{_address}/nacos/v1/auth/login";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", _username),
                new KeyValuePair<string, string>("password", _password)
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

            // 某些 Nacos 部署未开启鉴权，登录接口可能返回其他内容，视为免鉴权
            _accessToken = null;
            return ApiResult.Ok();
        }
        catch (Exception ex)
        {
            return ApiResult.Fail($"登录异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 确保已登录
    /// </summary>
    private async Task EnsureAuthenticatedAsync()
    {
        if (_accessToken == null)
        {
            await LoginAsync();
        }
    }

    /// <summary>
    /// 获取所有 Namespace 列表
    /// </summary>
    public async Task<ApiResult<List<NacosNamespace>>> GetNamespacesAsync()
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var url = BuildUrl("/nacos/v1/console/namespaces");

            var response = await _httpClient.GetAsync(url);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<List<NacosNamespace>>.Fail($"获取 Namespace 失败：HTTP {response.StatusCode}");
            }

            var namespaceResponse = JsonSerializer.Deserialize<NacosNamespaceResponse>(responseBody);

            var namespaces = new List<NacosNamespace>();

            if (namespaceResponse?.Data != null)
            {
                namespaces = namespaceResponse.Data.Select(ns => new NacosNamespace
                {
                    NamespaceId = ns.Namespace ?? string.Empty,
                    NamespaceName = ns.NamespaceShowName ?? string.Empty,
                    NamespaceDesc = ns.NamespaceDesc
                }).ToList();
            }

            // 如果没有 Namespace，添加默认 public
            if (namespaces.Count == 0)
            {
                namespaces.Add(new NacosNamespace
                {
                    NamespaceId = string.Empty,
                    NamespaceName = "public",
                    NamespaceDesc = "public"
                });
            }

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
    public async Task<ApiResult> CreateNamespaceAsync(string namespaceId, string namespaceName, string? namespaceDesc = null)
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var url = BuildUrl("/nacos/v1/console/namespaces");

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
    /// 获取指定 Namespace 下的所有配置列表（自动分页）
    /// </summary>
    public async Task<ApiResult<List<NacosConfigItem>>> ListConfigsAsync(string namespaceId, int pageSize = 100)
    {
        try
        {
            await EnsureAuthenticatedAsync();

            var items = new List<NacosConfigItem>();
            int pageNo = 1;
            int totalCount = int.MaxValue;

            while (items.Count < totalCount)
            {
                var query = HttpUtility.ParseQueryString(string.Empty);
                query["search"] = "blur";
                query["dataId"] = "";
                query["group"] = "";
                query["appName"] = "";
                query["config_tags"] = "";
                query["pageNo"] = pageNo.ToString();
                query["pageSize"] = pageSize.ToString();
                query["tenant"] = namespaceId;
                AppendAuth(query);

                var url = $"{_address}/nacos/v1/cs/configs?{query}";
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return ApiResult<List<NacosConfigItem>>.Fail($"获取配置列表失败：HTTP {response.StatusCode}");
                }

                var listResponse = JsonSerializer.Deserialize<ConfigListResponse>(responseBody);

                var pageItems = listResponse?.PageItems ?? new List<ConfigItemData>();
                totalCount = listResponse?.TotalCount ?? pageItems.Count;

                foreach (var item in pageItems)
                {
                    items.Add(new NacosConfigItem
                    {
                        DataId = item.DataId ?? string.Empty,
                        Group = item.Group ?? "DEFAULT_GROUP",
                        Content = item.Content ?? string.Empty,
                        Type = item.Type
                    });
                }

                if (pageItems.Count == 0 || items.Count >= totalCount)
                {
                    break;
                }

                pageNo++;
            }

            // 拉取每个配置的完整内容（列表接口可能不返回 content）
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Content))
                {
                    var contentResult = await GetConfigAsync(namespaceId, item.DataId, item.Group);
                    if (contentResult.Success && contentResult.Data != null)
                    {
                        item.Content = contentResult.Data;
                    }
                }
            }

            return ApiResult<List<NacosConfigItem>>.Ok(items);
        }
        catch (Exception ex)
        {
            return ApiResult<List<NacosConfigItem>>.Fail($"获取配置列表异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 获取单个配置内容
    /// </summary>
    public async Task<ApiResult<string?>> GetConfigAsync(string namespaceId, string dataId, string group)
    {
        try
        {
            await EnsureAuthenticatedAsync();

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["tenant"] = namespaceId;
            query["dataId"] = dataId;
            query["group"] = group;
            AppendAuth(query);

            var url = $"{_address}/nacos/v1/cs/configs?{query}";
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ApiResult<string?>.Ok(null);
            }

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<string?>.Fail($"获取配置失败：HTTP {response.StatusCode}");
            }

            return ApiResult<string?>.Ok(responseBody);
        }
        catch (Exception ex)
        {
            return ApiResult<string?>.Fail($"获取配置异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 发布配置
    /// </summary>
    public async Task<ApiResult> PublishConfigAsync(string namespaceId, NacosConfigItem item)
    {
        try
        {
            await EnsureAuthenticatedAsync();

            var url = BuildUrl("/nacos/v1/cs/configs");

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("tenant", namespaceId),
                new KeyValuePair<string, string>("dataId", item.DataId),
                new KeyValuePair<string, string>("group", item.Group),
                new KeyValuePair<string, string>("content", item.Content),
                new KeyValuePair<string, string>("type", NormalizeConfigType(item.Type, item.DataId))
            });

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ApiResult.Fail($"发布配置失败：HTTP {response.StatusCode}");
            }

            if (responseBody.Contains("true"))
            {
                return ApiResult.Ok();
            }

            return ApiResult.Fail($"发布配置失败：{responseBody}");
        }
        catch (Exception ex)
        {
            return ApiResult.Fail($"发布配置异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 构建带鉴权的 URL
    /// </summary>
    private string BuildUrl(string path)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        AppendAuth(query);
        var queryString = query.ToString();
        return string.IsNullOrEmpty(queryString)
            ? $"{_address}{path}"
            : $"{_address}{path}?{queryString}";
    }

    /// <summary>
    /// 追加鉴权参数
    /// </summary>
    private void AppendAuth(System.Collections.Specialized.NameValueCollection query)
    {
        if (!string.IsNullOrEmpty(_accessToken))
        {
            query["accessToken"] = _accessToken;
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

    /// <summary>
    /// 规范化配置类型
    /// </summary>
    private string NormalizeConfigType(string? type, string dataId)
    {
        var normalizedType = type?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(normalizedType))
        {
            return normalizedType == "yml" ? "yaml" : normalizedType;
        }

        var lowerDataId = dataId.ToLowerInvariant();
        if (lowerDataId.EndsWith(".yml") || lowerDataId.EndsWith(".yaml")) return "yaml";
        if (lowerDataId.EndsWith(".json")) return "json";
        if (lowerDataId.EndsWith(".properties")) return "properties";
        if (lowerDataId.EndsWith(".xml")) return "xml";
        if (lowerDataId.EndsWith(".html") || lowerDataId.EndsWith(".htm")) return "html";

        return "text";
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

    private class ConfigListResponse
    {
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; set; }

        [JsonPropertyName("pagesAvailable")]
        public int PagesAvailable { get; set; }

        [JsonPropertyName("pageItems")]
        public List<ConfigItemData>? PageItems { get; set; }
    }

    private class ConfigItemData
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("dataId")]
        public string? DataId { get; set; }

        [JsonPropertyName("group")]
        public string? Group { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("md5")]
        public string? Md5 { get; set; }
    }
}
