namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Events;
using TableOrder.Contract.Visits;
using TableOrder.Server.Core.Accessors;

public sealed class VisitService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly StoreAccessor storeAccessor;

    private readonly VisitAccessor visitAccessor;

    private readonly MenuService menuService;

    private readonly EventService eventService;

    public VisitService(
        ServiceContextProvider contextProvider,
        StoreAccessor storeAccessor,
        VisitAccessor visitAccessor,
        MenuService menuService,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.storeAccessor = storeAccessor;
        this.visitAccessor = visitAccessor;
        this.menuService = menuService;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    // テーブル端末は自分のテーブルの来店だけを読める
    public async ValueTask<ServiceResult<VisitResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var visit = await visitAccessor.QueryAsync(context.RequireTenantId(), context.RequireStoreId(), id, cancellationToken);
        if (visit is null)
        {
            return new(ServiceError.NotFound);
        }

        if (!InScope(context, visit))
        {
            return new(ServiceError.DeviceScope);
        }

        return new(await ToResponseAsync(visit, cancellationToken));
    }

    // テーブル端末の置き場所のテーブルの今の来店 (なければ null で、端末は待受にする)
    public async ValueTask<VisitResponse?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        if (context.TableId is not { } tableId)
        {
            return null;
        }

        var visit = await visitAccessor.QueryOpenByTableAsync(context.RequireTenantId(), context.RequireStoreId(), tableId, cancellationToken);
        return visit is null ? null : await ToResponseAsync(visit, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Open
    //--------------------------------------------------------------------------------

    // 来店の開始。スタッフ (ホール端末、管理画面の案内) はいつでも開き、受付機とテーブル端末は来店の開き方で許した店だけ開く
    // 受付機はテーブルを送らず、サーバが人数の入る空席を選ぶ。テーブル端末は自分のテーブルにだけ開く
    // 同じ Id の送り直しは、同じテーブル (受付機は同じ端末) なら開いた来店を返す (201 ではなく 200)
    public async ValueTask<ServiceResult<VisitResponse>> CreateAsync(VisitCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return new(ServiceError.Validation("id", "来店の Id を送ってください"));
        }

        if (ValidateGuests(request.Adults, request.Children) is { } invalid)
        {
            return new(invalid);
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var store = await storeAccessor.QueryAsync(tenantId, storeId, cancellationToken);
        if (store is null)
        {
            return new(ServiceError.NotFound);
        }

        var openedBy = context.DeviceKind switch
        {
            DeviceKind.Reception => VisitOpenedBy.Reception,
            DeviceKind.Table => VisitOpenedBy.Table,
            _ => VisitOpenedBy.Hall
        };
        var (requestedTableId, rejected) = ResolveTable(context, openedBy, SettingsService.ReadFeatures(store.Features).VisitOpening, request.TableId);
        if (rejected is not null)
        {
            return new(rejected);
        }

        var businessDate = StoreHours.BusinessDate(context.Now, store.TimeZone, StoreHours.Parse(store.OpenTime));

        return await eventService.WriteAsync<ServiceResult<VisitResponse>>(tenantId, storeId, async transaction =>
        {
            var existing = await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, request.Id, cancellationToken);
            if (existing is not null)
            {
                // 受付機はテーブルを送らないので、同じ端末が開いたかで送り直しを見分ける
                var resent = requestedTableId is { } requested ? existing.TableId == requested : existing.OpenedDeviceId == context.DeviceId;
                return resent
                    ? new(await ToResponseAsync(existing, cancellationToken))
                    : new(new ServiceError(ErrorCodes.DuplicateIdMismatch));
            }

            Guid tableId;
            if (requestedTableId is { } requestedId)
            {
                if (await storeAccessor.QueryActiveTableAsync(transaction.Tx, tenantId, storeId, requestedId, cancellationToken) is null)
                {
                    return new(ServiceError.NotFound);
                }

                if (await visitAccessor.QueryOpenByTableAsync(transaction.Tx, tenantId, storeId, requestedId, cancellationToken) is not null)
                {
                    return new(new ServiceError(ErrorCodes.TableOccupied));
                }

                tableId = requestedId;
            }
            else
            {
                // 書き込みは店舗ごとに順に並ぶので、選んだ空席に開くまでの間にほかの来店は入らない
                var vacant = await storeAccessor.QueryVacantTableByGuestsAsync(transaction.Tx, tenantId, storeId, request.Adults + request.Children, cancellationToken);
                if (vacant is null)
                {
                    return new(new ServiceError(ErrorCodes.NoVacantTable));
                }

                tableId = vacant.Id;
            }

            await visitAccessor.InsertAsync(transaction.Tx, tenantId, request.Id, storeId, tableId, businessDate, request.Adults, request.Children, openedBy, context.DeviceId, context.Now, cancellationToken);
            var visit = await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, request.Id, cancellationToken);
            var response = ToResponse(visit!, []);
            await transaction.AppendEventAsync(EventTypes.VisitOpened, response, [tableId], null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(response, created: true);
        }, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    // 人数の変更 (会計中も直せる。割り勘の目安が変わる)
    public async ValueTask<ServiceResult<VisitResponse>> UpdateAsync(Guid id, VisitUpdateRequest request, CancellationToken cancellationToken)
    {
        if (ValidateGuests(request.Adults, request.Children) is { } invalid)
        {
            return new(invalid);
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return await eventService.WriteAsync<ServiceResult<VisitResponse>>(tenantId, storeId, async transaction =>
        {
            var visit = await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken);
            if (Check(visit, request.Version, allowPaying: true) is { } error)
            {
                return new(error);
            }

            if (await visitAccessor.UpdateGuestsAsync(transaction.Tx, tenantId, id, request.Adults, request.Children, request.Version, context.Now, cancellationToken) == 0)
            {
                return new(new ServiceError(ErrorCodes.VersionMismatch));
            }

            var response = await ToResponseAsync((await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken))!, cancellationToken);
            await transaction.AppendEventAsync(EventTypes.VisitUpdated, response, [response.TableId], null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(response);
        }, cancellationToken);
    }

    // テーブルの移動 (会計を始める前だけ)。元のテーブル端末は待受に、移動先は注文の画面になる
    public ValueTask<ServiceResult<VisitResponse>> MoveAsync(Guid id, VisitMoveRequest request, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return eventService.WriteAsync<ServiceResult<VisitResponse>>(tenantId, storeId, async transaction =>
        {
            var visit = await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken);
            if (Check(visit, request.Version, allowPaying: false) is { } error)
            {
                return new(error);
            }

            if (request.ToTableId == visit!.TableId)
            {
                return new(ServiceError.Validation("toTableId", "今のテーブルとは別のテーブルを送ってください"));
            }

            if (await storeAccessor.QueryActiveTableAsync(transaction.Tx, tenantId, storeId, request.ToTableId, cancellationToken) is null)
            {
                return new(ServiceError.NotFound);
            }

            if (await visitAccessor.QueryOpenByTableAsync(transaction.Tx, tenantId, storeId, request.ToTableId, cancellationToken) is not null)
            {
                return new(new ServiceError(ErrorCodes.TableOccupied));
            }

            if (await visitAccessor.UpdateTableAsync(transaction.Tx, tenantId, id, request.ToTableId, request.Version, context.Now, cancellationToken) == 0)
            {
                return new(new ServiceError(ErrorCodes.VersionMismatch));
            }

            var response = await ToResponseAsync((await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken))!, cancellationToken);
            var data = new VisitMovedEventData
            {
                Visit = response,
                FromTableId = visit.TableId,
                FromTableName = visit.TableName
            };
            await transaction.AppendEventAsync(EventTypes.VisitMoved, data, [visit.TableId, request.ToTableId], null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(response);
        }, cancellationToken);
    }

    // 確認のルール (お酒の年齢など) にお客様が答えた記録。来店で 1 回だけ持つ (答え直しても増やさない)
    // ホール端末は、代わりの注文でスタッフがお客様に確かめたときに記録する
    // 新しく記録したら来店の変更を知らせる (ほかの端末が同じ来店で聞き直さないように)
    public ValueTask<ServiceResult<VisitResponse>> ConfirmAsync(Guid id, VisitConfirmationRequest request, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return eventService.WriteAsync<ServiceResult<VisitResponse>>(tenantId, storeId, async transaction =>
        {
            var visit = await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken);
            if (visit is null)
            {
                return new(ServiceError.NotFound);
            }

            if (!InScope(context, visit))
            {
                return new(ServiceError.DeviceScope);
            }

            if (visit.Status is not (VisitStatus.Open or VisitStatus.Paying))
            {
                return new(new ServiceError(ErrorCodes.VisitNotOpen));
            }

            var store = await storeAccessor.QueryAsync(tenantId, storeId, cancellationToken);
            var catalog = store is null ? null : await menuService.GetCatalogAsync(store, cancellationToken);
            if ((catalog is null) || !catalog.Rules.TryGetValue(request.RuleId, out var rule) || (rule.Kind != MenuRuleKind.Confirmation))
            {
                return new(ServiceError.Validation("ruleId", "メニューの確認のルールを送ってください"));
            }

            var inserted = await visitAccessor.InsertConfirmationAsync(transaction.Tx, tenantId, id, request.RuleId, context.DeviceId, context.Now, cancellationToken);
            var confirmations = await visitAccessor.QueryConfirmationListAsync(transaction.Tx, tenantId, id, cancellationToken);
            var response = ToResponse(visit, confirmations.Select(static x => x.RuleId).ToList());
            if (inserted > 0)
            {
                await transaction.AppendEventAsync(EventTypes.VisitUpdated, response, [response.TableId], null, context.Now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new(response);
        }, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Close
    //--------------------------------------------------------------------------------

    // テーブルの外で会計した来店を終える (レジで払った、スタッフが閉じた)。テーブルで払い終えた来店はサーバが閉じる
    public async ValueTask<ServiceResult<VisitResponse>> CloseAsync(Guid id, VisitCloseRequest request, CancellationToken cancellationToken)
    {
        if (request.ClosedBy is not (VisitClosedBy.Register or VisitClosedBy.Hall))
        {
            return new(ServiceError.Validation("closedBy", "Register か Hall を送ってください"));
        }

        if (request.StaffId?.Length > Length.StaffId)
        {
            return new(ServiceError.Validation("staffId", $"スタッフの Id を {Length.StaffId} 文字までで送ってください"));
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return await eventService.WriteAsync<ServiceResult<VisitResponse>>(tenantId, storeId, async transaction =>
        {
            var visit = await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken);
            if (Check(visit, request.Version, allowPaying: true) is { } error)
            {
                return new(error);
            }

            if (await visitAccessor.UpdateClosedAsync(transaction.Tx, tenantId, id, request.ClosedBy, request.StaffId, request.Version, context.Now, cancellationToken) == 0)
            {
                return new(new ServiceError(ErrorCodes.VersionMismatch));
            }

            var response = await ToResponseAsync((await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken))!, cancellationToken);
            await transaction.AppendEventAsync(EventTypes.VisitClosed, response, [response.TableId], null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(response);
        }, cancellationToken);
    }

    // 注文のないまま帰った来店の取りやめ。テーブル端末と受付機には来店を終えたときと同じ通知を送る
    public ValueTask<ServiceResult<VisitResponse>> CancelAsync(Guid id, VisitCancelRequest request, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return eventService.WriteAsync<ServiceResult<VisitResponse>>(tenantId, storeId, async transaction =>
        {
            var visit = await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken);
            if (Check(visit, request.Version, allowPaying: false) is { } error)
            {
                return new(error);
            }

            if (await visitAccessor.CountOrderLineAsync(transaction.Tx, tenantId, id, cancellationToken) > 0)
            {
                return new(new ServiceError(ErrorCodes.VisitHasOrders));
            }

            if (await visitAccessor.UpdateCancelledAsync(transaction.Tx, tenantId, id, request.Version, context.Now, cancellationToken) == 0)
            {
                return new(new ServiceError(ErrorCodes.VersionMismatch));
            }

            var response = await ToResponseAsync((await visitAccessor.QueryAsync(transaction.Tx, tenantId, storeId, id, cancellationToken))!, cancellationToken);
            await transaction.AppendEventAsync(EventTypes.VisitClosed, response, [response.TableId], null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(response);
        }, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // テーブル端末は自分のテーブルの来店だけを扱える
    internal static bool InScope(ServiceContext context, VisitEntity visit) =>
        (context.DeviceKind != DeviceKind.Table) || (visit.TableId == context.TableId);

    // 来店の開き方から、来店を開くテーブルを決める (null は受付機で、サーバが空席から選ぶ)
    private static (Guid? TableId, ServiceError? Error) ResolveTable(ServiceContext context, VisitOpenedBy openedBy, VisitOpening opening, Guid? requestedTableId)
    {
        switch (openedBy)
        {
            case VisitOpenedBy.Reception:
                if (opening != VisitOpening.Reception)
                {
                    return (null, new ServiceError(ErrorCodes.VisitOpeningDisabled));
                }

                return requestedTableId is null
                    ? (null, null)
                    : (null, ServiceError.Validation("tableId", "受付機はテーブルを送らないでください (空席はサーバが選びます)"));
            case VisitOpenedBy.Table:
                if (opening != VisitOpening.Table)
                {
                    return (null, new ServiceError(ErrorCodes.VisitOpeningDisabled));
                }

                // 置き場所のないテーブル端末と、ほかのテーブルを送ったテーブル端末は開けない
                if ((context.TableId is not { } ownTableId) || ((requestedTableId is { } other) && (other != ownTableId)))
                {
                    return (null, ServiceError.DeviceScope);
                }

                return (ownTableId, null);
            default:
                return requestedTableId is null
                    ? (null, ServiceError.Validation("tableId", "テーブルを送ってください"))
                    : (requestedTableId, null);
        }
    }

    // 大人と子どもを合わせて 1 人以上
    private static ServiceError? ValidateGuests(int adults, int children)
    {
        if ((adults is < 0 or > Length.MaxGuests) || (children is < 0 or > Length.MaxGuests) || (adults + children == 0))
        {
            return ServiceError.Validation("adults", $"大人と子どもをそれぞれ {Length.MaxGuests} 人まで、合わせて 1 人以上で送ってください");
        }

        return null;
    }

    // 変える前の来店を確かめる。状態の違いは版の違いより先に返す (終わった来店は読み直しても変えられない)
    private static ServiceError? Check(VisitEntity? visit, int version, bool allowPaying)
    {
        if (visit is null)
        {
            return ServiceError.NotFound;
        }

        if ((visit.Status != VisitStatus.Open) && (!allowPaying || (visit.Status != VisitStatus.Paying)))
        {
            return new ServiceError(visit.Status == VisitStatus.Paying ? ErrorCodes.CheckoutInProgress : ErrorCodes.VisitNotOpen);
        }

        if (visit.Version != version)
        {
            return new ServiceError(ErrorCodes.VersionMismatch);
        }

        return null;
    }

    // 来店の応答 (答えた確認のルールを足す。会計と支払の通知にも使う)
    internal async ValueTask<VisitResponse> ToResponseAsync(VisitEntity visit, CancellationToken cancellationToken)
    {
        var confirmations = await visitAccessor.QueryConfirmationListAsync(visit.TenantId, visit.Id, cancellationToken);
        return ToResponse(visit, confirmations.Select(static x => x.RuleId).ToList());
    }

    private static VisitResponse ToResponse(VisitEntity visit, IReadOnlyList<Guid> confirmedRuleIds) =>
        new()
        {
            Id = visit.Id,
            TableId = visit.TableId,
            TableName = visit.TableName,
            Adults = visit.Adults,
            Children = visit.Children,
            Status = visit.Status,
            OpenedBy = visit.OpenedBy,
            OpenedAt = visit.OpenedAt,
            ClosedBy = visit.ClosedBy,
            ClosedAt = visit.ClosedAt,
            BusinessDate = visit.BusinessDate,
            ConfirmedRuleIds = confirmedRuleIds,
            OrderTotal = visit.OrderTotal,
            Version = visit.Version
        };
}
