namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Serving;

public static class ServingEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapServingEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Serving)
            .RequireAuthorization(Policies.HallDevice);

        group.MapGet(string.Empty, HandleListAsync)
            .WithName("ServingList")
            .Produces<ServingListResponse>()
            .ProducesValidationProblem();

        group.MapPost("/serve", HandleServeAsync)
            .WithName("ServingServe")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    //--------------------------------------------------------------------------------
    // List
    //--------------------------------------------------------------------------------

    // status (Ordered / Cooking / Ready) は文字のまま渡す (読めない値も Problem Details の 400 にする)
    private static async ValueTask<IResult> HandleListAsync(
        ServingService servingService,
        string? status,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await servingService.GetListAsync(status, cancellationToken));

    //--------------------------------------------------------------------------------
    // Serve
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleServeAsync(
        ServingService servingService,
        ServeRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await servingService.ServeAsync(request, cancellationToken));
}
