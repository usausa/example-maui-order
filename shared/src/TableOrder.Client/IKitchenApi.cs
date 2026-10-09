namespace TableOrder.Client;

// キッチン端末 (持ち場に置く KDS) が使う注文サーバの API。端末の登録と設定は IDeviceApi、通知は IOrderEvents に置く
// 実装は通信の方式 (REST、gRPC) ごとに作る
public interface IKitchenApi
{
    //--------------------------------------------------------------------------------
    // Menu
    //--------------------------------------------------------------------------------

    // GET /menu (持ち場の名前と、品切れにする品)
    ValueTask<ApiResult<MenuResponse>> GetMenuAsync(CancellationToken cancel = default);

    // GET /stock
    ValueTask<ApiResult<StockResponse>> GetStockAsync(CancellationToken cancel = default);

    // PUT /stock/{targetId} (売り切れと残りの数)
    ValueTask<ApiResult<NoContent>> UpdateStockAsync(Guid targetId, StockUpdateRequest request, CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    // GET /store (設定の版とタイムゾーン)
    ValueTask<ApiResult<StoreResponse>> GetStoreAsync(CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Ticket
    //--------------------------------------------------------------------------------

    // GET /kitchen/tickets (持ち場を省くと受け持つすべての持ち場、状態を省くとまだ下げていないチケット)
    ValueTask<ApiResult<KitchenTicketListResponse>> GetTicketsAsync(Guid? stationId = null, KitchenTicketStatus? status = null, CancellationToken cancel = default);

    // POST /kitchen/tickets/{ticketId}/lines/{lineId}/start (作り始め)
    ValueTask<ApiResult<KitchenTicketListResponseItem>> StartLineAsync(Guid ticketId, Guid lineId, CancellationToken cancel = default);

    // POST /kitchen/tickets/{ticketId}/lines/{lineId}/ready (できあがり)
    ValueTask<ApiResult<KitchenTicketListResponseItem>> ReadyLineAsync(Guid ticketId, Guid lineId, CancellationToken cancel = default);

    // POST /kitchen/tickets/{ticketId}/bump (残りの明細をできあがりにして下げる)
    ValueTask<ApiResult<KitchenTicketListResponseItem>> BumpAsync(Guid ticketId, CancellationToken cancel = default);

    // POST /kitchen/tickets/{ticketId}/recall (下げたチケットを戻す)
    ValueTask<ApiResult<KitchenTicketListResponseItem>> RecallAsync(Guid ticketId, CancellationToken cancel = default);
}
