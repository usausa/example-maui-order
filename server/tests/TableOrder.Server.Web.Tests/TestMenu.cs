namespace TableOrder.Server.Web;

using TableOrder.Contract.Menu;
using TableOrder.Contract.Orders;

// テストの注文。単価はサーバのメニューと Pricing で求める (メニューの価格をテストに書かない)
public sealed class TestMenu
{
    // チーズインハンバーグ (キッチン。ソースを 1 つ選ぶ)
    public static readonly Guid Hamburg = Guid.Parse("000003e9-0000-0000-0000-000000000000");

    public static readonly Guid Demiglace = Guid.Parse("00001f4b-0000-0000-0000-000000000000");

    public static readonly Guid CheeseSauce = Guid.Parse("00001f4d-0000-0000-0000-000000000000");

    // サーロインステーキ (お一人様 1 点まで。焼き加減を 1 つ選ぶ)
    public static readonly Guid Sirloin = Guid.Parse("000003ec-0000-0000-0000-000000000000");

    public static readonly Guid Medium = Guid.Parse("00001f56-0000-0000-0000-000000000000");

    // シーザーサラダ (キッチン。オプションなし)
    public static readonly Guid Salad = Guid.Parse("00000bb9-0000-0000-0000-000000000000");

    // いちごパフェ (デザート。食後と選べる)
    public static readonly Guid Parfait = Guid.Parse("00000fa1-0000-0000-0000-000000000000");

    // ドリンクバー (お客様がとる)
    public static readonly Guid DrinkBar = Guid.Parse("00001771-0000-0000-0000-000000000000");

    // 生ビール (お酒。来店で 1 回、年齢を確かめる)
    public static readonly Guid Beer = Guid.Parse("00001772-0000-0000-0000-000000000000");

    public static readonly Guid AlcoholRuleId = Guid.Parse("000003e0-0000-0000-0000-000000000000");

    // 持ち場 (キッチン、デザート、ドリンク)
    public static readonly Guid KitchenStation = Guid.Parse("0000005b-0000-0000-0000-000000000000");

    public static readonly Guid DessertStation = Guid.Parse("0000005c-0000-0000-0000-000000000000");

    private readonly MenuResponse menu;

    private TestMenu(MenuResponse menu)
    {
        this.menu = menu;
    }

    public static async Task<TestMenu> LoadAsync(TestDevice device) =>
        new(await device.GetAsync<MenuResponse>("/api/v1/menu"));

    // 端末の窓口で読んだメニュー
    public static TestMenu From(MenuResponse menu) => new(menu);

    public OrderCreateRequestLine Line(Guid itemId, int quantity = 1, OrderTiming timing = OrderTiming.Now, params Guid[] optionIds)
    {
        var item = menu.Items.Single(x => x.Id == itemId);
        var deltas = optionIds.Select(id => menu.OptionGroups.SelectMany(static x => x.Options).Single(x => x.Id == id).PriceDelta);
        return new OrderCreateRequestLine
        {
            Id = Guid.CreateVersion7(),
            ItemId = itemId,
            OptionIds = optionIds,
            Quantity = quantity,
            UnitPrice = Pricing.UnitPrice(item.Price, deltas),
            Timing = timing
        };
    }

    public OrderCreateRequest Order(params OrderCreateRequestLine[] lines) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            MenuVersion = menu.MenuVersion,
            Lines = lines
        };
}
