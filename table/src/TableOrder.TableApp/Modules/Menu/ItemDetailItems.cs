namespace TableOrder.TableApp.Modules.Menu;

public sealed record ItemDetailParameter(Guid ItemId, CartLine? Line);

// オプションの組 (ソース、セットなど)。必須の組は選ぶまで入れられない
// 新しく入れるときは既定のオプションを選んでおき (品切れのものは選ばない)、直すときは選んでいたオプションを選んでおく
public sealed class OptionGroupChoice
{
    public string Name { get; }

    public string RuleText { get; }

    public bool IsRequired { get; }

    public int MinSelect { get; }

    public int MaxSelect { get; }

    public IReadOnlyList<OptionChoice> Options { get; }

    // タイルを 2 つずつ並べる行
    public IReadOnlyList<OptionRow> Rows { get; }

    public bool IsSatisfied => Options.Count(static x => x.IsSelected) >= MinSelect;

    public OptionGroupChoice(MenuResponseOptionGroup group, Language language, MenuState menuState, IReadOnlyCollection<Guid> selectedIds, bool useDefault)
    {
        Name = group.Name.Get(language);
        IsRequired = group.MinSelect > 0;
        MinSelect = group.MinSelect;
        MaxSelect = group.MaxSelect;
        RuleText = group.MaxSelect > 1
            ? ViewHelper.Format(AppResources.DetailChooseUpTo, group.MaxSelect)
            : IsRequired ? AppResources.DetailChooseOne : AppResources.DetailOptional;
        Options = group.Options
            .Select(x =>
            {
                var soldOut = menuState.IsSoldOut(x.Id);
                return new OptionChoice(this, x, language, soldOut, useDefault ? x.IsDefault && !soldOut : selectedIds.Contains(x.Id));
            })
            .ToList();
        Rows = Options.Chunk(2).Select(static x => new OptionRow(x)).ToList();
    }
}

// オプションのタイルの行 (折り返しの配置を行に分けて組む)
public sealed class OptionRow
{
    public IReadOnlyList<OptionChoice> Items { get; }

    public OptionRow(IReadOnlyList<OptionChoice> items)
    {
        Items = items;
    }
}

// オプションのタイル (選んだものを主色の枠で示す)。品切れのオプションは選べない (直すときに選んでいたものは外せる)
public sealed partial class OptionChoice : ObservableObject
{
    public OptionGroupChoice Group { get; }

    public Guid Id { get; }

    public string Name { get; }

    public decimal PriceDelta { get; }

    public bool IsSoldOut { get; }

    // 価格の差 (例: +¥100)。品切れは品切れと出す
    public string CaptionText { get; }

    public bool HasCaption => CaptionText.Length > 0;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public OptionChoice(OptionGroupChoice group, MenuResponseOption option, Language language, bool soldOut, bool selected)
    {
        Group = group;
        Id = option.Id;
        Name = option.Name.Get(language);
        PriceDelta = option.PriceDelta;
        IsSoldOut = soldOut;
        CaptionText = soldOut ? AppResources.SoldOut : ViewHelper.PriceDelta(option.PriceDelta);
        IsSelected = selected;
    }
}

// 出す時機 (すぐに / 食後に)
public sealed partial class TimingChoice : ObservableObject
{
    public OrderTiming Timing { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public TimingChoice(OrderTiming timing, string name, bool selected)
    {
        Timing = timing;
        Name = name;
        IsSelected = selected;
    }
}
