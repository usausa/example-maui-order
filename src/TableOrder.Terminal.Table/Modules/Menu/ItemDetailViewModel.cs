namespace TableOrder.Terminal.Table.Modules.Menu;

// 商品の詳細 (大きな絵、説明、アレルギー、オプション、出す時機、数量)。選んだ内容を返し、カートに入れるのは注文の画面が行う
public sealed partial class ItemDetailViewModel : AppDialogViewModelBase, IPopupInitialize<ItemDetailParameter>
{
    private readonly MenuState menuState;

    private readonly LanguageState languageState;

    private MenuResponseItem item = default!;

    private int maxQuantity = 1;

    private string commitName = string.Empty;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PriceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ImageName { get; set; }

    [ObservableProperty]
    public partial string BadgeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasBadge { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDescription { get; set; }

    [ObservableProperty]
    public partial string AllergenText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CaloriesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCalories { get; set; }

    [ObservableProperty]
    public partial bool IsSoldOut { get; set; }

    public ObservableCollection<OptionGroupChoice> Groups { get; } = [];

    public ObservableCollection<TimingChoice> Timings { get; } = [];

    [ObservableProperty]
    public partial bool HasTiming { get; set; }

    [ObservableProperty]
    public partial int Quantity { get; set; } = 1;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    // 入れるボタンの文言と合計 (例: カートに入れる  ¥1,628)
    [ObservableProperty]
    public partial string CommitText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanCommit { get; set; }

    public IObserveCommand SelectOptionCommand { get; }

    public IObserveCommand SelectTimingCommand { get; }

    public IObserveCommand DecreaseCommand { get; }

    public IObserveCommand IncreaseCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public ItemDetailViewModel(
        IPopupNavigator popupNavigator,
        MenuState menuState,
        LanguageState languageState)
    {
        this.menuState = menuState;
        this.languageState = languageState;

        SelectOptionCommand = MakeDelegateCommand<OptionChoice>(SelectOption);
        SelectTimingCommand = MakeDelegateCommand<TimingChoice>(SelectTiming);
        DecreaseCommand = MakeDelegateCommand(() => ChangeQuantity(Quantity - 1), () => Quantity > 1);
        IncreaseCommand = MakeDelegateCommand(() => ChangeQuantity(Quantity + 1), () => Quantity < maxQuantity);
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<ItemSelection?>(null));
        CommitCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<ItemSelection?>(CreateSelection()), () => CanCommit);
    }

    //--------------------------------------------------------------------------------
    // Initialize
    //--------------------------------------------------------------------------------

    public void Initialize(ItemDetailParameter parameter)
    {
        var language = languageState.Current;
        var line = parameter.Line;
        item = menuState.GetItem(parameter.ItemId);
        maxQuantity = menuState.MaxQuantity(item);

        Name = item.Name.Get(language);
        PriceText = ViewHelper.Price(item.Price);
        ImageName = item.ImageName;
        BadgeText = item.Badges.Count > 0 ? ViewHelper.Name(item.Badges[0]) : string.Empty;
        HasBadge = item.Badges.Count > 0;
        Description = item.Description.Get(language, string.Empty)!;
        HasDescription = Description.Length > 0;
        AllergenText = item.AllergenCodes.Count > 0
            ? String.Join(AppResources.ListSeparator, item.AllergenCodes.Select(x => menuState.AllergenName(x, language)))
            : AppResources.DetailAllergenNone;
        CaloriesText = item.Calories is { } calories ? ViewHelper.Format(AppResources.DetailCaloriesFormat, calories) : string.Empty;
        HasCalories = item.Calories is not null;
        IsSoldOut = menuState.IsSoldOut(item.Id);

        // 新しく入れるときは既定のオプション、直すときは選んでいたオプション
        var selected = line?.OptionIds ?? [];
        foreach (var group in menuState.GetOptionGroups(item))
        {
            Groups.Add(new OptionGroupChoice(group, language, selected.ToList(), line is null));
        }

        HasTiming = item.TimingSelectable;
        if (HasTiming)
        {
            var timing = line?.Timing ?? item.DefaultTiming;
            Timings.Add(new TimingChoice(OrderTiming.Now, AppResources.DetailTimingNow, timing == OrderTiming.Now));
            Timings.Add(new TimingChoice(OrderTiming.AfterMeal, AppResources.DetailTimingAfterMeal, timing == OrderTiming.AfterMeal));
        }

        Quantity = Math.Min(line?.Quantity ?? 1, maxQuantity);
        commitName = line is null ? AppResources.DetailAdd : AppResources.DetailUpdate;

        Update();
    }

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    // 1 つだけ選ぶ組は選び替え、任意の組はもう一度押すと外す。複数を選べる組は上限まで
    private void SelectOption(OptionChoice option)
    {
        var group = option.Group;
        if (option.IsSelected)
        {
            if (group.IsRequired && (group.MaxSelect == 1))
            {
                return;
            }

            option.IsSelected = false;
        }
        else if (group.MaxSelect == 1)
        {
            foreach (var other in group.Options)
            {
                other.IsSelected = other == option;
            }
        }
        else if (group.Options.Count(static x => x.IsSelected) < group.MaxSelect)
        {
            option.IsSelected = true;
        }

        Update();
    }

    private void SelectTiming(TimingChoice timing)
    {
        foreach (var choice in Timings)
        {
            choice.IsSelected = choice == timing;
        }
    }

    private void ChangeQuantity(int quantity)
    {
        Quantity = Math.Clamp(quantity, 1, maxQuantity);
        Update();
    }

    private void Update()
    {
        var unitPrice = Pricing.UnitPrice(item.Price, Groups.SelectMany(static x => x.Options).Where(static x => x.IsSelected).Select(static x => x.PriceDelta));
        TotalText = ViewHelper.Price(unitPrice * Quantity);
        CommitText = $"{commitName}  {TotalText}";
        CanCommit = !IsSoldOut && Groups.All(static x => x.IsSatisfied);
    }

    private ItemSelection CreateSelection() =>
        new(
            item.Id,
            Groups.SelectMany(static x => x.Options).Where(static x => x.IsSelected).Select(static x => x.Id).ToList(),
            Quantity,
            Timings.FirstOrDefault(static x => x.IsSelected)?.Timing ?? item.DefaultTiming);
}
