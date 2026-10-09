namespace TableOrder.HallApp.Modules.Serving;

// 提供のタブ。できあがった明細をテーブルごとに並べ、提供した明細を送る (明細ごとと、テーブルのすべて。確かめずに送る)
// 一覧は通知で読み直した提供を待つ明細 (ServingState) から出し、できあがってからの時間は時間ごとに出し直す
public sealed partial class ServingViewModel : TabViewModelBase
{
    // できあがってからの時間を出し直す間隔
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly IPopupNavigator popupNavigator;

    private readonly TimeProvider timeProvider;

    private readonly ServingState servingState;

    private readonly HallUsecase hallUsecase;

    public ObservableCollection<ServingGroup> Groups { get; } = [];

    // 提供を待つ明細がない
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public IObserveCommand ServeLineCommand { get; }

    public IObserveCommand ServeGroupCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public ServingViewModel(
        IPopupNavigator popupNavigator,
        TimeProvider timeProvider,
        StoreState storeState,
        CallState callState,
        ServingState servingState,
        HallUsecase hallUsecase)
        : base(ViewId.Serving, storeState, callState, servingState)
    {
        this.popupNavigator = popupNavigator;
        this.timeProvider = timeProvider;
        this.servingState = servingState;
        this.hallUsecase = hallUsecase;

        ServeLineCommand = MakeAsyncCommand<ServingLine>(x => ServeAsync(AppResources.ServeLine, [x.LineId]));
        ServeGroupCommand = MakeAsyncCommand<ServingGroup>(x => ServeAsync(AppResources.ServeAll, x.Lines.Select(static line => line.LineId).ToList()));

        UpdateGroups();
        Disposables.Add(Observable.Interval(TickInterval).ObserveOnCurrentContext().Subscribe(_ => Tick()));
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    protected override async Task OnServingChangedAsync()
    {
        await base.OnServingChangedAsync();
        UpdateGroups();
    }

    //--------------------------------------------------------------------------------
    // Serving
    //--------------------------------------------------------------------------------

    private void UpdateGroups()
    {
        var now = timeProvider.GetUtcNow();
        Groups.Sync(servingState.Items, static (group, item) => group.VisitId == item.VisitId, item => new ServingGroup(item, now), (group, item) => group.Update(item, now));
        IsEmpty = Groups.Count == 0;
    }

    private void Tick()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var group in Groups)
        {
            group.Tick(now);
        }
    }

    // 読み直した明細を出し (タブの件数も)、断られたら知らせる (取消になっていた、来店が終わっていたなど)
    private async Task ServeAsync(string title, IReadOnlyList<Guid> lineIds)
    {
        var result = await hallUsecase.ServeAsync(lineIds);
        await OnServingChangedAsync();
        if (!result.IsSuccess)
        {
            await popupNavigator.MessageAsync(title, ViewHelper.ErrorMessage(result));
        }
    }
}
