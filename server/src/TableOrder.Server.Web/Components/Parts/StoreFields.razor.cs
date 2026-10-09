namespace TableOrder.Server.Web.Components.Parts;

using Microsoft.AspNetCore.Components;

// 店舗の基本の入力欄 (足すときは PIN と設定を写す店舗も出す)
public sealed partial class StoreFields
{
    private static readonly TaxRounding[] Roundings = [TaxRounding.Floor, TaxRounding.Round, TaxRounding.Ceiling];

    [Parameter]
    [EditorRequired]
    public StoreForm Form { get; set; } = default!;

    // 設定を写す店舗の候補
    [Parameter]
    public IReadOnlyList<StoreEntity> Stores { get; set; } = [];

    [Parameter]
    public bool IsNew { get; set; }
}
