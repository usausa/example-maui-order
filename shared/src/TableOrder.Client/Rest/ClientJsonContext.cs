namespace TableOrder.Client.Rest;

using System.Text.Json.Serialization;

using TableOrder.Contract.Events;

// 注文サーバとやり取りする JSON。サーバと同じ形 (camelCase、null は省く、列挙型は名前) にし、リフレクションを使わずに読み書きする (端末のトリミングに残す)
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(DevicePairRequest))]
[JsonSerializable(typeof(DevicePairResponse))]
[JsonSerializable(typeof(DeviceTokenRequest))]
[JsonSerializable(typeof(DeviceTokenResponse))]
[JsonSerializable(typeof(DeviceHeartbeatRequest))]
[JsonSerializable(typeof(DeviceConfigResponse))]
[JsonSerializable(typeof(StoreResponse))]
[JsonSerializable(typeof(StoreOrderingRequest))]
[JsonSerializable(typeof(TableListResponse))]
[JsonSerializable(typeof(MenuResponse))]
[JsonSerializable(typeof(StockResponse))]
[JsonSerializable(typeof(StockUpdateRequest))]
[JsonSerializable(typeof(VisitCreateRequest))]
[JsonSerializable(typeof(VisitUpdateRequest))]
[JsonSerializable(typeof(VisitMoveRequest))]
[JsonSerializable(typeof(VisitCloseRequest))]
[JsonSerializable(typeof(VisitCancelRequest))]
[JsonSerializable(typeof(VisitConfirmationRequest))]
[JsonSerializable(typeof(VisitResponse))]
[JsonSerializable(typeof(OrderCreateRequest))]
[JsonSerializable(typeof(OrderListResponseItem))]
[JsonSerializable(typeof(OrderListResponse))]
[JsonSerializable(typeof(OrderReleaseRequest))]
[JsonSerializable(typeof(OrderLineCancelRequest))]
[JsonSerializable(typeof(ServingListResponse))]
[JsonSerializable(typeof(ServeRequest))]
[JsonSerializable(typeof(CallCreateRequest))]
[JsonSerializable(typeof(CallListResponseItem))]
[JsonSerializable(typeof(CallListResponse))]
[JsonSerializable(typeof(KitchenTicketListResponse))]
[JsonSerializable(typeof(KitchenTicketListResponseItem))]
[JsonSerializable(typeof(BillResponse))]
[JsonSerializable(typeof(CheckoutRequest))]
[JsonSerializable(typeof(PaymentCreateRequest))]
[JsonSerializable(typeof(PaymentResponse))]
[JsonSerializable(typeof(ReceiptResponse))]
[JsonSerializable(typeof(EventListResponse))]
[JsonSerializable(typeof(EventListResponseItem))]
[JsonSerializable(typeof(VisitMovedEventData))]
[JsonSerializable(typeof(StockUpdatedEventData))]
[JsonSerializable(typeof(OrderLinesUpdatedEventData))]
[JsonSerializable(typeof(DeviceUpdatedEventData))]
[JsonSerializable(typeof(ProblemResponse))]
[JsonSerializable(typeof(long))]
internal sealed partial class ClientJsonContext : JsonSerializerContext;

// 失敗の応答 (Problem Details) のうち、端末が使う項目
internal sealed class ProblemResponse
{
    public string? Title { get; set; }

    public string? ErrorCode { get; set; }
}
