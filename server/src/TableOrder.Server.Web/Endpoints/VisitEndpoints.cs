namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Visits;

public static class VisitEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapVisitEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Visits);

        group.MapPost(string.Empty, HandleCreateAsync)
            .RequireAuthorization(Policies.VisitOpener)
            .WithName("VisitCreate")
            .Produces<VisitResponse>(StatusCodes.Status201Created)
            .Produces<VisitResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", HandleGetAsync)
            .RequireAuthorization(Policies.VisitReader)
            .WithName("VisitGet")
            .Produces<VisitResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", HandleUpdateAsync)
            .RequireAuthorization(Policies.HallDevice)
            .WithName("VisitUpdate")
            .Produces<VisitResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/move", HandleMoveAsync)
            .RequireAuthorization(Policies.HallDevice)
            .WithName("VisitMove")
            .Produces<VisitResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/confirmations", HandleConfirmAsync)
            .RequireAuthorization(Policies.VisitReader)
            .WithName("VisitConfirm")
            .Produces<VisitResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/close", HandleCloseAsync)
            .RequireAuthorization(Policies.HallDevice)
            .WithName("VisitClose")
            .Produces<VisitResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/cancel", HandleCancelAsync)
            .RequireAuthorization(Policies.HallDevice)
            .WithName("VisitCancel")
            .Produces<VisitResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    //--------------------------------------------------------------------------------
    // Open
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleCreateAsync(
        VisitService visitService,
        VisitCreateRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Created(await visitService.CreateAsync(request, cancellationToken), static x => $"{ApiRoutes.Visits}/{x.Id}");

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleGetAsync(
        VisitService visitService,
        Guid id,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await visitService.GetAsync(id, cancellationToken));

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleUpdateAsync(
        VisitService visitService,
        Guid id,
        VisitUpdateRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await visitService.UpdateAsync(id, request, cancellationToken));

    private static async ValueTask<IResult> HandleMoveAsync(
        VisitService visitService,
        Guid id,
        VisitMoveRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await visitService.MoveAsync(id, request, cancellationToken));

    private static async ValueTask<IResult> HandleConfirmAsync(
        VisitService visitService,
        Guid id,
        VisitConfirmationRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await visitService.ConfirmAsync(id, request, cancellationToken));

    //--------------------------------------------------------------------------------
    // Close
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleCloseAsync(
        VisitService visitService,
        Guid id,
        VisitCloseRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await visitService.CloseAsync(id, request, cancellationToken));

    private static async ValueTask<IResult> HandleCancelAsync(
        VisitService visitService,
        Guid id,
        VisitCancelRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await visitService.CancelAsync(id, request, cancellationToken));
}
