namespace TableOrder.HallApp.Usecase;

// 席・呼び出し・提供の一覧を読み直して状態に入れる (起動と通知で使う)
public sealed class HallUsecase
{
    private readonly TableState tableState;

    private readonly CallState callState;

    private readonly ServingState servingState;

    private readonly IHallApi hallApi;

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
}
