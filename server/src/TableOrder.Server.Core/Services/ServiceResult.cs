namespace TableOrder.Server.Core.Services;

// 業務の処理の失敗。入口が errorCode から HTTP の状態と文言を決める
public sealed record ServiceError(string ErrorCode, IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static ServiceError NotFound { get; } = new(ErrorCodes.NotFound);

    public static ServiceError DeviceScope { get; } = new(ErrorCodes.DeviceScope);

    // 入力の誤り (項目ごとの理由)
    public static ServiceError Validation(string key, string message) =>
        new(ErrorCodes.ValidationError, new Dictionary<string, string[]> { [key] = [message] });
}

// 業務の処理の結果。成功は値と、新しく作ったか (同じ Id の送り直しで既にあったものは作っていない)
public sealed class ServiceResult<T>
    where T : class
{
    public T? Value { get; }

    public ServiceError? Error { get; }

    public bool Created { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    public ServiceResult(T value, bool created = false)
    {
        Value = value;
        Created = created;
    }

    public ServiceResult(ServiceError error)
    {
        Error = error;
    }
}
