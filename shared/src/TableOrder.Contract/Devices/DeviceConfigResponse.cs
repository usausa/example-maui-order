namespace TableOrder.Contract.Devices;

// 端末の設定 (起動のときと店舗の設定が変わったときに読む)
public sealed class DeviceConfigResponse
{
    public LocalizedText StoreName { get; set; } = default!;

    // 画面で選べる言語 ("ja"、"en")
    public IReadOnlyList<string> Languages { get; set; } = default!;

    public DeviceConfigResponseOrderRules OrderRules { get; set; } = default!;

    // テーブルで使える支払方法。空ならテーブルでは会計せず、レジに案内する
    public IReadOnlyList<PaymentMethod> PaymentMethods { get; set; } = default!;

    // 呼び出しの用件 (表示順)
    public IReadOnlyList<DeviceConfigResponseCallReason> CallReasons { get; set; } = default!;

    // 電子レシートを出すか
    public bool ElectronicReceipt { get; set; }

    public TaxRounding TaxRounding { get; set; }

    // 端末と置き場所 (サーバが端末の記録から返す。モックは返さず、端末の設定のテーブル番号を使う)
    public DeviceConfigResponseDevice? Device { get; set; }
}

public sealed class DeviceConfigResponseDevice
{
    public Guid Id { get; set; }

    public DeviceKind Kind { get; set; }

    public string Name { get; set; } = default!;

    // テーブル端末の置き場所
    public Guid? TableId { get; set; }

    public string? TableName { get; set; }

    // キッチン端末が受け持つ持ち場
    public IReadOnlyList<Guid> StationIds { get; set; } = default!;
}

public sealed class DeviceConfigResponseOrderRules
{
    // 1 明細の数量の上限
    public int MaxQuantityPerLine { get; set; }

    // 1 回の注文の明細の上限
    public int MaxLinesPerOrder { get; set; }

    // テーブル端末から人数を入れて来店を開けるか (ホール端末・受付機のない店)
    public bool SelfStart { get; set; }
}

public sealed class DeviceConfigResponseCallReason
{
    public string Code { get; set; } = default!;

    public LocalizedText Name { get; set; } = default!;

    public int SortOrder { get; set; }
}
