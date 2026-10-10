namespace TableOrder.HallApp.Usecase;

// 席・呼び出し・提供の一覧の読み直し (起動と通知で使う) と、来店を開く・直す・終える操作、呼び出し・提供・品切れ・注文の一時停止の操作
// 操作のあとは替わった一覧を読み直す (通知でも読み直すが、操作した画面がすぐ替わるように)
public sealed class HallUsecase
{
    private readonly StoreState storeState;

    private readonly MenuState menuState;

    private readonly TableState tableState;

    private readonly CallState callState;

    private readonly ServingState servingState;

    private readonly IHallApi hallApi;

    // 送れたかわからなかった案内 (同じテーブルと人数の次の案内で、同じ来店の id を送り直す)
    private (Guid TableId, Guid VisitId, int Adults, int Children)? pendingOpen;

    public HallUsecase(
        StoreState storeState,
        MenuState menuState,
        TableState tableState,
        CallState callState,
        ServingState servingState,
        IHallApi hallApi)
    {
        this.storeState = storeState;
        this.menuState = menuState;
        this.tableState = tableState;
        this.callState = callState;
        this.servingState = servingState;
        this.hallApi = hallApi;
    }

    //--------------------------------------------------------------------------------
    // Refresh
    //--------------------------------------------------------------------------------

    // すべてのテーブル
    public async ValueTask<ApiResult<TableListResponse>> RefreshTablesAsync()
    {
        var result = await hallApi.GetTablesAsync();
        if (result.Content is { } tables)
        {
            tableState.Update(tables);

            // 送れたかわからなかった案内が開いていたら、送り直さない
            if ((pendingOpen is { } pending) && tableState.Items.Any(x => x.Visit?.VisitId == pending.VisitId))
            {
                pendingOpen = null;
            }
        }

        return result;
    }

    // 終わっていない呼び出し
    public async ValueTask<ApiResult<CallListResponse>> RefreshCallsAsync()
    {
        var result = await hallApi.GetCallsAsync();
        if (result.Content is { } calls)
        {
            callState.Update(calls);
        }

        return result;
    }

    // できあがった明細
    public async ValueTask<ApiResult<ServingListResponse>> RefreshServingAsync()
    {
        var result = await hallApi.GetServingAsync();
        if (result.Content is { } serving)
        {
            servingState.Update(serving);
        }

        return result;
    }

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    // 案内したテーブルに来店を開く。サーバは同じ id の送り直しに、同じテーブルなら開いた来店を返す (状態と人数は見ない)
    // 送り直すのは同じテーブルと人数のときだけにし (人数を替えたら新しい案内)、返った来店が終わっていたら (開いたあとに閉じた) 使わずに新しく開く
    public async ValueTask<ApiResult<VisitResponse>> OpenVisitAsync(Guid tableId, int adults, int children)
    {
        var resend = (pendingOpen is { } pending) && (pending.TableId == tableId) && (pending.Adults == adults) && (pending.Children == children);
        var id = resend ? pendingOpen!.Value.VisitId : Guid.CreateVersion7();
        var result = await hallApi.OpenVisitAsync(new VisitCreateRequest { Id = id, TableId = tableId, Adults = adults, Children = children });
        if (resend && (result.Content is { Status: VisitStatus.Closed or VisitStatus.Cancelled }))
        {
            id = Guid.CreateVersion7();
            result = await hallApi.OpenVisitAsync(new VisitCreateRequest { Id = id, TableId = tableId, Adults = adults, Children = children });
        }

        pendingOpen = result.Status == ApiStatus.Unavailable ? (tableId, id, adults, children) : null;
        return await AfterChangeAsync(result, RefreshTablesAsync);
    }

    public async ValueTask<ApiResult<VisitResponse>> UpdateGuestsAsync(VisitResponse visit, int adults, int children) =>
        await AfterChangeAsync(await hallApi.UpdateVisitAsync(visit.Id, new VisitUpdateRequest { Adults = adults, Children = children, Version = visit.Version }), RefreshTablesAsync);

    public async ValueTask<ApiResult<VisitResponse>> MoveVisitAsync(VisitResponse visit, Guid toTableId) =>
        await AfterChangeAsync(await hallApi.MoveVisitAsync(visit.Id, new VisitMoveRequest { ToTableId = toTableId, Version = visit.Version }), RefreshTablesAsync);

    // レジで払った来店を終える (会計中も終えられる)
    public async ValueTask<ApiResult<VisitResponse>> CloseVisitAsync(VisitResponse visit) =>
        await AfterChangeAsync(await hallApi.CloseVisitAsync(visit.Id, new VisitCloseRequest { ClosedBy = VisitClosedBy.Register, Version = visit.Version }), RefreshTablesAsync);

    // 注文のないまま帰った来店を取りやめる
    public async ValueTask<ApiResult<VisitResponse>> CancelVisitAsync(VisitResponse visit) =>
        await AfterChangeAsync(await hallApi.CancelVisitAsync(visit.Id, new VisitCancelRequest { Version = visit.Version }), RefreshTablesAsync);

    // 食後の品 (止めている明細) をすべてお願いする
    public async ValueTask<ApiResult<OrderListResponse>> ReleaseAsync(VisitResponse visit) =>
        await AfterChangeAsync(await hallApi.ReleaseAsync(visit.Id, new OrderReleaseRequest { LineIds = [] }), RefreshTablesAsync);

    // 明細を取り消す (数量の一部の取消は、サーバが明細を分けて取り消す)
    public async ValueTask<ApiResult<OrderListResponseItem>> CancelLineAsync(Guid orderId, Guid lineId, int quantity) =>
        await AfterChangeAsync(await hallApi.CancelLineAsync(orderId, lineId, new OrderLineCancelRequest { Quantity = quantity }), RefreshTablesAsync);

    // 会計を始める (表示していた明細の版を送り、明細が変わっていれば断られる)
    public async ValueTask<ApiResult<VisitResponse>> StartCheckoutAsync(VisitResponse visit, string billVersion) =>
        await AfterChangeAsync(await hallApi.StartCheckoutAsync(visit.Id, new CheckoutRequest { BillVersion = billVersion, Version = visit.Version }), RefreshTablesAsync);

    // 会計を取りやめる (払い終えた支払があれば、サーバは会計中のまま返す)
    public async ValueTask<ApiResult<VisitResponse>> CancelCheckoutAsync(VisitResponse visit) =>
        await AfterChangeAsync(await hallApi.CancelCheckoutAsync(visit.Id), RefreshTablesAsync);

    //--------------------------------------------------------------------------------
    // Call
    //--------------------------------------------------------------------------------

    // 向かう (テーブル端末に「向かっています」と出す)。ほかの端末が先に進めていたら、サーバは今の呼び出しを返す
    public async ValueTask<ApiResult<CallListResponseItem>> AcknowledgeCallAsync(Guid callId) =>
        await AfterChangeAsync(await hallApi.AcknowledgeCallAsync(callId), RefreshCallsAsync);

    // 対応した (向かわずに対応してもよい)
    public async ValueTask<ApiResult<CallListResponseItem>> CompleteCallAsync(Guid callId) =>
        await AfterChangeAsync(await hallApi.CompleteCallAsync(callId), RefreshCallsAsync);

    //--------------------------------------------------------------------------------
    // Serving
    //--------------------------------------------------------------------------------

    // 提供した明細 (提供済みの明細は、サーバが変えずに飛ばす)
    public async ValueTask<ApiResult<NoContent>> ServeAsync(IReadOnlyList<Guid> lineIds) =>
        await AfterChangeAsync(await hallApi.ServeAsync(new ServeRequest { LineIds = lineIds }), RefreshServingAsync);

    //--------------------------------------------------------------------------------
    // Stock
    //--------------------------------------------------------------------------------

    // 売れない品と残りの数のある品
    public async ValueTask<ApiResult<StockResponse>> RefreshStockAsync()
    {
        var result = await hallApi.GetStockAsync();
        if (result.Content is { } stock)
        {
            menuState.UpdateStock(stock);
        }

        return result;
    }

    // 品切れと残りの数 (残りの数は Limited のときだけ。0 はサーバが品切れにする)
    public async ValueTask<ApiResult<NoContent>> UpdateStockAsync(Guid targetId, StockTargetKind kind, StockStatus status, int? remaining) =>
        await AfterChangeAsync(await hallApi.UpdateStockAsync(targetId, new StockUpdateRequest { TargetKind = kind, Status = status, Remaining = remaining }), RefreshStockAsync);

    // すべての品を売れるように戻す
    public async ValueTask<ApiResult<NoContent>> ResetStockAsync() =>
        await AfterChangeAsync(await hallApi.ResetStockAsync(), RefreshStockAsync);

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    // 店舗の今の状態 (注文の一時停止)
    public async ValueTask<ApiResult<StoreResponse>> RefreshStoreAsync()
    {
        var result = await hallApi.GetStoreAsync();
        if (result.Content is { } store)
        {
            storeState.UpdateStore(store);
        }

        return result;
    }

    // 注文の一時停止と再開。テーブル端末に出す文言は送らない (テーブル端末の既定の文言を出す)
    public async ValueTask<ApiResult<NoContent>> SetOrderingAsync(bool paused) =>
        await AfterChangeAsync(await hallApi.SetOrderingAsync(new StoreOrderingRequest { Paused = paused }), RefreshStoreAsync);

    // 断られたとき (席が埋まっていた、ほかで替えられていた、来店が終わっていた) も、一覧が古いので読み直す。通信できないときは読み直さない
    private static async ValueTask<ApiResult<T>> AfterChangeAsync<T, TList>(ApiResult<T> result, Func<ValueTask<ApiResult<TList>>> refresh)
    {
        if (result.Status != ApiStatus.Unavailable)
        {
            await refresh();
        }

        return result;
    }
}
