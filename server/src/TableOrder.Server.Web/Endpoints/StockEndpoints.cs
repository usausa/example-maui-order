namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Menu;

public static class StockEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapStockEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Stock);

        group.MapGet(string.Empty, HandleListAsync)
            .RequireAuthorization(Policies.MenuReader)
            .WithName("StockList")
            .Produces<StockListResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{targetId:guid}", HandleUpdateAsync)
            .RequireAuthorization(Policies.StockWriter)
            .WithName("StockUpdate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/reset", HandleResetAsync)
            .RequireAuthorization(Policies.HallDevice)
            .WithName("StockReset")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    //--------------------------------------------------------------------------------
    // List
    //--------------------------------------------------------------------------------

    private static async ValueTask<Ok<StockListResponse>> HandleListAsync(
        StockService stockService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await stockService.GetStockAsync(cancellationToken));

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleUpdateAsync(
        StockService stockService,
        Guid targetId,
        StockUpdateRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await stockService.UpdateAsync(targetId, request, cancellationToken));

    private static async ValueTask<NoContent> HandleResetAsync(
        StockService stockService,
        CancellationToken cancellationToken)
    {
        await stockService.ResetAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
