namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Payments;

public static class PaymentEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var visits = app.MapApiGroup(ApiRoutes.Visits)
            .RequireAuthorization(Policies.TableDevice);

        visits.MapPost("/{visitId:guid}/payments", HandleCreateAsync)
            .WithName("PaymentCreate")
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .Produces<PaymentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var payments = app.MapApiGroup(ApiRoutes.Payments);

        payments.MapGet("/{id:guid}", HandleGetAsync)
            .RequireAuthorization(Policies.TableDevice)
            .WithName("PaymentGet")
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        payments.MapPost("/{id:guid}/cancel", HandleCancelAsync)
            .RequireAuthorization(Policies.TableDevice)
            .WithName("PaymentCancel")
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        payments.MapPost("/{id:guid}/result", HandleResultAsync)
            .RequireAuthorization(Policies.TableDevice)
            .WithName("PaymentResult")
            .Produces<PaymentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // 決済サービスの結果の通知。仮の決済サービスは署名を持たないので、開発の環境だけで受ける
        if (app.Environment.IsDevelopment())
        {
            payments.MapPost("/callbacks/{provider}", HandleCallbackAsync)
                .AllowAnonymous()
                .WithName("PaymentCallback")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status404NotFound);
        }
    }

    //--------------------------------------------------------------------------------
    // Payment
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleCreateAsync(
        PaymentService paymentService,
        Guid visitId,
        PaymentCreateRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Created(await paymentService.CreateAsync(visitId, request, cancellationToken), static x => $"{ApiRoutes.Payments}/{x.Id}");

    private static async ValueTask<IResult> HandleGetAsync(
        PaymentService paymentService,
        Guid id,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await paymentService.GetAsync(id, cancellationToken));

    private static async ValueTask<IResult> HandleCancelAsync(
        PaymentService paymentService,
        Guid id,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await paymentService.CancelAsync(id, cancellationToken));

    //--------------------------------------------------------------------------------
    // Result
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleResultAsync(
        PaymentService paymentService,
        Guid id,
        PaymentResultRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await paymentService.ReportResultAsync(id, request, cancellationToken));

    private static async ValueTask<IResult> HandleCallbackAsync(
        PaymentService paymentService,
        string provider,
        PaymentCallbackRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await paymentService.ReceiveCallbackAsync(provider, request, cancellationToken));
}
