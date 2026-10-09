namespace TableOrder.HallApp.Modules.Dialogs;

// 代わりの注文の品の詳細。オプション (品切れは選べない)、出す時機 (選べる品だけ)、数量を選び、選んだ内容を返す (閉じたら null)
// カートに入れる前のルールの確かめ (確認、上限) は開いた画面が行う
public sealed partial class OrderItemViewModel : AppDialogViewModelBase, IPopupInitialize<OrderItemParameter>
{
    private readonly MenuState menuState;

    private MenuResponseItem item = default!;

    private int maxQuantity = 1;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    // 選んだオプションと数量の合計 (表題の右に出す。ボタンに入れると狭い画面で収まらない)
    [ObservableProperty]
    public partial string PriceText { get; set; } = string.Empty;

    public ObservableCollection<OrderOptionGroup> Groups { get; } = [];

    public ObservableCollection<OrderTimingChoice> Timings { get; } = [];

    [ObservableProperty]
    public partial bool HasTiming { get; set; }

    // 選ぶもの (オプション、出す時機) がある (なければ選択肢の欄を出さない)
    [ObservableProperty]
    public partial bool HasChoices { get; set; }

    [ObservableProperty]
    public partial int Quantity { get; set; } = 1;

    [ObservableProperty]
    public partial bool CanCommit { get; set; }

    public IObserveCommand SelectOptionCommand { get; }

    public IObserveCommand SelectTimingCommand { get; }

    public IObserveCommand DecreaseCommand { get; }

    public IObserveCommand IncreaseCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public OrderItemViewModel(
        IPopupNavigator popupNavigator,
        MenuState menuState)
    {
        this.menuState = menuState;

        SelectOptionCommand = MakeDelegateCommand<OrderOptionChoice>(SelectOption);
        SelectTimingCommand = MakeDelegateCommand<OrderTimingChoice>(SelectTiming);
        DecreaseCommand = MakeDelegateCommand(() => ChangeQuantity(Quantity - 1), () => Quantity > 1);
        IncreaseCommand = MakeDelegateCommand(() => ChangeQuantity(Quantity + 1), () => Quantity < maxQuantity);

        // 結果は開く側と同じ型 (ItemSelection?) で返す
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<ItemSelection?>(null));
        CommitCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<ItemSelection?>(CreateSelection()), () => CanCommit);
    }

    public void Initialize(OrderItemParameter parameter)
    {
        item = menuState.FindItem(parameter.ItemId)!;
        maxQuantity = Math.Max(1, parameter.MaxQuantity);

        Name = ViewHelper.Text(item.Name);
        foreach (var group in menuState.GetOptionGroups(item))
        {
            Groups.Add(new OrderOptionGroup(group, menuState));
        }

        HasTiming = item.TimingSelectable;
        if (HasTiming)
        {
            Timings.Add(new OrderTimingChoice(OrderTiming.Now, AppResources.ItemTimingNow, item.DefaultTiming == OrderTiming.Now));
            Timings.Add(new OrderTimingChoice(OrderTiming.AfterMeal, AppResources.ItemTimingAfterMeal, item.DefaultTiming == OrderTiming.AfterMeal));
        }

        HasChoices = (Groups.Count > 0) || HasTiming;
        Update();
    }

    // 1 つだけ選ぶ組は選び替え、任意の組はもう一度押すと外す。複数を選べる組は上限まで
    private void SelectOption(OrderOptionChoice option)
    {
        if (option.IsSoldOut)
        {
            return;
        }

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

    private void SelectTiming(OrderTimingChoice timing)
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
        var unitPrice = Pricing.UnitPrice(item.Price, SelectedOptions().Select(static x => x.PriceDelta));
        PriceText = ViewHelper.Price(unitPrice * Quantity);
        CanCommit = Groups.All(static x => x.IsSatisfied);
    }

    private IEnumerable<OrderOptionChoice> SelectedOptions() =>
        Groups.SelectMany(static x => x.Options).Where(static x => x.IsSelected);

    private ItemSelection CreateSelection() =>
        new(
            item.Id,
            SelectedOptions().Select(static x => x.Id).ToList(),
            Quantity,
            Timings.FirstOrDefault(static x => x.IsSelected)?.Timing ?? item.DefaultTiming);
}
