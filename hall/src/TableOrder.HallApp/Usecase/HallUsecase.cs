namespace TableOrder.HallApp.Usecase;

// 席・呼び出し・提供の一覧の読み直し (起動と通知で使う) と、来店を開く・直す・終える操作、呼び出しと提供の操作
// 操作のあとは替わった一覧を読み直す (通知でも読み直すが、操作した画面がすぐ替わるように)
public sealed class HallUsecase
{
    private readonly TableState tableState;

    private readonly CallState callState;

    private readonly ServingState servingState;

    private readonly IHallApi hallApi;

    // 送れたかわからなかった案内 (同じテーブルの次の案内で、同じ来店の id を送り直す)
    private (Guid TableId, Guid VisitId)? pendingOpen;

    public HallUsecase(
        TableState tableState,
        CallState callState,
        ServingState servingState,
        IHallApi hallApi)
    {
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

    // 案内したテーブルに来店を開く。サーバは同じ id の送り直しに、同じテーブルなら開いた来店を返す
    public async ValueTask<ApiResult<VisitResponse>> OpenVisitAsync(Guid tableId, int adults, int children)
    {
        var id = (pendingOpen is { } pending) && (pending.TableId == tableId) ? pending.VisitId : Guid.CreateVersion7();
        var result = await hallApi.OpenVisitAsync(new VisitCreateRequest { Id = id, TableId = tableId, Adults = adults, Children = children });
        pendingOpen = result.Status == ApiStatus.Unavailable ? (tableId, id) : null;
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
