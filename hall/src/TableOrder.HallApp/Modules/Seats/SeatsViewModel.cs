namespace TableOrder.HallApp.Modules.Seats;

using TableOrder.HallApp.Modules.Dialogs;

// 席のタブ。テーブルを 2 列のタイルで並べ、空きを押すと案内 (人数を入れて来店を開く)、来店中と会計中を押すと来店の詳細を開く
// タイルは通知で読み直した席の一覧 (TableState) から出し、経過時間は時間ごとに出し直す
public sealed class SeatsViewModel : TabViewModelBase
{
    // 経過時間を出し直す間隔
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    // 案内のはじめの人数
    private const int DefaultAdults = 2;

    private readonly IPopupNavigator popupNavigator;

    private readonly TimeProvider timeProvider;

    private readonly TableState tableState;

    private readonly HallUsecase hallUsecase;

    public ObservableCollection<SeatTile> Tiles { get; } = [];

    public IObserveCommand SelectCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public SeatsViewModel(
        IPopupNavigator popupNavigator,
        TimeProvider timeProvider,
        StoreState storeState,
        CallState callState,
        ServingState servingState,
        TableState tableState,
        HallUsecase hallUsecase)
        : base(ViewId.Seats, storeState, callState, servingState)
    {
        this.popupNavigator = popupNavigator;
        this.timeProvider = timeProvider;
        this.tableState = tableState;
        this.hallUsecase = hallUsecase;

        SelectCommand = MakeAsyncCommand<SeatTile>(SelectAsync);

        UpdateTiles();
        Disposables.Add(Observable.Interval(TickInterval).ObserveOnCurrentContext().Subscribe(_ => Tick()));
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    protected override Task OnTablesChangedAsync()
    {
        UpdateTiles();
        return Task.CompletedTask;
    }

    //--------------------------------------------------------------------------------
    // Seat
    //--------------------------------------------------------------------------------

    // 並びが同じなら中身だけを替え (スクロールの位置を保つ)、違えば作り直す
    private void UpdateTiles()
    {
        var now = timeProvider.GetUtcNow();
        var tables = tableState.Items;
        if ((Tiles.Count == tables.Count) && Tiles.Select(static x => x.TableId).SequenceEqual(tables.Select(static x => x.Id)))
        {
            for (var i = 0; i < tables.Count; i++)
            {
                Tiles[i].Update(tables[i], now);
            }

            return;
        }

        Tiles.Clear();
        foreach (var table in tables)
        {
            Tiles.Add(new SeatTile(table, now));
        }
    }

    private void Tick()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var tile in Tiles)
        {
            tile.Tick(now);
        }
    }

    private async Task SelectAsync(SeatTile tile)
    {
        if (tile.VisitId is { } visitId)
        {
            await Navigator.ForwardAsync(ViewId.Visit, Parameters.MakeVisit(visitId));
            return;
        }

        await GuideAsync(tile);
    }

    // 人数を入れて来店を開く (定員を添えるが、超えても止めない)。断られたら知らせる (席の一覧は読み直してある)
    private async Task GuideAsync(SeatTile tile)
    {
        var title = ViewHelper.Format(AppResources.GuideTitleFormat, tile.Name);
        var guests = await popupNavigator.GuestCountAsync(new GuestCountParameter(
            title,
            ViewHelper.Format(AppResources.GuideCapacityFormat, tile.Capacity),
            DefaultAdults,
            0,
            AppResources.GuideOpen));
        if (guests is null)
        {
            return;
        }

        var result = await hallUsecase.OpenVisitAsync(tile.TableId, guests.Adults, guests.Children);
        UpdateTiles();
        if (!result.IsSuccess)
        {
            await popupNavigator.MessageAsync(title, ViewHelper.ErrorMessage(result));
        }
    }
}
