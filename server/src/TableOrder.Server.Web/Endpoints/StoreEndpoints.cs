namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Stores;

public static class StoreEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapStoreEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Store);

        group.MapGet(string.Empty, HandleGetAsync)
            .RequireAuthorization(Policies.AnyDevice)
            .WithName("StoreGet")
            .Produces<StoreResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/ordering", HandleOrderingAsync)
            .RequireAuthorization(Policies.HallDevice)
            .WithName("StoreOrdering")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleGetAsync(
        StoreService storeService,
        CancellationToken cancellationToken) =>
        await storeService.GetStoreAsync(cancellationToken) is { } store ? TypedResults.Ok(store) : ApiProblems.NotFound();

    //--------------------------------------------------------------------------------
    // Ordering
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleOrderingAsync(
        StoreService storeService,
        StoreOrderingRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await storeService.SetOrderingAsync(request, cancellationToken));
}
