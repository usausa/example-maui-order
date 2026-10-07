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
    }

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleGetAsync(
        StoreService storeService,
        CancellationToken cancellationToken) =>
        await storeService.GetStoreAsync(cancellationToken) is { } store ? TypedResults.Ok(store) : ApiProblems.NotFound();
}
