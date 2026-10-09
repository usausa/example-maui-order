namespace TableOrder.Client.Rest;

// 受付機の要求の REST の窓口
public sealed class RestReceptionApi : IReceptionApi
{
    private readonly RestConnection connection;

    public RestReceptionApi(RestConnection connection)
    {
        this.connection = connection;
    }

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<StoreResponse>> GetStoreAsync(CancellationToken cancel = default) =>
        connection.GetAsync("store", ClientJsonContext.Default.StoreResponse, cancel);

    public ValueTask<ApiResult<TableListResponse>> GetTablesAsync(TableStatus? status = null, CancellationToken cancel = default) =>
        connection.GetAsync(status is { } value ? $"tables?status={value}" : "tables", ClientJsonContext.Default.TableListResponse, cancel);

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<VisitResponse>> OpenVisitAsync(VisitCreateRequest request, CancellationToken cancel = default) =>
        connection.PostAsync("visits", request, ClientJsonContext.Default.VisitCreateRequest, ClientJsonContext.Default.VisitResponse, cancel);
}
