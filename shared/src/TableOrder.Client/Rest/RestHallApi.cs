namespace TableOrder.Client.Rest;

// ホール端末の要求の REST の窓口
public sealed class RestHallApi : IHallApi
{
    private readonly RestConnection connection;

    private readonly RestMenuCache menu;

    public RestHallApi(IDeviceContext context, RestConnection connection)
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

    public ValueTask<ApiResult<NoContent>> ResetStockAsync(CancellationToken cancel = default) =>
        connection.PostEmptyNoContentAsync("stock/reset", cancel);

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<StoreResponse>> GetStoreAsync(CancellationToken cancel = default) =>
        connection.GetAsync("store", ClientJsonContext.Default.StoreResponse, cancel);

    public ValueTask<ApiResult<NoContent>> SetOrderingAsync(StoreOrderingRequest request, CancellationToken cancel = default) =>
        connection.PutNoContentAsync("store/ordering", request, ClientJsonContext.Default.StoreOrderingRequest, cancel);

    public ValueTask<ApiResult<TableListResponse>> GetTablesAsync(TableStatus? status = null, CancellationToken cancel = default) =>
        connection.GetAsync(WithStatus("tables", status), ClientJsonContext.Default.TableListResponse, cancel);

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<VisitResponse>> OpenVisitAsync(VisitCreateRequest request, CancellationToken cancel = default) =>
        connection.PostAsync("visits", request, ClientJsonContext.Default.VisitCreateRequest, ClientJsonContext.Default.VisitResponse, cancel);

    public ValueTask<ApiResult<VisitResponse>> GetVisitAsync(Guid visitId, CancellationToken cancel = default) =>
        connection.GetAsync($"visits/{visitId}", ClientJsonContext.Default.VisitResponse, cancel);

    public ValueTask<ApiResult<VisitResponse>> UpdateVisitAsync(Guid visitId, VisitUpdateRequest request, CancellationToken cancel = default) =>
        connection.PatchAsync($"visits/{visitId}", request, ClientJsonContext.Default.VisitUpdateRequest, ClientJsonContext.Default.VisitResponse, cancel);

    public ValueTask<ApiResult<VisitResponse>> MoveVisitAsync(Guid visitId, VisitMoveRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"visits/{visitId}/move", request, ClientJsonContext.Default.VisitMoveRequest, ClientJsonContext.Default.VisitResponse, cancel);

    public ValueTask<ApiResult<VisitResponse>> CloseVisitAsync(Guid visitId, VisitCloseRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"visits/{visitId}/close", request, ClientJsonContext.Default.VisitCloseRequest, ClientJsonContext.Default.VisitResponse, cancel);

    public ValueTask<ApiResult<VisitResponse>> CancelVisitAsync(Guid visitId, VisitCancelRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"visits/{visitId}/cancel", request, ClientJsonContext.Default.VisitCancelRequest, ClientJsonContext.Default.VisitResponse, cancel);

    public ValueTask<ApiResult<VisitResponse>> ConfirmAsync(Guid visitId, VisitConfirmationRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"visits/{visitId}/confirmations", request, ClientJsonContext.Default.VisitConfirmationRequest, ClientJsonContext.Default.VisitResponse, cancel);

    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<OrderListResponseItem>> CreateOrderAsync(Guid visitId, OrderCreateRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"visits/{visitId}/orders", request, ClientJsonContext.Default.OrderCreateRequest, ClientJsonContext.Default.OrderListResponseItem, cancel);

    public ValueTask<ApiResult<OrderListResponse>> GetOrdersAsync(Guid visitId, CancellationToken cancel = default) =>
        connection.GetAsync($"visits/{visitId}/orders", ClientJsonContext.Default.OrderListResponse, cancel);

    public ValueTask<ApiResult<OrderListResponse>> ReleaseAsync(Guid visitId, OrderReleaseRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"visits/{visitId}/orders/release", request, ClientJsonContext.Default.OrderReleaseRequest, ClientJsonContext.Default.OrderListResponse, cancel);

    public ValueTask<ApiResult<OrderListResponseItem>> CancelLineAsync(Guid orderId, Guid lineId, OrderLineCancelRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"orders/{orderId}/lines/{lineId}/cancel", request, ClientJsonContext.Default.OrderLineCancelRequest, ClientJsonContext.Default.OrderListResponseItem, cancel);

    //--------------------------------------------------------------------------------
    // Serving
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<ServingListResponse>> GetServingAsync(OrderLineStatus? status = null, CancellationToken cancel = default) =>
        connection.GetAsync(WithStatus("serving", status), ClientJsonContext.Default.ServingListResponse, cancel);

    public ValueTask<ApiResult<NoContent>> ServeAsync(ServeRequest request, CancellationToken cancel = default) =>
        connection.PostNoContentAsync("serving/serve", request, ClientJsonContext.Default.ServeRequest, cancel);

    //--------------------------------------------------------------------------------
    // Call
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<CallListResponse>> GetCallsAsync(CallStatus? status = null, CancellationToken cancel = default) =>
        connection.GetAsync(WithStatus("calls", status), ClientJsonContext.Default.CallListResponse, cancel);

    public ValueTask<ApiResult<CallListResponseItem>> AcknowledgeCallAsync(Guid callId, CancellationToken cancel = default) =>
        connection.PostEmptyAsync($"calls/{callId}/acknowledge", ClientJsonContext.Default.CallListResponseItem, cancel);

    public ValueTask<ApiResult<CallListResponseItem>> CompleteCallAsync(Guid callId, CancellationToken cancel = default) =>
        connection.PostEmptyAsync($"calls/{callId}/done", ClientJsonContext.Default.CallListResponseItem, cancel);

    //--------------------------------------------------------------------------------
    // Bill
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<BillResponse>> GetBillAsync(Guid visitId, CancellationToken cancel = default) =>
        connection.GetAsync($"visits/{visitId}/bill", ClientJsonContext.Default.BillResponse, cancel);

    public ValueTask<ApiResult<VisitResponse>> StartCheckoutAsync(Guid visitId, CheckoutRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"visits/{visitId}/checkout", request, ClientJsonContext.Default.CheckoutRequest, ClientJsonContext.Default.VisitResponse, cancel);

    public ValueTask<ApiResult<VisitResponse>> CancelCheckoutAsync(Guid visitId, CancellationToken cancel = default) =>
        connection.PostEmptyAsync($"visits/{visitId}/checkout/cancel", ClientJsonContext.Default.VisitResponse, cancel);

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // 一覧の状態の絞り込み (サーバは列挙型の名前で受ける)
    private static string WithStatus<T>(string path, T? status)
        where T : struct, Enum =>
        status is { } value ? $"{path}?status={value}" : path;
}
