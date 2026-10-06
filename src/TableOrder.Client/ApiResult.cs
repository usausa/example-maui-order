namespace TableOrder.Client;

// 通信の方式 (REST / gRPC) によらない結果の分類
public enum ApiStatus
{
    Success,
    // 業務のルールや入力の誤りで受け付けられなかった (REST の 4xx、gRPC の FAILED_PRECONDITION など)
    Rejected,
    // 端末の登録が無効 (REST の 401、gRPC の UNAUTHENTICATED)
    Unauthorized,
    // 通信できない・時間切れ・サーバの障害 (送り直せば通る見込みがある)
    Unavailable,
    Canceled
}

// API の呼び出しの結果。例外を投げずに返し、失敗はサーバの errorCode と文言を持つ
public sealed class ApiResult<T>
{
    public ApiStatus Status { get; }

    public T? Content { get; }

    public string? ErrorCode { get; }

    // サーバが返した利用者向けの文言 (Problem Details の title など)
    public string? Detail { get; }

    public Exception? Exception { get; }

    public bool IsSuccess => Status == ApiStatus.Success;

    public ApiResult(ApiStatus status, T? content, string? errorCode, string? detail, Exception? exception)
    {
        Status = status;
        Content = content;
        ErrorCode = errorCode;
        Detail = detail;
        Exception = exception;
    }
}

public static class ApiResult
{
    public static ApiResult<T> Success<T>(T content) =>
        new(ApiStatus.Success, content, null, null, null);

    public static ApiResult<T> Failure<T>(ApiStatus status, string? errorCode = null, string? detail = null, Exception? exception = null) =>
        new(status, default, errorCode, detail, exception);
}
