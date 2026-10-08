namespace TableOrder.Client;

// テーブル端末が使う注文サーバの API。窓口は端末の種類 (テーブル、ホール、キッチン、受付) ごとに分ける
// 端末の登録と設定は、すべての端末に共通の IDeviceApi に置く。実装は通信の方式 (REST、gRPC) ごとに作る
public interface ITableApi
{
    //--------------------------------------------------------------------------------
    // Menu
    //--------------------------------------------------------------------------------

    // GET /menu
    ValueTask<ApiResult<MenuResponse>> GetMenuAsync(CancellationToken cancel = default);

    // GET /stock
    ValueTask<ApiResult<StockResponse>> GetStockAsync(CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    // GET /store
    ValueTask<ApiResult<StoreResponse>> GetStoreAsync(CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    // GET /devices/me/visit (来店がなければ内容が null)
    ValueTask<ApiResult<VisitResponse?>> GetCurrentVisitAsync(CancellationToken cancel = default);

    // POST /visits/{visitId}/confirmations
    ValueTask<ApiResult<VisitResponse>> ConfirmAsync(Guid visitId, VisitConfirmationRequest request, CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    // POST /visits/{visitId}/orders
    ValueTask<ApiResult<OrderListResponseItem>> CreateOrderAsync(Guid visitId, OrderCreateRequest request, CancellationToken cancel = default);

    // GET /visits/{visitId}/orders
    ValueTask<ApiResult<OrderListResponse>> GetOrdersAsync(Guid visitId, CancellationToken cancel = default);

    // POST /visits/{visitId}/orders/release
    ValueTask<ApiResult<OrderListResponse>> ReleaseAsync(Guid visitId, OrderReleaseRequest request, CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Call
    //--------------------------------------------------------------------------------

    // POST /visits/{visitId}/calls
    ValueTask<ApiResult<CallListResponseItem>> CreateCallAsync(Guid visitId, CallCreateRequest request, CancellationToken cancel = default);

    // GET /visits/{visitId}/calls
    ValueTask<ApiResult<CallListResponse>> GetCallsAsync(Guid visitId, CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Bill / Payment
    //--------------------------------------------------------------------------------

    // GET /visits/{visitId}/bill
    ValueTask<ApiResult<BillResponse>> GetBillAsync(Guid visitId, CancellationToken cancel = default);

    // POST /visits/{visitId}/checkout
    ValueTask<ApiResult<VisitResponse>> StartCheckoutAsync(Guid visitId, CheckoutRequest request, CancellationToken cancel = default);

    // POST /visits/{visitId}/checkout/cancel
    ValueTask<ApiResult<VisitResponse>> CancelCheckoutAsync(Guid visitId, CancellationToken cancel = default);

    // POST /visits/{visitId}/payments
    ValueTask<ApiResult<PaymentResponse>> CreatePaymentAsync(Guid visitId, PaymentCreateRequest request, CancellationToken cancel = default);

    // GET /payments/{paymentId}
    ValueTask<ApiResult<PaymentResponse>> GetPaymentAsync(Guid paymentId, CancellationToken cancel = default);

    // POST /payments/{paymentId}/cancel
    ValueTask<ApiResult<PaymentResponse>> CancelPaymentAsync(Guid paymentId, CancellationToken cancel = default);

    // GET /visits/{visitId}/receipt
    ValueTask<ApiResult<ReceiptResponse>> GetReceiptAsync(Guid visitId, CancellationToken cancel = default);
}
