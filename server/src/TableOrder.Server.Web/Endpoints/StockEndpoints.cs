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

        group.MapGet(string.Empty, HandleGetAsync)
            .RequireAuthorization(Policies.MenuReader)
            .WithName("StockGet")
            .Produces<StockResponse>();
    }

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    private static async ValueTask<Ok<StockResponse>> HandleGetAsync(
        StockService stockService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await stockService.GetStockAsync(cancellationToken));
}
