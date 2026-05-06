namespace MyProjectBase.Services;

public sealed record ServiceResult(bool Success, string Message)
{
    public static ServiceResult Ok(string message = "") => new(true, message);
    public static ServiceResult Fail(string message) => new(false, message);
}

public sealed record ServiceResult<T>(bool Success, T? Value, string Message)
{
    public static ServiceResult<T> Ok(T value, string message = "") => new(true, value, message);
    public static ServiceResult<T> Fail(string message) => new(false, default, message);
}
