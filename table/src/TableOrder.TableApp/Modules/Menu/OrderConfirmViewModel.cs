namespace TableOrder.TableApp.Modules.Menu;

// 注文の確認。明細と合計を見せ、提案のルールに当たれば 1 枠だけ提案する。送れたら受け付けの知らせを出して閉じる
public sealed partial class OrderConfirmViewModel : AppDialogViewModelBase
{
    private static readonly TimeSpan CompletedDisplay = TimeSpan.FromSeconds(2.5);

    private readonly ILogger<OrderConfirmViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly MenuState menuState;

    private readonly CartState cartState;

    private readonly LanguageState languageState;

    private readonly OrderUsecase orderUsecase;

    public ObservableCollection<ConfirmLine> Lines { get; } = [];

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSuggestion { get; set; }

    [ObservableProperty]
    public partial string SuggestionMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuggestionRemainingText { get; set; } = string.Empty;

    public ObservableCollection<SuggestItem> SuggestItems { get; } = [];

    [ObservableProperty]
    public partial bool IsFailed { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    [ObservableProperty]
    public partial string SubmitText { get; set; } = AppResources.ConfirmSubmit;

    public IObserveCommand AddSuggestionCommand { get; }

    public IObserveCommand BackCommand { get; }

    public IObserveCommand SubmitCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public OrderConfirmViewModel(
        ILogger<OrderConfirmViewModel> log,
        IPopupNavigator popupNavigator,
        MenuState menuState,
        CartState cartState,
        LanguageState languageState,
        OrderUsecase orderUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.menuState = menuState;
        this.cartState = cartState;
        this.languageState = languageState;
        this.orderUsecase = orderUsecase;

        AddSuggestionCommand = MakeDelegateCommand<SuggestItem>(AddSuggestion);
        BackCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync(false));
        SubmitCommand = MakeAsyncCommand(SubmitAsync);

        Refresh();
    }

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    private void Refresh()
    {
        var language = languageState.Current;

        Lines.Clear();
        foreach (var line in cartState.Lines)
        {
            Lines.Add(new ConfirmLine(menuState.GetItem(line.ItemId).Name.Get(language), menuState.OptionText(line.OptionIds, language), line));
        }

        CountText = ViewHelper.Count(cartState.Count);
        TotalText = ViewHelper.Price(cartState.Total);

        SuggestItems.Clear();
        var suggestion = orderUsecase.GetSuggestion();
        HasSuggestion = suggestion is not null;
        if (suggestion is not null)
        {
            SuggestionMessage = suggestion.Rule.Message.Get(language, AppResources.SuggestionDefault)!;
            SuggestionRemainingText = ViewHelper.Format(AppResources.SuggestionRemainingFormat, suggestion.Shortage);
            foreach (var item in suggestion.Items)
            {
                SuggestItems.Add(new SuggestItem(item.Id, item.Name.Get(language), menuState.UnitPrice(item.Id, [])));
            }
        }
    }

    // 提案の品を 1 つ足す (足りなくなるまで続けて押せる)
    private void AddSuggestion(SuggestItem suggest)
    {
        var selection = OrderUsecase.CreateSelection(menuState.GetItem(suggest.Id));
        if (orderUsecase.FindExceededLimit(selection) is not null)
        {
            return;
        }

        cartState.Add(selection, menuState.UnitPrice(suggest.Id, []));
        IsFailed = false;
        Refresh();
    }

    // 通信できないときはカートを残し、同じ注文として送り直せるようにする
    private async Task SubmitAsync()
    {
        IsFailed = false;

        var result = await orderUsecase.SubmitAsync();
        if (!result.IsSuccess)
        {
            log.WarnApiFailed(nameof(ITableApi.CreateOrderAsync), result.Status, result.ErrorCode);
            ErrorText = ViewHelper.ErrorMessage(result);
            IsFailed = true;
            SubmitText = AppResources.ConfirmRetry;
            return;
        }

        IsCompleted = true;
        await Task.Delay(CompletedDisplay);
        await popupNavigator.CloseAsync(true);
    }
}
