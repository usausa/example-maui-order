namespace TableOrder.Contract.Devices;

// 端末の設定 (起動のときに読む。チェーンと店舗の設定の版が替わったら、起動からやり直して読み直す)
public sealed class DeviceConfigResponse
{
    public LocalizedText StoreName { get; set; } = default!;

    // 画面で選べる言語 ("ja"、"en")。1 つなら言語のボタンを出さない
    public IReadOnlyList<string> Languages { get; set; } = default!;

    public DeviceConfigResponseOrderRules OrderRules { get; set; } = default!;

    // テーブルで使える支払方法。空ならテーブルでは会計せず、レジに案内する
    public IReadOnlyList<PaymentMethod> PaymentMethods { get; set; } = default!;

    // 呼び出しの用件 (表示順)。空なら店員呼出を出さない
    public IReadOnlyList<DeviceConfigResponseCallReason> CallReasons { get; set; } = default!;

    // 電子レシートを出すか
    public bool ElectronicReceipt { get; set; }

    public TaxRounding TaxRounding { get; set; }

    // 端末と置き場所 (サーバが端末の記録から返す)
    public DeviceConfigResponseDevice? Device { get; set; }

    // チェーンの設定 (テナントの名前・ロゴ・色)
    public DeviceConfigResponseBrand Brand { get; set; } = default!;

    // 機能の有無 (店舗の設定。ない項目は既定の値)
    public DeviceConfigResponseFeatures Features { get; set; } = default!;

    // スタッフの PIN のハッシュ (端末は入れた PIN を同じ計算で確かめ、平文を持たない)。PIN を使わないキッチン端末は null
    public DeviceConfigResponseStaffPin? StaffPin { get; set; }

    // チェーンと店舗の設定の版 (store.updated の店舗の版と違えば、待受のときに起動からやり直す)
    public int SettingsVersion { get; set; }
}

public sealed class DeviceConfigResponseBrand
{
    public LocalizedText Name { get; set; } = default!;

    // 正方形のロゴの画像の名前 (地の色を含む)。なければ印に名前の頭の文字を出す
    public string? LogoImageName { get; set; }

    // 替える色の役割と色。ない役割は端末の既定のまま
    public IReadOnlyList<DeviceConfigResponseThemeColor> Theme { get; set; } = default!;
}

// 色の役割 (ThemeRoles の名前) と色 (#RRGGBB か #AARRGGBB)。テナントの設定に持つ JSON の形も兼ねる
public sealed class DeviceConfigResponseThemeColor
{
    public string Role { get; set; } = default!;

    public string Color { get; set; } = default!;
}

// 店舗の設定に持つ JSON の形も兼ねる (ない項目は既定の値)
public sealed class DeviceConfigResponseFeatures
{
    // お会計で「レジで払う」を出す
    public bool RegisterCheckout { get; set; } = true;

    // 割り勘で 1 人分ずつ払える
    public bool SplitPayment { get; set; } = true;

    // ラストオーダーの何分前から知らせるか (0 は知らせない)
    public int LastOrderNoticeMinutes { get; set; } = 30;

    // お礼の画面から待受に戻るまでの秒数
    public int FinishSeconds { get; set; } = 30;

    // 来店の開き方 (テーブル端末は待受の文言と、人数を入れて始めるかを替える)
    public VisitOpening VisitOpening { get; set; } = VisitOpening.Hall;

    // キッチン端末で、チケットができてから何分で注意の色にするか (0 は色を替えない)
    public int KitchenAlertMinutes { get; set; } = 15;
}

// PBKDF2-HMAC-SHA256 の回数と、Base64 の塩とハッシュ (店舗の設定に持つ JSON の形も兼ねる)
public sealed class DeviceConfigResponseStaffPin
{
    public int Iterations { get; set; }

    public string Salt { get; set; } = default!;

    public string Hash { get; set; } = default!;
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
}

public sealed class DeviceConfigResponseCallReason
{
    public string Code { get; set; } = default!;

    public LocalizedText Name { get; set; } = default!;

    public int SortOrder { get; set; }
}
