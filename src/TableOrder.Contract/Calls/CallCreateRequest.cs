namespace TableOrder.Contract.Calls;

// 店員の呼び出し。同じ用件の呼び出しが開いていれば、新しく作らずにそれを返す
public sealed class CallCreateRequest
{
    public Guid Id { get; set; }

    public string ReasonCode { get; set; } = default!;
}
