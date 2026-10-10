namespace TableOrder.HallApp.Modules;

// 下部のタブの画面 (席、呼び出し、提供、品切れ) の基底。ヘッダ (タブの名前、店舗の名前、注文の一時停止の知らせ) とタブの帯 (切り替え、待っている件数) を持つ
// ヘッダとタブの帯の部品 (Controls/TabHeader、Controls/TabFooter) はこの型にバインドする
// 店舗の設定の版が替わったら、タブの画面で起動からやり直して読み直す (入ったときと store.updated。来店の詳細と代わりの注文の画面では、入れかけの操作を捨てないように替えない)
public abstract partial class TabViewModelBase : AppViewModelBase
{
    private readonly ViewId tab;

    private readonly StoreState storeState;

    private readonly CallState callState;

    private readonly ServingState servingState;

    public string TitleText { get; }

    public string StoreNameText { get; }

    public bool IsSeats => tab == ViewId.Seats;

    public bool IsCalls => tab == ViewId.Calls;

    public bool IsServing => tab == ViewId.Serving;

    public bool IsStock => tab == ViewId.Stock;

    [ObservableProperty]
    public partial bool IsOrderingPaused { get; set; }

    // まだ誰も向かっていない呼び出しの数 (なければ空にしてバッジを出さない)
    [ObservableProperty]
    public partial string CallBadgeText { get; set; } = string.Empty;

    // できあがった明細の数 (なければ空にしてバッジを出さない)
    [ObservableProperty]
    public partial string ServingBadgeText { get; set; } = string.Empty;

    public IObserveCommand TabCommand { get; }

    public IObserveCommand DeviceCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    protected TabViewModelBase(
        ViewId tab,
        StoreState storeState,
        CallState callState,
        ServingState servingState)
    {
        this.tab = tab;
        this.storeState = storeState;
        this.callState = callState;
        this.servingState = servingState;

        TitleText = ViewHelper.TabName(tab);
        StoreNameText = ViewHelper.Text(storeState.StoreName);
        UpdateStore();
        UpdateCalls();
        UpdateServing();

        TabCommand = MakeAsyncCommand<ViewId>(SelectTabAsync);
        DeviceCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.Device, Parameters.MakeTab(tab)));
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        if (storeState.IsSettingsChanged)
        {
            await Navigator.PostForwardAsync(this, ViewId.Startup);
        }
    }

    // 席のタブに戻る。席のタブでは何もしない (アプリの外へ出さない)
    protected override async Task OnNotifyBackAsync()
    {
        if (tab != ViewId.Seats)
        {
            await Navigator.ForwardAsync(ViewId.Seats);
        }
    }

    private async Task SelectTabAsync(ViewId selected)
    {
        if (selected != tab)
        {
            await Navigator.ForwardAsync(selected);
        }
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    protected override async Task OnStoreChangedAsync()
    {
        if (storeState.IsSettingsChanged)
        {
            await Navigator.ForwardAsync(ViewId.Startup);
            return;
        }

        UpdateStore();
    }

    protected override Task OnCallsChangedAsync()
    {
        UpdateCalls();
        return Task.CompletedTask;
    }

    protected override Task OnServingChangedAsync()
    {
        UpdateServing();
        return Task.CompletedTask;
    }

    private void UpdateStore() => IsOrderingPaused = storeState.OrderingPaused;

    private void UpdateCalls() => CallBadgeText = ViewHelper.Badge(callState.WaitingCount);

    private void UpdateServing() => ServingBadgeText = ViewHelper.Badge(servingState.WaitingCount);
}
