namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Orders;

public static class OrderEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapOrderEndpoints(this WebApplication app)
    {
        var visits = app.MapApiGroup(ApiRoutes.Visits);

        visits.MapPost("/{visitId:guid}/orders", HandleCreateAsync)
            .RequireAuthorization(Policies.VisitReader)
            .WithName("OrderCreate")
            .Produces<OrderListResponseItem>(StatusCodes.Status201Created)
            .Produces<OrderListResponseItem>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        visits.MapGet("/{visitId:guid}/orders", HandleListAsync)
            .RequireAuthorization(Policies.VisitReader)
            .WithName("OrderList")
            .Produces<OrderListResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        visits.MapPost("/{visitId:guid}/orders/release", HandleReleaseAsync)
            .RequireAuthorization(Policies.VisitReader)
            .WithName("OrderRelease")
            .Produces<OrderListResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var orders = app.MapApiGroup(ApiRoutes.Orders);

        orders.MapPost("/{orderId:guid}/lines/{lineId:guid}/cancel", HandleCancelLineAsync)
            .RequireAuthorization(Policies.HallDevice)
            .WithName("OrderLineCancel")
            .Produces<OrderListResponseItem>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleCreateAsync(
        OrderService orderService,
        Guid visitId,
        OrderCreateRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Created(await orderService.CreateAsync(visitId, request, cancellationToken), x => $"{ApiRoutes.Visits}/{visitId}/orders/{x.Id}");

    private static async ValueTask<IResult> HandleListAsync(
        OrderService orderService,
        Guid visitId,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await orderService.GetListAsync(visitId, cancellationToken));

    private static async ValueTask<IResult> HandleReleaseAsync(
        OrderService orderService,
        Guid visitId,
        OrderReleaseRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await orderService.ReleaseAsync(visitId, request, cancellationToken));

    //--------------------------------------------------------------------------------
    // Cancel
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleCancelLineAsync(
        OrderService orderService,
        Guid orderId,
        Guid lineId,
        OrderLineCancelRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await orderService.CancelLineAsync(orderId, lineId, request, cancellationToken));
}
