namespace TableOrder.Server.Core.Models;

using TableOrder.Contract.Menu;

// 業務の確かめ (注文、品切れ、確認のルール) に使うメニュー。公開の内容を Id で引けるようにしたもの
public sealed class MenuCatalog
{
    public Guid PublicationId { get; }

    public MenuResponse Menu { get; }

    public IReadOnlyDictionary<Guid, MenuResponseItem> Items { get; }

    public IReadOnlyDictionary<Guid, MenuResponseOptionGroup> OptionGroups { get; }

    // オプションと、オプションの入っている組
    public IReadOnlyDictionary<Guid, (MenuResponseOptionGroup Group, MenuResponseOption Option)> Options { get; }

    public IReadOnlyDictionary<Guid, MenuResponseRule> Rules { get; }

    // 出せる条件のルール (Availability)
    public IReadOnlyList<MenuResponseRule> AvailabilityRules { get; }

    // 時間帯の始まりと終わり (コードで引く。時刻を読めない時間帯は入れない)
    public IReadOnlyDictionary<string, (TimeOnly Start, TimeOnly End)> Periods { get; }

    public MenuCatalog(Guid publicationId, MenuResponse menu)
    {
        PublicationId = publicationId;
        Menu = menu;

        // 公開の内容は本部が作るので、Id が重なっていても止めずに先のものを使う
        var items = new Dictionary<Guid, MenuResponseItem>();
        foreach (var item in menu.Items)
        {
            items.TryAdd(item.Id, item);
        }

        var groups = new Dictionary<Guid, MenuResponseOptionGroup>();
        var options = new Dictionary<Guid, (MenuResponseOptionGroup, MenuResponseOption)>();
        foreach (var group in menu.OptionGroups)
        {
            groups.TryAdd(group.Id, group);
            foreach (var option in group.Options)
            {
                options.TryAdd(option.Id, (group, option));
            }
        }

        var rules = new Dictionary<Guid, MenuResponseRule>();
        foreach (var rule in menu.Rules)
        {
            rules.TryAdd(rule.Id, rule);
        }

        var periods = new Dictionary<string, (TimeOnly, TimeOnly)>(StringComparer.Ordinal);
        foreach (var daypart in menu.Dayparts)
        {
            if (StoreHours.TryParse(daypart.Start, out var start) && StoreHours.TryParse(daypart.End, out var end))
            {
                periods.TryAdd(daypart.Code, (start, end));
            }
        }

        Items = items;
        OptionGroups = groups;
        Options = options;
        Rules = rules;
        AvailabilityRules = menu.Rules.Where(static x => x.Kind == MenuRuleKind.Availability).ToList();
        Periods = periods;
    }

    // ルールの時間帯。ない時間帯は空の時間帯 (どの時刻も外) にして、指したものを出さない (誤った公開の内容で、時間帯の品をいつでも出さない)
    public IEnumerable<(TimeOnly Start, TimeOnly End)> PeriodsOf(MenuResponseRule rule) =>
        (rule.Dayparts ?? []).Select(x => Periods.TryGetValue(x, out var period) ? period : (TimeOnly.MinValue, TimeOnly.MinValue));
}
