namespace TableOrder.Server.Core.Models.Entity;

// 呼び出し。読み出すときは、来店の今のテーブルを足す
public sealed class CallEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid VisitId { get; set; }

    public string ReasonCode { get; set; } = default!;

    public CallStatus Status { get; set; }

    public Guid? DeviceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? DoneAt { get; set; }

    // 読み出しで足す値
    public Guid TableId { get; set; }

    public string TableName { get; set; } = default!;
}
