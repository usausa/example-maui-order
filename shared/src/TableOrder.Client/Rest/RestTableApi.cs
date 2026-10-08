namespace TableOrder.Client.Rest;

using System.Net;
using System.Net.Http;

// テーブル端末の要求の REST の窓口
public sealed class RestTableApi : ITableApi
{
    private readonly RestConnection connection;

    private readonly RestMenuCache menu;

    public RestTableApi(IDeviceContext context, RestConnection connection)
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

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<StoreResponse>> GetStoreAsync(CancellationToken cancel = default) =>
        connection.GetAsync("store", ClientJsonContext.Default.StoreResponse, cancel);

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    // 来店がなければ 204 を受けて内容を null にする
    public ValueTask<ApiResult<VisitResponse?>> GetCurrentVisitAsync(CancellationToken cancel = default) =>
        connection.SendWithTokenAsync(
            uri => new HttpRequestMessage(HttpMethod.Get, uri),
            async (response, token) => response.StatusCode == HttpStatusCode.NoContent
                ? null
                : await RestConnection.ReadAsync(response, ClientJsonContext.Default.VisitResponse, token),
            "devices/me/visit",
            cancel);

    public ValueTask<ApiResult<VisitResponse>> StartVisitAsync(VisitCreateRequest request, CancellationToken cancel = default) =>
        connection.PostAsync("visits", request, ClientJsonContext.Default.VisitCreateRequest, ClientJsonContext.Default.VisitResponse, cancel);

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

    //--------------------------------------------------------------------------------
    // Call
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<CallListResponseItem>> CreateCallAsync(Guid visitId, CallCreateRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"visits/{visitId}/calls", request, ClientJsonContext.Default.CallCreateRequest, ClientJsonContext.Default.CallListResponseItem, cancel);

    public ValueTask<ApiResult<CallListResponse>> GetCallsAsync(Guid visitId, CancellationToken cancel = default) =>
        connection.GetAsync($"visits/{visitId}/calls", ClientJsonContext.Default.CallListResponse, cancel);

    //--------------------------------------------------------------------------------
    // Bill / Payment
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<BillResponse>> GetBillAsync(Guid visitId, CancellationToken cancel = default) =>
        connection.GetAsync($"visits/{visitId}/bill", ClientJsonContext.Default.BillResponse, cancel);

    public ValueTask<ApiResult<VisitResponse>> StartCheckoutAsync(Guid visitId, CheckoutRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"visits/{visitId}/checkout", request, ClientJsonContext.Default.CheckoutRequest, ClientJsonContext.Default.VisitResponse, cancel);

    public ValueTask<ApiResult<VisitResponse>> CancelCheckoutAsync(Guid visitId, CancellationToken cancel = default) =>
        connection.PostEmptyAsync($"visits/{visitId}/checkout/cancel", ClientJsonContext.Default.VisitResponse, cancel);

    public ValueTask<ApiResult<PaymentResponse>> CreatePaymentAsync(Guid visitId, PaymentCreateRequest request, CancellationToken cancel = default) =>
        connection.PostAsync($"visits/{visitId}/payments", request, ClientJsonContext.Default.PaymentCreateRequest, ClientJsonContext.Default.PaymentResponse, cancel);

    public ValueTask<ApiResult<PaymentResponse>> GetPaymentAsync(Guid paymentId, CancellationToken cancel = default) =>
        connection.GetAsync($"payments/{paymentId}", ClientJsonContext.Default.PaymentResponse, cancel);

    public ValueTask<ApiResult<PaymentResponse>> CancelPaymentAsync(Guid paymentId, CancellationToken cancel = default) =>
        connection.PostEmptyAsync($"payments/{paymentId}/cancel", ClientJsonContext.Default.PaymentResponse, cancel);

    public ValueTask<ApiResult<ReceiptResponse>> GetReceiptAsync(Guid visitId, CancellationToken cancel = default) =>
        connection.GetAsync($"visits/{visitId}/receipt", ClientJsonContext.Default.ReceiptResponse, cancel);
}
