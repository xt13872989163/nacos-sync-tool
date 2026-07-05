namespace NacosSyncTool.Windows.Models;

/// <summary>
/// API 响应结果
/// </summary>
public class ApiResult<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResult<T> Fail(string message) => new() { Success = false, Message = message };
}

public class ApiResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }

    public static ApiResult Ok() => new() { Success = true };
    public static ApiResult Fail(string message) => new() { Success = false, Message = message };
}
