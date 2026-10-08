namespace TableOrder.Contract.Stores;

// テーブルと今の来店の要約 (ホールの席の一覧、受付機の空席)
public sealed class TableListResponse
{
    public IReadOnlyList<TableListResponseItem> Items { get; set; } = default!;
}

public sealed class TableListResponseItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Area { get; set; }

    public int Capacity { get; set; }

    public int SortOrder { get; set; }

    // 今の来店 (空いていれば null)
    public TableListResponseVisit? Visit { get; set; }
}

public sealed class TableListResponseVisit
{
    public Guid VisitId { get; set; }

    public int Adults { get; set; }

    public int Children { get; set; }

    public VisitStatus Status { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public DateTimeOffset? LastOrderedAt { get; set; }

    // まだ出していない明細 (取消と食後の品を除く)
    public int UnservedCount { get; set; }

    // 終わっていない呼び出し
    public int OpenCallCount { get; set; }

    public int Version { get; set; }
}
