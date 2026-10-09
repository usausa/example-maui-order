namespace TableOrder.Client.Rest;

// キッチン端末の要求の REST の窓口
public sealed class RestKitchenApi : IKitchenApi
{
    private readonly RestConnection connection;

    private readonly RestMenuCache menu;

    public RestKitchenApi(IDeviceContext context, RestConnection connection)
    {
        this.connection = connection;
        menu = new RestMenuCache(context, connection);
    }

    //--------------------------------------------------------------------------------
    // Menu
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<MenuResponse>> GetMenuAsync(CancellationToken cancel = default) =>
        menu.GetAsync(cancel);

    public ValueTask<ApiResult<StockResponse>> GetStockAsync(CancellationToken cancel = default) =>
        connection.GetAsync("stock", ClientJsonContext.Default.StockResponse, cancel);

    public ValueTask<ApiResult<NoContent>> UpdateStockAsync(Guid targetId, StockUpdateRequest request, CancellationToken cancel = default) =>
        connection.PutNoContentAsync($"stock/{targetId}", request, ClientJsonContext.Default.StockUpdateRequest, cancel);

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<StoreResponse>> GetStoreAsync(CancellationToken cancel = default) =>
        connection.GetAsync("store", ClientJsonContext.Default.StoreResponse, cancel);

    //--------------------------------------------------------------------------------
    // Ticket
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<KitchenTicketListResponse>> GetTicketsAsync(Guid? stationId = null, KitchenTicketStatus? status = null, CancellationToken cancel = default) =>
        connection.GetAsync(TicketsPath(stationId, status), ClientJsonContext.Default.KitchenTicketListResponse, cancel);

    public ValueTask<ApiResult<KitchenTicketListResponseItem>> StartLineAsync(Guid ticketId, Guid lineId, CancellationToken cancel = default) =>
        connection.PostEmptyAsync($"kitchen/tickets/{ticketId}/lines/{lineId}/start", ClientJsonContext.Default.KitchenTicketListResponseItem, cancel);

    public ValueTask<ApiResult<KitchenTicketListResponseItem>> ReadyLineAsync(Guid ticketId, Guid lineId, CancellationToken cancel = default) =>
        connection.PostEmptyAsync($"kitchen/tickets/{ticketId}/lines/{lineId}/ready", ClientJsonContext.Default.KitchenTicketListResponseItem, cancel);

    public ValueTask<ApiResult<KitchenTicketListResponseItem>> BumpAsync(Guid ticketId, CancellationToken cancel = default) =>
        connection.PostEmptyAsync($"kitchen/tickets/{ticketId}/bump", ClientJsonContext.Default.KitchenTicketListResponseItem, cancel);

    public ValueTask<ApiResult<KitchenTicketListResponseItem>> RecallAsync(Guid ticketId, CancellationToken cancel = default) =>
        connection.PostEmptyAsync($"kitchen/tickets/{ticketId}/recall", ClientJsonContext.Default.KitchenTicketListResponseItem, cancel);

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // チケットの絞り込み (サーバは持ち場の id と、状態を列挙型の名前で受ける)
    private static string TicketsPath(Guid? stationId, KitchenTicketStatus? status)
    {
        var query = new List<string>(2);
        if (stationId is { } station)
        {
            query.Add($"stationId={station}");
        }

        if (status is { } value)
        {
            query.Add($"status={value}");
        }

        return query.Count > 0 ? $"kitchen/tickets?{String.Join('&', query)}" : "kitchen/tickets";
    }
}
