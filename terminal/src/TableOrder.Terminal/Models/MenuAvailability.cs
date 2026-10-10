namespace TableOrder.Terminal.Models;

using TableOrder.Contract.Menu;

// 出せる条件のルール (時間帯、子どもがいる) を満たさないタグと理由。店舗の現地時刻と来店の子どもの人数で求め、品とカテゴリの出し分けに使う
// 端末は時間帯の終わりの時刻で隠す (終わりの猶予はサーバだけが見る)。最後に決めるのはサーバ
public sealed class MenuAvailability
{
    // 条件のない (すべて出せる) とき
    public static MenuAvailability All { get; } = new([]);

    private readonly Dictionary<string, UnavailableReason> tags;

    private MenuAvailability(Dictionary<string, UnavailableReason> tags)
    {
        this.tags = tags;
    }

    // ルールごとに満たすかを確かめ、満たさないルールのタグを理由と一緒に持つ (同じタグのルールは、先に満たさなかったものの理由)
    public static MenuAvailability Evaluate(MenuResponse menu, TimeOnly now, int children)
    {
        var periods = PeriodsOf(menu);
        var tags = new Dictionary<string, UnavailableReason>(StringComparer.Ordinal);
        foreach (var rule in menu.Rules.Where(static x => x.Kind == MenuRuleKind.Availability))
        {
            var reason = TagRules.CheckAvailability(Resolve(rule, periods), rule.RequiresChildren == true, now, children);
            if (reason != UnavailableReason.None)
            {
                tags.TryAdd(rule.TargetTag, reason);
            }
        }

        return tags.Count > 0 ? new MenuAvailability(tags) : All;
    }

    // 商品・選んだオプション・カテゴリのタグのうち、満たさないものの理由 (どれも満たせば None)
    public UnavailableReason ReasonOf(IEnumerable<string> targetTags)
    {
        foreach (var tag in targetTags)
        {
            if (tags.TryGetValue(tag, out var reason))
            {
                return reason;
            }
        }

        return UnavailableReason.None;
    }

    public bool IsAvailable(IEnumerable<string> targetTags) => ReasonOf(targetTags) == UnavailableReason.None;

    // 隠すものが同じか (時刻の見直しで並べ直すかを決める)
    public bool IsSame(MenuAvailability other) =>
        (tags.Count == other.tags.Count) && tags.All(x => other.tags.TryGetValue(x.Key, out var reason) && (reason == x.Value));

    // 今の時刻が中にある、出せる条件のルールが指す時間帯のうち、いちばん早く終わるものと終わりまでの残り (終わる前の知らせ)
    public static (MenuResponseDaypart Daypart, TimeSpan Remaining)? FindEnding(MenuResponse menu, TimeOnly now)
    {
        var used = menu.Rules
            .Where(static x => x.Kind == MenuRuleKind.Availability)
            .SelectMany(static x => x.Dayparts ?? [])
            .ToHashSet(StringComparer.Ordinal);
        (MenuResponseDaypart Daypart, TimeSpan Remaining)? ending = null;
        foreach (var daypart in menu.Dayparts.Where(x => used.Contains(x.Code)))
        {
            if (StoreHours.TryParse(daypart.Start, out var start) && StoreHours.TryParse(daypart.End, out var end) &&
                (StoreHours.UntilPeriodEnd(now, start, end) is { } remaining) &&
                ((ending is null) || (remaining < ending.Value.Remaining)))
            {
                ending = (daypart, remaining);
            }
        }

        return ending;
    }

    private static Dictionary<string, (TimeOnly Start, TimeOnly End)> PeriodsOf(MenuResponse menu)
    {
        var periods = new Dictionary<string, (TimeOnly, TimeOnly)>(StringComparer.Ordinal);
        foreach (var daypart in menu.Dayparts)
        {
            if (StoreHours.TryParse(daypart.Start, out var start) && StoreHours.TryParse(daypart.End, out var end))
            {
                periods.TryAdd(daypart.Code, (start, end));
            }
        }

        return periods;
    }

    // ない時間帯は空の時間帯 (どの時刻も外) にして、指したものを出さない (サーバと同じ)
    private static IEnumerable<(TimeOnly Start, TimeOnly End)> Resolve(MenuResponseRule rule, Dictionary<string, (TimeOnly Start, TimeOnly End)> periods) =>
        (rule.Dayparts ?? []).Select(x => periods.TryGetValue(x, out var period) ? period : (TimeOnly.MinValue, TimeOnly.MinValue));
}
