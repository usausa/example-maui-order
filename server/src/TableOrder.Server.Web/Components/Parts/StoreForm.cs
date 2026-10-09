namespace TableOrder.Server.Web.Components.Parts;

// 店舗の画面のフォームの値 (店舗の基本。足すときは PIN と写す店舗も持つ)
public sealed class StoreForm
{
    public string Code { get; set; } = string.Empty;

    public string NameJa { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "Asia/Tokyo";

    public string OpenTime { get; set; } = "10:00";

    public string CloseTime { get; set; } = "23:00";

    public string LastOrderTime { get; set; } = "22:30";

    public TaxRounding TaxRounding { get; set; } = TaxRounding.Floor;

    public int MaxQuantityPerLine { get; set; } = 9;

    public int MaxLinesPerOrder { get; set; } = 20;

    public string StaffPin { get; set; } = string.Empty;

    // 設定 (言語、支払方法、機能、呼び出しの用件、メニュー) を写す店舗 (null は写さない)
    public Guid? SourceStoreId { get; set; }

    public static StoreForm From(StoreEntity store) =>
        new()
        {
            Code = store.Code,
            NameJa = store.Name.Ja,
            NameEn = store.Name.En ?? string.Empty,
            TimeZone = store.TimeZone,
            OpenTime = store.OpenTime,
            CloseTime = store.CloseTime,
            LastOrderTime = store.LastOrderTime ?? string.Empty,
            TaxRounding = store.TaxRounding,
            MaxQuantityPerLine = store.MaxQuantityPerLine,
            MaxLinesPerOrder = store.MaxLinesPerOrder
        };

    public StoreBasics ToBasics() =>
        new(Code, new LocalizedText { Ja = NameJa, En = NameEn }, TimeZone.Trim(), OpenTime.Trim(), CloseTime.Trim(), LastOrderTime, TaxRounding, MaxQuantityPerLine, MaxLinesPerOrder);
}
