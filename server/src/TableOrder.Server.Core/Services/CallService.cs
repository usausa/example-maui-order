namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Calls;
using TableOrder.Contract.Events;
using TableOrder.Server.Core.Accessors;

public sealed class CallService
{
    // 終わった呼び出しを返す数
    private const int DoneLimit = 50;

    private readonly ServiceContextProvider contextProvider;

    private readonly StoreAccessor storeAccessor;

    private readonly VisitAccessor visitAccessor;

    private readonly CallAccessor callAccessor;

    private readonly EventService eventService;

    public CallService(
        ServiceContextProvider contextProvider,
        StoreAccessor storeAccessor,
        VisitAccessor visitAccessor,
        CallAccessor callAccessor,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.storeAccessor = storeAccessor;
        this.visitAccessor = visitAccessor;
        this.callAccessor = callAccessor;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // Create
    //--------------------------------------------------------------------------------

    // 店員の呼び出し。同じ用件の終わっていない呼び出しがあれば、増やさずにそれを返す (201 ではなく 200)
    public async ValueTask<ServiceResult<CallListResponseItem>> CreateAsync(Guid visitId, CallCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return new(ServiceError.Validation("id", "呼び出しの Id を送ってください"));
        }

        if (String.IsNullOrEmpty(request.ReasonCode) || (request.ReasonCode.Length > Length.CallReasonCode))
        {
            return new(ServiceError.Validation("reasonCode", $"呼び出しの用件を {Length.CallReasonCode} 文字までで送ってください"));
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var reasons = await storeAccessor.QueryCallReasonListAsync(tenantId, storeId, cancellationToken);
        if (reasons.All(x => x.Code != request.ReasonCode))
        {
            return new(ServiceError.Validation("reasonCode", "店舗の呼び出しの用件を送ってください"));
        }

        return await eventService.WriteAsync<ServiceResult<CallListResponseItem>>(tenantId, storeId, async transaction =>
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

            var existing = await callAccessor.QueryAsync(tx, tenantId, storeId, request.Id, cancellationToken);
            if (existing is not null)
            {
                return (existing.VisitId == visitId) && (existing.ReasonCode == request.ReasonCode)
                    ? new(ToItem(existing))
                    : new(new ServiceError(ErrorCodes.DuplicateIdMismatch));
            }

            if (visit.Status is not (VisitStatus.Open or VisitStatus.Paying))
            {
                return new(new ServiceError(ErrorCodes.VisitNotOpen));
            }

            if (await callAccessor.QueryOpenByReasonAsync(tx, tenantId, visitId, request.ReasonCode, cancellationToken) is { } open)
            {
                return new(ToItem(open));
            }

            await callAccessor.InsertAsync(tx, tenantId, request.Id, storeId, visitId, request.ReasonCode, context.DeviceId, context.Now, cancellationToken);
            var item = ToItem((await callAccessor.QueryAsync(tx, tenantId, storeId, request.Id, cancellationToken))!);
            await transaction.AppendEventAsync(EventTypes.CallCreated, item, [visit.TableId], null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(item, created: true);
        }, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // List
    //--------------------------------------------------------------------------------

    // 来店の呼び出しと状態 (古い順)
    public async ValueTask<ServiceResult<CallListResponse>> GetListAsync(Guid visitId, CancellationToken cancellationToken)
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

        var calls = await callAccessor.QueryListAsync(tenantId, visitId, cancellationToken);
        return new(new CallListResponse { Items = calls.Select(ToItem).ToList() });
    }

    // 店舗の呼び出し。既定は終わっていないもの (古い順)、Done は終わった新しい順に直近のもの
    public async ValueTask<ServiceResult<CallListResponse>> GetStoreListAsync(string? status, CancellationToken cancellationToken)
    {
        CallStatus? filter = null;
        if (!String.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse<CallStatus>(status, true, out var value) || !Enum.IsDefined(value))
            {
                return new(ServiceError.Validation("status", "Open / Acknowledged / Done のどれかを送ってください"));
            }

            filter = value;
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var calls = filter == CallStatus.Done
            ? await callAccessor.QueryDoneListAsync(tenantId, storeId, DoneLimit, cancellationToken)
            : (await callAccessor.QueryOpenListAsync(tenantId, storeId, cancellationToken)).Where(x => (filter is null) || (x.Status == filter)).ToList();
        return new(new CallListResponse { Items = calls.Select(ToItem).ToList() });
    }

    //--------------------------------------------------------------------------------
    // Change
    //--------------------------------------------------------------------------------

    // 向かう (テーブル端末に「スタッフが向かっています」と出す)
    public ValueTask<ServiceResult<CallListResponseItem>> AcknowledgeAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, CallStatus.Acknowledged, cancellationToken);

    // 対応した
    public ValueTask<ServiceResult<CallListResponseItem>> DoneAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, CallStatus.Done, cancellationToken);

    // すでに進んでいる呼び出しは変えずに返す (ホール端末どうしで同時に押しても失敗にしない)
    private ValueTask<ServiceResult<CallListResponseItem>> ChangeAsync(Guid id, CallStatus status, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return eventService.WriteAsync<ServiceResult<CallListResponseItem>>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var call = await callAccessor.QueryAsync(tx, tenantId, storeId, id, cancellationToken);
            if (call is null)
            {
                return new(ServiceError.NotFound);
            }

            var changed = status == CallStatus.Acknowledged
                ? await callAccessor.UpdateAcknowledgedAsync(tx, tenantId, id, context.Now, cancellationToken)
                : await callAccessor.UpdateDoneAsync(tx, tenantId, id, context.Now, cancellationToken);
            if (changed == 0)
            {
                return new(ToItem(call));
            }

            var item = ToItem((await callAccessor.QueryAsync(tx, tenantId, storeId, id, cancellationToken))!);
            await transaction.AppendEventAsync(EventTypes.CallUpdated, item, [call.TableId], null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(item);
        }, cancellationToken);
    }

    private static CallListResponseItem ToItem(CallEntity call) =>
        new()
        {
            Id = call.Id,
            VisitId = call.VisitId,
            TableId = call.TableId,
            TableName = call.TableName,
            ReasonCode = call.ReasonCode,
            Status = call.Status,
            CreatedAt = call.CreatedAt,
            AcknowledgedAt = call.AcknowledgedAt,
            DoneAt = call.DoneAt
        };
}
