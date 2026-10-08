namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Bills;
using TableOrder.Contract.Visits;

public static class BillEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapBillEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Visits);

        group.MapGet("/{visitId:guid}/bill", HandleGetAsync)
            .RequireAuthorization(Policies.VisitReader)
            .WithName("BillGet")
            .Produces<BillResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{visitId:guid}/checkout", HandleCheckoutAsync)
            .RequireAuthorization(Policies.VisitReader)
            .WithName("BillCheckout")
            .Produces<VisitResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{visitId:guid}/checkout/cancel", HandleCancelAsync)
            .RequireAuthorization(Policies.VisitReader)
            .WithName("BillCheckoutCancel")
            .Produces<VisitResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{visitId:guid}/receipt", HandleReceiptAsync)
            .RequireAuthorization(Policies.TableDevice)
            .WithName("BillReceipt")
            .Produces<ReceiptResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    //--------------------------------------------------------------------------------
    // Bill
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleGetAsync(
        BillService billService,
        Guid visitId,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await billService.GetAsync(visitId, cancellationToken));

    //--------------------------------------------------------------------------------
    // Checkout
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleCheckoutAsync(
        BillService billService,
        Guid visitId,
        CheckoutRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await billService.StartCheckoutAsync(visitId, request, cancellationToken));

    private static async ValueTask<IResult> HandleCancelAsync(
        BillService billService,
        Guid visitId,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await billService.CancelCheckoutAsync(visitId, cancellationToken));

    //--------------------------------------------------------------------------------
    // Receipt
    //--------------------------------------------------------------------------------

    // 電子レシートの URL は、要求を受けた接続先で作る (電子レシートの画面もこのサーバが配る)
    private static async ValueTask<IResult> HandleReceiptAsync(
        HttpContext context,
        PaymentService paymentService,
        Guid visitId,
        CancellationToken cancellationToken)
    {
        var result = await paymentService.GetReceiptAsync(visitId, cancellationToken);
        if (!result.Succeeded)
        {
            return ApiProblems.From(result.Error);
        }

        var request = context.Request;
        return TypedResults.Ok(new ReceiptResponse
        {
            Url = new Uri($"{request.Scheme}://{request.Host}{ApiRoutes.Receipts}/{result.Value.Token}"),
            Total = result.Value.Total,
            IssuedAt = result.Value.IssuedAt
        });
    }
}
