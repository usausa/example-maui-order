namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Calls;

public static class CallEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapCallEndpoints(this WebApplication app)
    {
        var visits = app.MapApiGroup(ApiRoutes.Visits)
            .RequireAuthorization(Policies.TableDevice);

        visits.MapPost("/{visitId:guid}/calls", HandleCreateAsync)
            .WithName("CallCreate")
            .Produces<CallListResponseItem>(StatusCodes.Status201Created)
            .Produces<CallListResponseItem>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        visits.MapGet("/{visitId:guid}/calls", HandleVisitListAsync)
            .WithName("CallVisitList")
            .Produces<CallListResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var calls = app.MapApiGroup(ApiRoutes.Calls)
            .RequireAuthorization(Policies.HallDevice);

        calls.MapGet(string.Empty, HandleStoreListAsync)
            .WithName("CallList")
            .Produces<CallListResponse>()
            .ProducesValidationProblem();

        calls.MapPost("/{id:guid}/acknowledge", HandleAcknowledgeAsync)
            .WithName("CallAcknowledge")
            .Produces<CallListResponseItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        calls.MapPost("/{id:guid}/done", HandleDoneAsync)
            .WithName("CallDone")
            .Produces<CallListResponseItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleCreateAsync(
        CallService callService,
        Guid visitId,
        CallCreateRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Created(await callService.CreateAsync(visitId, request, cancellationToken), static x => $"{ApiRoutes.Calls}/{x.Id}");

    private static async ValueTask<IResult> HandleVisitListAsync(
        CallService callService,
        Guid visitId,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await callService.GetListAsync(visitId, cancellationToken));

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    // status (Open / Acknowledged / Done) は文字のまま渡す (読めない値も Problem Details の 400 にする)
    private static async ValueTask<IResult> HandleStoreListAsync(
        CallService callService,
        string? status,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await callService.GetStoreListAsync(status, cancellationToken));

    private static async ValueTask<IResult> HandleAcknowledgeAsync(
        CallService callService,
        Guid id,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await callService.AcknowledgeAsync(id, cancellationToken));

    private static async ValueTask<IResult> HandleDoneAsync(
        CallService callService,
        Guid id,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await callService.DoneAsync(id, cancellationToken));
}
