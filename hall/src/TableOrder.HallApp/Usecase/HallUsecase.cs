namespace TableOrder.HallApp.Usecase;

// 席・呼び出し・提供の一覧の読み直し (起動と通知で使う) と、来店を開く・直す・終える操作
// 来店の操作のあとは席の一覧を読み直す (通知でも読み直すが、操作した画面に戻ったときに替わっているように)
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
        return await AfterChangeAsync(result);
    }

    public async ValueTask<ApiResult<VisitResponse>> UpdateGuestsAsync(VisitResponse visit, int adults, int children) =>
        await AfterChangeAsync(await hallApi.UpdateVisitAsync(visit.Id, new VisitUpdateRequest { Adults = adults, Children = children, Version = visit.Version }));

    public async ValueTask<ApiResult<VisitResponse>> MoveVisitAsync(VisitResponse visit, Guid toTableId) =>
        await AfterChangeAsync(await hallApi.MoveVisitAsync(visit.Id, new VisitMoveRequest { ToTableId = toTableId, Version = visit.Version }));

    // レジで払った来店を終える (会計中も終えられる)
    public async ValueTask<ApiResult<VisitResponse>> CloseVisitAsync(VisitResponse visit) =>
        await AfterChangeAsync(await hallApi.CloseVisitAsync(visit.Id, new VisitCloseRequest { ClosedBy = VisitClosedBy.Register, Version = visit.Version }));

    // 注文のないまま帰った来店を取りやめる
    public async ValueTask<ApiResult<VisitResponse>> CancelVisitAsync(VisitResponse visit) =>
        await AfterChangeAsync(await hallApi.CancelVisitAsync(visit.Id, new VisitCancelRequest { Version = visit.Version }));

    // 断られたとき (席が埋まっていた、ほかで替えられていた) も、席の一覧が古いので読み直す。通信できないときは読み直さない
    private async ValueTask<ApiResult<VisitResponse>> AfterChangeAsync(ApiResult<VisitResponse> result)
    {
        if (result.Status != ApiStatus.Unavailable)
        {
            await RefreshTablesAsync();
        }

        return result;
    }
}
