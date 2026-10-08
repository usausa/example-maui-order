namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Kitchen;

public static class KitchenEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapKitchenEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Kitchen)
            .RequireAuthorization(Policies.KitchenDevice);

        group.MapGet("/tickets", HandleListAsync)
            .WithName("KitchenTicketList")
            .Produces<KitchenTicketListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/tickets/{id:guid}/lines/{lineId:guid}/start", HandleStartAsync)
            .WithName("KitchenLineStart")
            .Produces<KitchenTicketListResponseItem>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/tickets/{id:guid}/lines/{lineId:guid}/ready", HandleReadyAsync)
            .WithName("KitchenLineReady")
            .Produces<KitchenTicketListResponseItem>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/tickets/{id:guid}/bump", HandleBumpAsync)
            .WithName("KitchenTicketBump")
            .Produces<KitchenTicketListResponseItem>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/tickets/{id:guid}/recall", HandleRecallAsync)
            .WithName("KitchenTicketRecall")
            .Produces<KitchenTicketListResponseItem>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    //--------------------------------------------------------------------------------
    // List
    //--------------------------------------------------------------------------------

    // status (Open / Done) は文字のまま渡す (読めない値も Problem Details の 400 にする)
    private static async ValueTask<IResult> HandleListAsync(
        KitchenService kitchenService,
        Guid? stationId,
        string? status,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await kitchenService.GetTicketsAsync(stationId, status, cancellationToken));

    //--------------------------------------------------------------------------------
    // Line
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleStartAsync(
        KitchenService kitchenService,
        Guid id,
        Guid lineId,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await kitchenService.StartAsync(id, lineId, cancellationToken));

    private static async ValueTask<IResult> HandleReadyAsync(
        KitchenService kitchenService,
        Guid id,
        Guid lineId,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await kitchenService.ReadyAsync(id, lineId, cancellationToken));

    //--------------------------------------------------------------------------------
    // Ticket
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleBumpAsync(
        KitchenService kitchenService,
        Guid id,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await kitchenService.BumpAsync(id, cancellationToken));

    private static async ValueTask<IResult> HandleRecallAsync(
        KitchenService kitchenService,
        Guid id,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await kitchenService.RecallAsync(id, cancellationToken));
}
