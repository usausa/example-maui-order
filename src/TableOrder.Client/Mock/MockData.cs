namespace TableOrder.Client.Mock;

// モックの応答のデータ (ファミリーレストランのメニュー)。ドリンクバー・お酒・キッズはタグとルールで表す
internal static class MockData
{
    // Tag

    private const string DrinkBarTag = "drink-bar";
    private const string AlcoholTag = "alcohol";
    private const string KidsTag = "kids";
    private const string DessertTag = "dessert";
    private const string OnePerGuestTag = "one-per-guest";

    // Station

    private static readonly Guid KitchenStation = Id(91);
    private static readonly Guid DessertStation = Id(92);
    private static readonly Guid DrinkStation = Id(93);

    // Option group

    private static readonly Guid SauceGroup = Id(801);
    private static readonly Guid DonenessGroup = Id(802);
    private static readonly Guid SetGroup = Id(803);
    private static readonly Guid DrinkGroup = Id(804);
    private static readonly Guid SizeGroup = Id(805);

    // Item

    private static readonly Guid CheeseHamburg = Id(1001);
    private static readonly Guid EggHamburg = Id(1002);
    private static readonly Guid MixedGrill = Id(1003);
    private static readonly Guid Sirloin = Id(1004);
    private static readonly Guid Carbonara = Id(2001);
    private static readonly Guid MeatSauce = Id(2002);
    private static readonly Guid Doria = Id(2003);
    private static readonly Guid Caesar = Id(3001);
    private static readonly Guid Fries = Id(3002);
    private static readonly Guid CornSoup = Id(3003);
    private static readonly Guid Margherita = Id(3004);
    private static readonly Guid Parfait = Id(4001);
    private static readonly Guid Pancake = Id(4002);
    private static readonly Guid IceCream = Id(4003);
    private static readonly Guid KidsPlate = Id(5001);
    private static readonly Guid KidsDrinkBar = Id(5002);
    private static readonly Guid DrinkBar = Id(6001);
    private static readonly Guid Beer = Id(6002);
    private static readonly Guid Wine = Id(6003);

    // 商品の説明
    private static readonly Dictionary<Guid, LocalizedText> Descriptions = new()
    {
        [CheeseHamburg] = Text("粗挽きの合挽き肉でとろけるチーズを包み、鉄板で焼き上げました。", "Coarse-ground hamburg steak with melting cheese inside, grilled on a hot plate."),
        [EggHamburg] = Text("定番のハンバーグに半熟の目玉焼きをのせました。", "Our classic hamburg steak topped with a soft fried egg."),
        [MixedGrill] = Text("ハンバーグ、グリルチキン、ソーセージを一皿で楽しめます。", "Hamburg steak, grilled chicken and sausages on one plate."),
        [Sirloin] = Text("やわらかなサーロインを鉄板で焼き上げます。数量限定です。", "Tender sirloin grilled on a hot plate. Limited quantity."),
        [Carbonara] = Text("卵とチーズのコクを生かした濃厚なソースです。", "A rich sauce of egg and cheese."),
        [MeatSauce] = Text("牛肉と香味野菜をじっくり煮込んだミートソースです。", "Beef and vegetables slow-cooked into a hearty sauce."),
        [Doria] = Text("ミートソースとホワイトソースにチーズをのせて焼きました。", "Rice baked with meat sauce, white sauce and cheese."),
        [Caesar] = Text("ロメインレタスにベーコン、クルトン、パルメザンチーズを合わせました。", "Romaine lettuce with bacon, croutons and parmesan."),
        [Fries] = Text("皮付きのポテトをカリッと揚げました。", "Crispy skin-on fries."),
        [CornSoup] = Text("北海道産のとうもろこしを使ったポタージュです。", "Creamy soup made with Hokkaido corn."),
        [Margherita] = Text("トマトソースにモッツァレラとバジルをのせて焼きました。", "Tomato sauce, mozzarella and basil."),
        [Parfait] = Text("いちごとバニラアイス、ホイップクリームを重ねました。", "Strawberries layered with vanilla ice cream and whipped cream."),
        [Pancake] = Text("ふんわり焼いたパンケーキにメープルシロップを添えました。", "Fluffy pancakes served with maple syrup."),
        [IceCream] = Text("なめらかなバニラアイスです。", "Smooth vanilla ice cream."),
        [KidsPlate] = Text("ミニハンバーグ、ナポリタン、ポテト、ゼリーのプレートです。", "Mini hamburg steak, spaghetti, fries and jelly."),
        [KidsDrinkBar] = Text("小学生以下のお子様のドリンクバーです。", "Drink bar for children of elementary school age and under."),
        [DrinkBar] = Text("ソフトドリンク、コーヒー、紅茶をご自由にお楽しみください。", "Unlimited soft drinks, coffee and tea."),
        [Beer] = Text("よく冷えた生ビールです。", "Ice-cold draft beer."),
        [Wine] = Text("料理に合わせやすいミディアムボディの赤ワインです。", "A medium-bodied red wine that pairs well with our dishes.")
    };

    //--------------------------------------------------------------------------------
    // Config
    //--------------------------------------------------------------------------------

    public static DeviceConfigResponse CreateConfig() =>
        new()
        {
            StoreName = Text("駅前店", "Ekimae"),
            Languages = ["ja", "en"],
            OrderRules = new DeviceConfigResponseOrderRules
            {
                MaxQuantityPerLine = 9,
                MaxLinesPerOrder = 20,
                SelfStart = true
            },
            PaymentMethods = [PaymentMethod.QrCode, PaymentMethod.CreditCard],
            CallReasons =
            [
                CallReason("Staff", "店員を呼ぶ", "Call staff", 1),
                CallReason("Water", "お水", "Water", 2),
                CallReason("Plates", "取り皿", "Small plates", 3),
                CallReason("Cutlery", "スプーン・フォーク", "Spoon & fork", 4),
                CallReason("KidsTableware", "子ども用の食器", "Kids tableware", 5),
                CallReason("Clear", "お皿を下げる", "Clear the table", 6),
                CallReason("Payment", "お会計の相談", "Help with payment", 7)
            ],
            ElectronicReceipt = true,
            TaxRounding = TaxRounding.Floor
        };

    //--------------------------------------------------------------------------------
    // Stock
    //--------------------------------------------------------------------------------

    // パンケーキは売り切れ、サーロインステーキは残り 3 点
    public static IReadOnlyList<StockResponseItem> CreateStock() =>
    [
        new() { TargetId = Pancake, TargetKind = StockTargetKind.Item, Status = StockStatus.SoldOut, UpdatedAt = DateTimeOffset.UtcNow },
        new() { TargetId = Sirloin, TargetKind = StockTargetKind.Item, Status = StockStatus.Limited, Remaining = 3, UpdatedAt = DateTimeOffset.UtcNow }
    ];

    //--------------------------------------------------------------------------------
    // Menu
    //--------------------------------------------------------------------------------

    public static MenuResponse CreateMenu() =>
        new()
        {
            MenuVersion = "mock-1",
            Categories =
            [
                Category(101, "おすすめ", "Recommended", [], [MixedGrill, CheeseHamburg, Parfait, Margherita, Carbonara, Sirloin, EggHamburg, Doria, Caesar, Pancake, CornSoup, DrinkBar]),
                Category(102, "ハンバーグ・ステーキ", "Hamburg & Steak", [], [CheeseHamburg, EggHamburg, MixedGrill, Sirloin]),
                Category(103, "パスタ・ドリア", "Pasta & Doria", [], [Carbonara, MeatSauce, Doria]),
                Category(104, "サラダ・サイド", "Salad & Sides", [], [Caesar, Fries, CornSoup, Margherita]),
                Category(105, "デザート", "Desserts", [DessertTag], [Parfait, Pancake, IceCream]),
                Category(106, "キッズ", "Kids", [KidsTag], [KidsPlate, KidsDrinkBar]),
                Category(107, "ドリンク", "Drinks", [], [DrinkBar, Beer, Wine])
            ],
            Items =
            [
                Item(CheeseHamburg, "1001", "チーズインハンバーグ", "Cheese-filled Hamburg Steak", 999, "food_hamburg.png", [ItemBadge.Popular], KitchenStation, [], [SauceGroup, SetGroup, DrinkGroup], 820, ["wheat", "egg", "milk"]),
                Item(EggHamburg, "1002", "目玉焼きハンバーグ", "Hamburg Steak with Fried Egg", 949, "food_hamburg_egg.png", [], KitchenStation, [], [SauceGroup, SetGroup, DrinkGroup], 860, ["wheat", "egg", "milk"]),
                Item(MixedGrill, "1003", "ミックスグリル", "Mixed Grill", 1299, "food_grill.png", [ItemBadge.Recommended], KitchenStation, [], [SetGroup, DrinkGroup], 1050, ["wheat", "egg", "milk"]),
                Item(Sirloin, "1004", "サーロインステーキ", "Sirloin Steak", 1899, "food_steak.png", [ItemBadge.Limited], KitchenStation, [OnePerGuestTag], [DonenessGroup, SetGroup, DrinkGroup], 680, []),
                Item(Carbonara, "2001", "カルボナーラ", "Carbonara", 799, "food_carbonara.png", [], KitchenStation, [], [DrinkGroup], 780, ["wheat", "egg", "milk"]),
                Item(MeatSauce, "2002", "ミートソース", "Spaghetti Bolognese", 699, "food_meatsauce.png", [], KitchenStation, [], [DrinkGroup], 690, ["wheat", "milk"]),
                Item(Doria, "2003", "チーズドリア", "Cheese Doria", 599, "food_doria.png", [ItemBadge.New], KitchenStation, [], [DrinkGroup], 620, ["wheat", "milk"]),
                Item(Caesar, "3001", "シーザーサラダ", "Caesar Salad", 499, "food_salad.png", [], KitchenStation, [], [], 280, ["wheat", "egg", "milk"]),
                Item(Fries, "3002", "フライドポテト", "French Fries", 349, "food_fries.png", [], KitchenStation, [], [SizeGroup], 420, []),
                Item(CornSoup, "3003", "コーンスープ", "Corn Soup", 249, "food_soup.png", [], KitchenStation, [], [], 150, ["milk"]),
                Item(Margherita, "3004", "マルゲリータ", "Margherita Pizza", 599, "food_pizza.png", [ItemBadge.New], KitchenStation, [], [], 640, ["wheat", "milk"]),
                Item(Parfait, "4001", "いちごパフェ", "Strawberry Parfait", 699, "food_parfait.png", [ItemBadge.Popular], DessertStation, [DessertTag], [], 520, ["wheat", "egg", "milk"], OrderTiming.AfterMeal),
                Item(Pancake, "4002", "パンケーキ", "Pancakes", 599, "food_pancake.png", [], DessertStation, [DessertTag], [], 610, ["wheat", "egg", "milk"], OrderTiming.AfterMeal),
                Item(IceCream, "4003", "バニラアイス", "Vanilla Ice Cream", 299, "food_icecream.png", [], DessertStation, [DessertTag], [], 210, ["egg", "milk"], OrderTiming.AfterMeal),
                Item(KidsPlate, "5001", "キッズプレート", "Kids Plate", 599, "food_kidsplate.png", [], KitchenStation, [KidsTag], [], 560, ["wheat", "egg", "milk"]),
                Item(KidsDrinkBar, "5002", "キッズドリンクバー", "Kids Drink Bar", 199, "food_drinkbar.png", [], null, [DrinkBarTag, KidsTag], [], null, []),
                Item(DrinkBar, "6001", "ドリンクバー", "Drink Bar", 459, "food_drinkbar.png", [], null, [DrinkBarTag], [], null, []),
                Item(Beer, "6002", "生ビール", "Draft Beer", 499, "food_beer.png", [], DrinkStation, [AlcoholTag], [], 200, ["wheat"]),
                Item(Wine, "6003", "グラスワイン (赤)", "Glass of Red Wine", 399, "food_wine.png", [], DrinkStation, [AlcoholTag], [], 110, [])
            ],
            OptionGroups =
            [
                Group(SauceGroup, "ソース", "Sauce", 1, 1, [Option(8011, "デミグラス", "Demi-glace", 0, true), Option(8012, "和風おろし", "Japanese grated radish", 0), Option(8013, "チーズ", "Cheese", 100)]),
                Group(DonenessGroup, "焼き加減", "Doneness", 1, 1, [Option(8021, "レア", "Rare", 0), Option(8022, "ミディアム", "Medium", 0, true), Option(8023, "ウェルダン", "Well-done", 0)]),
                Group(SetGroup, "セット", "Set", 0, 1, [Option(8031, "ライス・スープセット", "Rice & Soup", 330), Option(8032, "パン・スープセット", "Bread & Soup", 330)]),
                Group(DrinkGroup, "ドリンク", "Drink", 0, 1, [Option(8041, "セットドリンクバー", "Drink Bar (set)", 299, tags: [DrinkBarTag])]),
                Group(SizeGroup, "サイズ", "Size", 1, 1, [Option(8051, "レギュラー", "Regular", 0, true), Option(8052, "ラージ", "Large", 150)])
            ],
            Tags =
            [
                new() { Code = DrinkBarTag, Name = Text("ドリンクバー", "Drink bar") },
                new() { Code = AlcoholTag, Name = Text("お酒", "Alcohol") },
                new() { Code = KidsTag, Name = Text("キッズ", "Kids") },
                new() { Code = DessertTag, Name = Text("デザート", "Dessert") },
                new() { Code = OnePerGuestTag, Name = Text("お一人様 1 点まで", "One per guest") }
            ],
            Rules =
            [
                new()
                {
                    Id = Id(991),
                    Kind = MenuRuleKind.Suggestion,
                    TargetTag = DrinkBarTag,
                    Basis = GuestBasis.Guests,
                    SuggestItemIds = [DrinkBar, KidsDrinkBar],
                    Message = Text("ドリンクバーを人数分にしますか？", "Would you like drink bar for everyone?")
                },
                new()
                {
                    Id = Id(992),
                    Kind = MenuRuleKind.Confirmation,
                    TargetTag = AlcoholTag,
                    Scope = RuleScope.Visit,
                    Message = Text("20 歳以上で、お車を運転されない方のご注文ですか？", "Are you 20 or older and not driving?")
                },
                new()
                {
                    Id = Id(993),
                    Kind = MenuRuleKind.Limit,
                    TargetTag = OnePerGuestTag,
                    Scope = RuleScope.Guest,
                    Max = 1
                }
            ],
            Allergens =
            [
                Allergen("shrimp", "えび", "Shrimp"),
                Allergen("crab", "かに", "Crab"),
                Allergen("walnut", "くるみ", "Walnut"),
                Allergen("wheat", "小麦", "Wheat"),
                Allergen("buckwheat", "そば", "Buckwheat"),
                Allergen("egg", "卵", "Egg"),
                Allergen("milk", "乳", "Milk"),
                Allergen("peanut", "落花生", "Peanut")
            ],
            Stations =
            [
                new() { Id = KitchenStation, Name = "キッチン", SortOrder = 1 },
                new() { Id = DessertStation, Name = "デザート", SortOrder = 2 },
                new() { Id = DrinkStation, Name = "ドリンク", SortOrder = 3 }
            ]
        };

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static Guid Id(int value) => new(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static LocalizedText Text(string ja, string en) => new() { Ja = ja, En = en };

    private static DeviceConfigResponseCallReason CallReason(string code, string ja, string en, int sortOrder) =>
        new()
        {
            Code = code,
            Name = Text(ja, en),
            SortOrder = sortOrder
        };

    private static MenuResponseCategory Category(int id, string ja, string en, IReadOnlyList<string> tags, IReadOnlyList<Guid> itemIds) =>
        new()
        {
            Id = Id(id),
            Name = Text(ja, en),
            SortOrder = id,
            Tags = tags,
            ItemIds = itemIds
        };

    // 作る持ち場がない品 (ドリンクバー) はお客様が自分でとる
    private static MenuResponseItem Item(
        Guid id,
        string code,
        string ja,
        string en,
        decimal price,
        string imageName,
        IReadOnlyList<ItemBadge> badges,
        Guid? stationId,
        IReadOnlyList<string> tags,
        IReadOnlyList<Guid> optionGroupIds,
        int? calories,
        IReadOnlyList<string> allergenCodes,
        OrderTiming timing = OrderTiming.Now) =>
        new()
        {
            Id = id,
            Code = code,
            Name = Text(ja, en),
            Description = Descriptions.GetValueOrDefault(id),
            Price = price,
            TaxRate = 0.10m,
            ImageName = imageName,
            Badges = badges,
            AllergenCodes = allergenCodes,
            Calories = calories,
            Tags = tags,
            StationId = stationId,
            ServedBy = stationId is null ? ServedBy.Guest : ServedBy.Staff,
            OptionGroupIds = optionGroupIds,
            DefaultTiming = timing,
            TimingSelectable = timing == OrderTiming.AfterMeal
        };

    private static MenuResponseOptionGroup Group(Guid id, string ja, string en, int min, int max, IReadOnlyList<MenuResponseOption> options) =>
        new()
        {
            Id = id,
            Name = Text(ja, en),
            MinSelect = min,
            MaxSelect = max,
            Options = options
        };

    private static MenuResponseOption Option(int id, string ja, string en, decimal priceDelta, bool isDefault = false, IReadOnlyList<string>? tags = null) =>
        new()
        {
            Id = Id(id),
            Name = Text(ja, en),
            PriceDelta = priceDelta,
            IsDefault = isDefault,
            Tags = tags ?? [],
            AllergenCodes = []
        };

    private static MenuResponseAllergen Allergen(string code, string ja, string en) =>
        new()
        {
            Code = code,
            Name = Text(ja, en),
            IsMandatory = true
        };
}
