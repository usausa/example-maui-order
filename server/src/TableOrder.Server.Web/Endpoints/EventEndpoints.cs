namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Events;

public static class EventEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapEventEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Events);

        group.MapGet(string.Empty, HandleListAsync)
            .RequireAuthorization(Policies.AnyDevice)
            .WithName("EventList")
            .Produces<EventListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status410Gone);
    }

    //--------------------------------------------------------------------------------
    // List
    //--------------------------------------------------------------------------------

    // つなぎ直した端末が抜けた通知 (after は最後に受けた通し番号)
    private static async ValueTask<IResult> HandleListAsync(
        EventService eventService,
        long? after,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await eventService.GetEventsAsync(after, cancellationToken));
}
