namespace TableOrder.Server.Core.Services;

using System.Security.Cryptography;

using TableOrder.Contract.Events;
using TableOrder.Contract.Payments;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

// 電子レシート (URL は入口が自分の接続先から作る)
public sealed record ReceiptResult(string Token, decimal Total, DateTimeOffset IssuedAt);

public sealed class PaymentService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly IPaymentProvider paymentProvider;

    private readonly DirectoryAccessor directoryAccessor;

    private readonly StoreAccessor storeAccessor;

    private readonly VisitAccessor visitAccessor;

    private readonly OrderAccessor orderAccessor;

    private readonly PaymentAccessor paymentAccessor;

    private readonly VisitService visitService;

    private readonly EventService eventService;

    public PaymentService(
        ServiceContextProvider contextProvider,
        IPaymentProvider paymentProvider,
        DirectoryAccessor directoryAccessor,
        StoreAccessor storeAccessor,
        VisitAccessor visitAccessor,
        OrderAccessor orderAccessor,
        PaymentAccessor paymentAccessor,
        VisitService visitService,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.paymentProvider = paymentProvider;
        this.directoryAccessor = directoryAccessor;
        this.storeAccessor = storeAccessor;
        this.visitAccessor = visitAccessor;
        this.orderAccessor = orderAccessor;
        this.paymentAccessor = paymentAccessor;
        this.visitService = visitService;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // Create
    //--------------------------------------------------------------------------------

    // 支払を始める (会計を始めた来店だけ)。額は残りから、期限内の待っている支払を引いた額まで
    // 同じ Id の送り直しは、同じ支払方法と額なら始めた支払を返す (201 ではなく 200)
    public async ValueTask<ServiceResult<PaymentResponse>> CreateAsync(Guid visitId, PaymentCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return new(ServiceError.Validation("id", "支払の Id を送ってください"));
        }

        if (!Enum.IsDefined(request.Method))
        {
            return new(ServiceError.Validation("method", "QrCode か CreditCard を送ってください"));
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var now = context.Now;
        return await eventService.WriteAsync<ServiceResult<PaymentResponse>>(tenantId, storeId, async transaction =>
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

            var existing = await paymentAccessor.QueryAsync(tx, tenantId, storeId, request.Id, cancellationToken);
            if (existing is not null)
            {
                return (existing.VisitId == visitId) && (existing.Method == request.Method) && (existing.Amount == request.Amount)
                    ? new(ToResponse(existing))
                    : new(new ServiceError(ErrorCodes.DuplicateIdMismatch));
            }

            if (visit.Status != VisitStatus.Paying)
            {
                return new(new ServiceError(ErrorCodes.VisitNotOpen));
            }

            var store = (await storeAccessor.QueryAsync(tx, tenantId, storeId, cancellationToken))!;
            var methods = JsonSerializer.Deserialize<List<PaymentMethod>>(store.PaymentMethods, JsonDefaults.Options) ?? [];
            if (!methods.Contains(request.Method))
            {
                return new(new ServiceError(ErrorCodes.PaymentMethodUnavailable));
            }

            var lines = await orderAccessor.QueryLineListAsync(tx, tenantId, visitId, cancellationToken);
            var options = await orderAccessor.QueryLineOptionListAsync(tx, tenantId, visitId, cancellationToken);
            var payments = await paymentAccessor.QueryListAsync(tx, tenantId, visitId, cancellationToken);
            var bill = BillCalculator.Calculate(visit, store, lines, options, payments);
            var pending = payments.Where(x => (x.Status == PaymentStatus.Pending) && ((x.ExpiresAt is null) || (x.ExpiresAt > now))).Sum(static x => x.Amount);
            if ((request.Amount <= 0) || (request.Amount > bill.Balance - pending))
            {
                return new(new ServiceError(ErrorCodes.PaymentAmountInvalid));
            }

            var start = paymentProvider.Start(request.Id, request.Method, request.Amount, now);
            await paymentAccessor.InsertAsync(tx, tenantId, request.Id, storeId, visitId, request.Method, request.Amount, start.QrCode, start.ExpiresAt, paymentProvider.Name, start.Reference, context.DeviceId, now, cancellationToken);
            var response = ToResponse((await paymentAccessor.QueryAsync(tx, tenantId, storeId, request.Id, cancellationToken))!);
            await transaction.AppendEventAsync(EventTypes.PaymentUpdated, response, [visit.TableId], null, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(response, created: true);
        }, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    // 支払の状態 (通知を受け損ねたとき)
    public async ValueTask<ServiceResult<PaymentResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var payment = await paymentAccessor.QueryAsync(tenantId, storeId, id, cancellationToken);
        if (payment is null)
        {
            return new(ServiceError.NotFound);
        }

        var visit = await visitAccessor.QueryAsync(tenantId, storeId, payment.VisitId, cancellationToken);
        return VisitService.InScope(context, visit!) ? new(ToResponse(payment)) : new(ServiceError.DeviceScope);
    }

    //--------------------------------------------------------------------------------
    // Result
    //--------------------------------------------------------------------------------

    // 待っている支払をやめる (待っている支払のほかは変えずに返す)
    public ValueTask<ServiceResult<PaymentResponse>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return eventService.WriteAsync<ServiceResult<PaymentResponse>>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var payment = await paymentAccessor.QueryAsync(tx, tenantId, storeId, id, cancellationToken);
            if (payment is null)
            {
                return new(ServiceError.NotFound);
            }

            var visit = (await visitAccessor.QueryAsync(tx, tenantId, storeId, payment.VisitId, cancellationToken))!;
            if (!VisitService.InScope(context, visit))
            {
                return new(ServiceError.DeviceScope);
            }

            if (await paymentAccessor.UpdateCancelledAsync(tx, tenantId, id, context.Now, cancellationToken) == 0)
            {
                return new(ToResponse(payment));
            }

            var response = ToResponse((await paymentAccessor.QueryAsync(tx, tenantId, storeId, id, cancellationToken))!);
            await transaction.AppendEventAsync(EventTypes.PaymentUpdated, response, [visit.TableId], null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(response);
        }, cancellationToken);
    }

    // テーブルの決済端末で払った結果 (クレジットカード。QR コード決済の結果は決済サービスから受ける)
    public ValueTask<ServiceResult<PaymentResponse>> ReportResultAsync(Guid id, PaymentResultRequest request, CancellationToken cancellationToken)
    {
        if (ValidateResult(request.Status, request.Provider, request.ProviderReference, request.FailureReason) is { } invalid)
        {
            return ValueTask.FromResult(new ServiceResult<PaymentResponse>(invalid));
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return eventService.WriteAsync<ServiceResult<PaymentResponse>>(tenantId, storeId, async transaction =>
        {
            var payment = await paymentAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken);
            if (payment is null)
            {
                return new(ServiceError.NotFound);
            }

            var visit = (await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, payment.VisitId, cancellationToken))!;
            if (!VisitService.InScope(context, visit))
            {
                return new(ServiceError.DeviceScope);
            }

            if (payment.Method != PaymentMethod.CreditCard)
            {
                return new(ServiceError.Validation("status", "QR コード決済の結果は決済サービスから受けます"));
            }

            var applied = await ApplyAsync(transaction, payment, request.Status, request.Provider, request.ProviderReference, request.FailureReason, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(ToResponse(applied));
        }, cancellationToken);
    }

    // 決済サービスからの結果の通知。テナントのわからない要求なので、取引番号で引いた支払のテナントと店舗で書く
    public async ValueTask<ServiceError?> ReceiveCallbackAsync(string provider, PaymentCallbackRequest request, CancellationToken cancellationToken)
    {
        if (provider != paymentProvider.Name)
        {
            return ServiceError.NotFound;
        }

        if (String.IsNullOrEmpty(request.ProviderReference) || (ValidateResult(request.Status, null, request.ProviderReference, request.FailureReason) is not null))
        {
            return ServiceError.Validation("status", "取引番号と結果 (Completed か Failed) を送ってください");
        }

        var found = await directoryAccessor.QueryPaymentByProviderReferenceAsync(provider, request.ProviderReference, cancellationToken);
        if (found is null)
        {
            return ServiceError.NotFound;
        }

        var now = contextProvider.Current.Now;
        return await eventService.WriteAsync<ServiceError?>(found.TenantId, found.StoreId, async transaction =>
        {
            var payment = (await paymentAccessor.QueryAsync(transaction.Tx, found.TenantId, found.StoreId, found.Id, cancellationToken))!;
            await ApplyAsync(transaction, payment, request.Status, null, null, request.FailureReason, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }, cancellationToken);
    }

    // 待っている支払を払い終えたことにする (開発の環境で、決済サービスの代わりに時間で進める)
    public async ValueTask CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        await eventService.WriteAsync(tenantId, storeId, async transaction =>
        {
            var payment = await paymentAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken);
            if (payment is null)
            {
                return false;
            }

            await ApplyAsync(transaction, payment, PaymentStatus.Completed, null, null, null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Receipt
    //--------------------------------------------------------------------------------

    // 電子レシート (支払が揃って来店を閉じたときに作る。電子レシートを出さない店はない)
    public async ValueTask<ServiceResult<ReceiptResult>> GetReceiptAsync(Guid visitId, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var visit = await visitAccessor.QueryAsync(tenantId, context.RequireStoreId(), visitId, cancellationToken);
        if (visit is null)
        {
            return new(ServiceError.NotFound);
        }

        if (!VisitService.InScope(context, visit))
        {
            return new(ServiceError.DeviceScope);
        }

        var receipt = await paymentAccessor.QueryReceiptAsync(tenantId, visitId, cancellationToken);
        return receipt is null
            ? new(ServiceError.NotFound)
            : new(new ReceiptResult(receipt.Token, visit.OrderTotal, receipt.IssuedAt));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // 結果を記録する (待っている支払だけ。結果は 1 回だけ受ける)。払い終えて残りがなくなったら来店を閉じる
    private async ValueTask<PaymentEntity> ApplyAsync(StoreTransaction transaction, PaymentEntity payment, PaymentStatus status, string? provider, string? reference, string? reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (payment.Status != PaymentStatus.Pending)
        {
            return payment;
        }

        var tx = transaction.Tx;
        var tenantId = transaction.TenantId;
        var storeId = transaction.StoreId;
        if (status == PaymentStatus.Completed)
        {
            await paymentAccessor.UpdateCompletedAsync(tx, tenantId, payment.Id, provider, reference, now, cancellationToken);
        }
        else
        {
            await paymentAccessor.UpdateFailedAsync(tx, tenantId, payment.Id, provider, reference, reason, now, cancellationToken);
        }

        var updated = (await paymentAccessor.QueryAsync(tx, tenantId, storeId, payment.Id, cancellationToken))!;
        var visit = (await visitAccessor.QueryAsync(tx, tenantId, storeId, payment.VisitId, cancellationToken))!;
        await transaction.AppendEventAsync(EventTypes.PaymentUpdated, ToResponse(updated), [visit.TableId], null, now, cancellationToken);
        if ((status != PaymentStatus.Completed) || (visit.Status != VisitStatus.Paying))
        {
            return updated;
        }

        var store = (await storeAccessor.QueryAsync(tx, tenantId, storeId, cancellationToken))!;
        var lines = await orderAccessor.QueryLineListAsync(tx, tenantId, visit.Id, cancellationToken);
        var options = await orderAccessor.QueryLineOptionListAsync(tx, tenantId, visit.Id, cancellationToken);
        var payments = await paymentAccessor.QueryListAsync(tx, tenantId, visit.Id, cancellationToken);
        if (BillCalculator.Calculate(visit, store, lines, options, payments).Balance > 0)
        {
            return updated;
        }

        await visitAccessor.UpdateClosedAsync(tx, tenantId, visit.Id, VisitClosedBy.TablePayment, null, visit.Version, now, cancellationToken);
        if (store.ElectronicReceipt)
        {
            await paymentAccessor.InsertReceiptAsync(tx, tenantId, visit.Id, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), now, cancellationToken);
        }

        var closed = await visitService.ToResponseAsync((await visitAccessor.QueryAsync(tx, tenantId, storeId, visit.Id, cancellationToken))!, cancellationToken);
        await transaction.AppendEventAsync(EventTypes.VisitClosed, closed, [visit.TableId], null, now, cancellationToken);
        return updated;
    }

    private static ServiceError? ValidateResult(PaymentStatus status, string? provider, string? reference, string? reason)
    {
        if (status is not (PaymentStatus.Completed or PaymentStatus.Failed))
        {
            return ServiceError.Validation("status", "Completed か Failed を送ってください");
        }

        if ((provider?.Length > Length.PaymentReference) || (reference?.Length > Length.PaymentReference) || (reason?.Length > Length.PaymentFailureReason))
        {
            return ServiceError.Validation("providerReference", $"決済サービスと取引番号を {Length.PaymentReference} 文字まで、理由を {Length.PaymentFailureReason} 文字までで送ってください");
        }

        return null;
    }

    internal static PaymentResponse ToResponse(PaymentEntity payment) =>
        new()
        {
            Id = payment.Id,
            Method = payment.Method,
            Amount = payment.Amount,
            Status = payment.Status,
            QrCode = payment.QrCode,
            ExpiresAt = payment.ExpiresAt,
            CompletedAt = payment.CompletedAt
        };
}
