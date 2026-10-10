namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Bills;
using TableOrder.Contract.Events;
using TableOrder.Contract.Visits;
using TableOrder.Server.Core.Accessors;

public sealed class BillService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly StoreAccessor storeAccessor;

    private readonly VisitAccessor visitAccessor;

    private readonly OrderAccessor orderAccessor;

    private readonly PaymentAccessor paymentAccessor;

    private readonly VisitService visitService;

    private readonly OrderService orderService;

    private readonly EventService eventService;

    public BillService(
        ServiceContextProvider contextProvider,
        StoreAccessor storeAccessor,
        VisitAccessor visitAccessor,
        OrderAccessor orderAccessor,
        PaymentAccessor paymentAccessor,
        VisitService visitService,
        OrderService orderService,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.storeAccessor = storeAccessor;
        this.visitAccessor = visitAccessor;
        this.orderAccessor = orderAccessor;
        this.paymentAccessor = paymentAccessor;
        this.visitService = visitService;
        this.orderService = orderService;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // Bill
    //--------------------------------------------------------------------------------

    // 会計の明細と合計 (同じ商品とオプションの明細はまとめ、取消を除く)
    public async ValueTask<ServiceResult<BillResponse>> GetAsync(Guid visitId, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var visit = await visitAccessor.QueryAsync(tenantId, storeId, visitId, cancellationToken);
        if (visit is null)
        {
            return new(ServiceError.NotFound);
        }

        if (!VisitService.InScope(context, visit))
        {
            return new(ServiceError.DeviceScope);
        }

        var store = await storeAccessor.QueryAsync(tenantId, storeId, cancellationToken);
        var lines = await orderAccessor.QueryLineListAsync(tenantId, visitId, cancellationToken);
        var options = await orderAccessor.QueryLineOptionListAsync(tenantId, visitId, cancellationToken);
        var payments = await paymentAccessor.QueryListAsync(tenantId, visitId, cancellationToken);
        return new(BillCalculator.Calculate(visit, store!, lines, options, payments));
    }

    //--------------------------------------------------------------------------------
    // Checkout
    //--------------------------------------------------------------------------------

    // 会計を始める (来店を Paying にして注文を止める)。表示していた明細と版で確かめ、会計中なら変えずに返す
    // お願いしていない食後の品は、ここでキッチンにお願いする (払い終えると来店は閉じ、あとからお願いできない。払った品は作る)
    public async ValueTask<ServiceResult<VisitResponse>> StartCheckoutAsync(Guid visitId, CheckoutRequest request, CancellationToken cancellationToken)
    {
        if (String.IsNullOrEmpty(request.BillVersion))
        {
            return new(ServiceError.Validation("billVersion", "表示していた会計の明細の版を送ってください"));
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return await eventService.WriteAsync<ServiceResult<VisitResponse>>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var visit = await visitAccessor.QueryAsync(tx, tenantId, storeId, visitId, cancellationToken);
            if (visit is null)
            {
                return new(ServiceError.NotFound);
            }

            if (!VisitService.InScope(context, visit))
            {
                return new(ServiceError.DeviceScope);
            }

            if (visit.Status == VisitStatus.Paying)
            {
                return new(await visitService.ToResponseAsync(tx, visit, cancellationToken));
            }

            if (visit.Status != VisitStatus.Open)
            {
                return new(new ServiceError(ErrorCodes.VisitNotOpen));
            }

            if (visit.Version != request.Version)
            {
                return new(new ServiceError(ErrorCodes.VersionMismatch));
            }

            var store = await storeAccessor.QueryAsync(tx, tenantId, storeId, cancellationToken);
            var lines = await orderAccessor.QueryLineListAsync(tx, tenantId, visitId, cancellationToken);
            var options = await orderAccessor.QueryLineOptionListAsync(tx, tenantId, visitId, cancellationToken);
            var payments = await paymentAccessor.QueryListAsync(tx, tenantId, visitId, cancellationToken);
            if (BillCalculator.Calculate(visit, store!, lines, options, payments).BillVersion != request.BillVersion)
            {
                return new(new ServiceError(ErrorCodes.BillChanged));
            }

            if (await visitAccessor.UpdatePayingAsync(tx, tenantId, visitId, request.Version, context.Now, cancellationToken) == 0)
            {
                return new(new ServiceError(ErrorCodes.VersionMismatch));
            }

            if (await orderService.ReleaseHeldAsync(transaction, visit, [], context.Now, cancellationToken) is { } error)
            {
                return new(error);
            }

            var response = await visitService.LoadResponseAsync(tx, tenantId, storeId, visitId, cancellationToken);
            await transaction.AppendEventAsync(EventTypes.VisitUpdated, response, [visit.TableId], null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(response);
        }, cancellationToken);
    }

    // 会計をやめる。待っている支払はやめ、注文できる状態に戻す (送り直しても同じ結果になるので版は確かめない)
    // 払い終えた支払があれば戻さずに会計中のまま返し、会計の前の来店は変えずに返す
    public ValueTask<ServiceResult<VisitResponse>> CancelCheckoutAsync(Guid visitId, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return eventService.WriteAsync<ServiceResult<VisitResponse>>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var visit = await visitAccessor.QueryAsync(tx, tenantId, storeId, visitId, cancellationToken);
            if (visit is null)
            {
                return new(ServiceError.NotFound);
            }

            if (!VisitService.InScope(context, visit))
            {
                return new(ServiceError.DeviceScope);
            }

            if (visit.Status == VisitStatus.Open)
            {
                return new(await visitService.ToResponseAsync(tx, visit, cancellationToken));
            }

            if (visit.Status != VisitStatus.Paying)
            {
                return new(new ServiceError(ErrorCodes.VisitNotOpen));
            }

            var payments = await paymentAccessor.QueryListAsync(tx, tenantId, visitId, cancellationToken);
            if (payments.Any(static x => x.Status == PaymentStatus.Completed))
            {
                return new(await visitService.ToResponseAsync(tx, visit, cancellationToken));
            }

            await PaymentService.CancelPendingAsync(paymentAccessor, transaction, payments, visit.TableId, context.Now, cancellationToken);

            if (await visitAccessor.UpdateReopenedAsync(tx, tenantId, visitId, context.Now, cancellationToken) == 0)
            {
                return new(new ServiceError(ErrorCodes.VersionMismatch));
            }

            var response = await visitService.LoadResponseAsync(tx, tenantId, storeId, visitId, cancellationToken);
            await transaction.AppendEventAsync(EventTypes.VisitUpdated, response, [visit.TableId], null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(response);
        }, cancellationToken);
    }
}
