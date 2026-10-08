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

        Items = items;
        OptionGroups = groups;
        Options = options;
        Rules = rules;
    }
}
