namespace TableOrder.Client;

// ホール端末 (スタッフのハンディ) が使う注文サーバの API。端末の登録と設定は IDeviceApi、通知は IOrderEvents に置く
// 実装は通信の方式 (REST、gRPC) ごとに作る
public interface IHallApi
{
    //--------------------------------------------------------------------------------
    // Menu
    //--------------------------------------------------------------------------------

    // GET /menu (代わりの注文と品切れで使う)
    ValueTask<ApiResult<MenuResponse>> GetMenuAsync(CancellationToken cancel = default);

    // GET /stock
    ValueTask<ApiResult<StockResponse>> GetStockAsync(CancellationToken cancel = default);

    // PUT /stock/{targetId} (売り切れと残りの数)
    ValueTask<ApiResult<NoContent>> UpdateStockAsync(Guid targetId, StockUpdateRequest request, CancellationToken cancel = default);

    // POST /stock/reset (すべて戻す)
    ValueTask<ApiResult<NoContent>> ResetStockAsync(CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    // GET /store
    ValueTask<ApiResult<StoreResponse>> GetStoreAsync(CancellationToken cancel = default);

    // PUT /store/ordering (注文の一時停止と再開)
    ValueTask<ApiResult<NoContent>> SetOrderingAsync(StoreOrderingRequest request, CancellationToken cancel = default);

    // GET /tables (状態を省くとすべてのテーブル)
    ValueTask<ApiResult<TableListResponse>> GetTablesAsync(TableStatus? status = null, CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    // POST /visits (案内したテーブルと人数)
    ValueTask<ApiResult<VisitResponse>> OpenVisitAsync(VisitCreateRequest request, CancellationToken cancel = default);

    // GET /visits/{visitId}
    ValueTask<ApiResult<VisitResponse>> GetVisitAsync(Guid visitId, CancellationToken cancel = default);

    // PATCH /visits/{visitId} (人数)
    ValueTask<ApiResult<VisitResponse>> UpdateVisitAsync(Guid visitId, VisitUpdateRequest request, CancellationToken cancel = default);

    // POST /visits/{visitId}/move
    ValueTask<ApiResult<VisitResponse>> MoveVisitAsync(Guid visitId, VisitMoveRequest request, CancellationToken cancel = default);

    // POST /visits/{visitId}/close (レジで払った)
    ValueTask<ApiResult<VisitResponse>> CloseVisitAsync(Guid visitId, VisitCloseRequest request, CancellationToken cancel = default);

    // POST /visits/{visitId}/cancel (注文のないまま帰った)
    ValueTask<ApiResult<VisitResponse>> CancelVisitAsync(Guid visitId, VisitCancelRequest request, CancellationToken cancel = default);

    // POST /visits/{visitId}/confirmations (代わりの注文で、スタッフがお客様に確かめた確認のルール)
    ValueTask<ApiResult<VisitResponse>> ConfirmAsync(Guid visitId, VisitConfirmationRequest request, CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    // POST /visits/{visitId}/orders (代わりの注文)
    ValueTask<ApiResult<OrderListResponseItem>> CreateOrderAsync(Guid visitId, OrderCreateRequest request, CancellationToken cancel = default);

    // GET /visits/{visitId}/orders
    ValueTask<ApiResult<OrderListResponse>> GetOrdersAsync(Guid visitId, CancellationToken cancel = default);

    // POST /visits/{visitId}/orders/release (食後の品のお願い)
    ValueTask<ApiResult<OrderListResponse>> ReleaseAsync(Guid visitId, OrderReleaseRequest request, CancellationToken cancel = default);

    // POST /orders/{orderId}/lines/{lineId}/cancel (明細の取消)
    ValueTask<ApiResult<OrderListResponseItem>> CancelLineAsync(Guid orderId, Guid lineId, OrderLineCancelRequest request, CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Serving
    //--------------------------------------------------------------------------------

    // GET /serving (状態を省くとできあがった明細)
    ValueTask<ApiResult<ServingListResponse>> GetServingAsync(OrderLineStatus? status = null, CancellationToken cancel = default);

    // POST /serving/serve
    ValueTask<ApiResult<NoContent>> ServeAsync(ServeRequest request, CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Call
    //--------------------------------------------------------------------------------

    // GET /calls (状態を省くと終わっていない呼び出し)
    ValueTask<ApiResult<CallListResponse>> GetCallsAsync(CallStatus? status = null, CancellationToken cancel = default);

    // POST /calls/{callId}/acknowledge (向かう)
    ValueTask<ApiResult<CallListResponseItem>> AcknowledgeCallAsync(Guid callId, CancellationToken cancel = default);

    // POST /calls/{callId}/done (対応した)
    ValueTask<ApiResult<CallListResponseItem>> CompleteCallAsync(Guid callId, CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Bill
    //--------------------------------------------------------------------------------

    // GET /visits/{visitId}/bill
    ValueTask<ApiResult<BillResponse>> GetBillAsync(Guid visitId, CancellationToken cancel = default);

    // POST /visits/{visitId}/checkout (会計の手伝い)
    ValueTask<ApiResult<VisitResponse>> StartCheckoutAsync(Guid visitId, CheckoutRequest request, CancellationToken cancel = default);

    // POST /visits/{visitId}/checkout/cancel
    ValueTask<ApiResult<VisitResponse>> CancelCheckoutAsync(Guid visitId, CancellationToken cancel = default);
}
