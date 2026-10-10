namespace TableOrder.Contract.Menu;

// 店舗で出すメニュー全体。特定の商品 (ドリンクバー、お酒、キッズ) と時間帯で出す品はタグとルールで表す
public sealed class MenuResponse
{
    public string MenuVersion { get; set; } = default!;

    public IReadOnlyList<MenuResponseCategory> Categories { get; set; } = default!;

    public IReadOnlyList<MenuResponseItem> Items { get; set; } = default!;

    public IReadOnlyList<MenuResponseOptionGroup> OptionGroups { get; set; } = default!;

    public IReadOnlyList<MenuResponseTag> Tags { get; set; } = default!;

    public IReadOnlyList<MenuResponseRule> Rules { get; set; } = default!;

    public IReadOnlyList<MenuResponseAllergen> Allergens { get; set; } = default!;

    public IReadOnlyList<MenuResponseStation> Stations { get; set; } = default!;

    // 時間帯 (出せる条件のルールがコードで指す)。時間帯を持たない公開の内容では空にする
    public IReadOnlyList<MenuResponseDaypart> Dayparts { get; set; } = [];
}

public sealed class MenuResponseCategory
{
    public Guid Id { get; set; }

    public LocalizedText Name { get; set; } = default!;

    public int SortOrder { get; set; }

    public IReadOnlyList<string> Tags { get; set; } = default!;

    // 表示順。1 つの商品が複数のカテゴリ (おすすめと本来のカテゴリ) に入ってよい
    public IReadOnlyList<Guid> ItemIds { get; set; } = default!;
}

public sealed class MenuResponseItem
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public LocalizedText Name { get; set; } = default!;

    public LocalizedText? Description { get; set; }

    // 税込
    public decimal Price { get; set; }

    public decimal TaxRate { get; set; }

    public string? ImageName { get; set; }

    public IReadOnlyList<ItemBadge> Badges { get; set; } = default!;

    public int SpiceLevel { get; set; }

    public IReadOnlyList<string> AllergenCodes { get; set; } = default!;

    public int? Calories { get; set; }

    public IReadOnlyList<string> Tags { get; set; } = default!;

    // 作る持ち場 (null は作らない品)
    public Guid? StationId { get; set; }

    public ServedBy ServedBy { get; set; }

    public IReadOnlyList<Guid> OptionGroupIds { get; set; } = default!;

    public int? MaxQuantity { get; set; }

    public OrderTiming DefaultTiming { get; set; }

    // お客様が出す時機 (すぐに / 食後) を選べるか
    public bool TimingSelectable { get; set; }
}

public sealed class MenuResponseOptionGroup
{
    public Guid Id { get; set; }

    public LocalizedText Name { get; set; } = default!;

    public int MinSelect { get; set; }

    public int MaxSelect { get; set; }

    public IReadOnlyList<MenuResponseOption> Options { get; set; } = default!;
}

public sealed class MenuResponseOption
{
    public Guid Id { get; set; }

    public LocalizedText Name { get; set; } = default!;

    // 税込の差額
    public decimal PriceDelta { get; set; }

    public bool IsDefault { get; set; }

    public IReadOnlyList<string> Tags { get; set; } = default!;

    public IReadOnlyList<string> AllergenCodes { get; set; } = default!;
}

public sealed class MenuResponseTag
{
    public string Code { get; set; } = default!;

    public LocalizedText Name { get; set; } = default!;
}

// 種類 (Kind) ごとに使う項目が違う。Suggestion は Basis と SuggestItemIds、Confirmation は Scope と Message、Limit は Scope と Max、
// Availability は Dayparts と RequiresChildren
public sealed class MenuResponseRule
{
    public Guid Id { get; set; }

    public MenuRuleKind Kind { get; set; }

    public string TargetTag { get; set; } = default!;

    public GuestBasis? Basis { get; set; }

    public IReadOnlyList<Guid>? SuggestItemIds { get; set; }

    public RuleScope? Scope { get; set; }

    public int? Max { get; set; }

    public LocalizedText? Message { get; set; }

    // 出せる時間帯のコード (どれかの中なら出せる)
    public IReadOnlyList<string>? Dayparts { get; set; }

    // 子どもが 1 人以上の来店だけに出す
    public bool? RequiresChildren { get; set; }
}

public sealed class MenuResponseAllergen
{
    public string Code { get; set; } = default!;

    public LocalizedText Name { get; set; } = default!;

    // 特定原材料 (表示が必須の 8 品目)
    public bool IsMandatory { get; set; }
}

// 時間帯。始まりと終わりは店舗の現地時刻の HH:mm で、終わりが始まりより前なら日をまたぐ
public sealed class MenuResponseDaypart
{
    public string Code { get; set; } = default!;

    public LocalizedText Name { get; set; } = default!;

    public string Start { get; set; } = default!;

    public string End { get; set; } = default!;
}

public sealed class MenuResponseStation
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public int SortOrder { get; set; }
}
