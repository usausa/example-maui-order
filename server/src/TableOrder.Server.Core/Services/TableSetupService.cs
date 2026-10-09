namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Events;
using TableOrder.Server.Core.Accessors;

// 足す・替えるテーブル
public sealed record TableInput(string Name, string? Area, int Capacity);

// テーブルの管理 (追加、名前・エリア・席の数の変更、並べ替え、使わなくする)。管理画面から、選んだ店舗の文脈で呼ぶ
// 替えたら店舗の設定の版を上げて store.updated を送り、端末は起動からやり直してテーブルを読み直す
public sealed class TableSetupService
{
    public const int MaxNameLength = 20;

    public const int MaxAreaLength = 20;

    public const int MaxCapacity = 50;

    private readonly ServiceContextProvider contextProvider;

    private readonly IDialect dialect;

    private readonly EventService eventService;

    private readonly StoreAccessor storeAccessor;

    private readonly SettingsAccessor settingsAccessor;

    public TableSetupService(
        ServiceContextProvider contextProvider,
        IDialect dialect,
        EventService eventService,
        StoreAccessor storeAccessor,
        SettingsAccessor settingsAccessor)
    {
        this.contextProvider = contextProvider;
        this.dialect = dialect;
        this.eventService = eventService;
        this.storeAccessor = storeAccessor;
        this.settingsAccessor = settingsAccessor;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public ValueTask<List<TableSetupEntity>> GetListAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        return storeAccessor.QueryTableSetupListAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    // 並びの最後に足す
    public async ValueTask<ServiceError?> AddAsync(TableInput input, CancellationToken cancellationToken)
    {
        if (Validate(input) is { } invalid)
        {
            return invalid;
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return await WriteAsync(tenantId, storeId, async transaction =>
        {
            await storeAccessor.InsertTableAsync(transaction.Tx, tenantId, Guid.CreateVersion7(context.Now), storeId, input.Name.Trim(), NullIfEmpty(input.Area), input.Capacity, context.Now, cancellationToken);
            return null;
        }, cancellationToken);
    }

    public async ValueTask<ServiceError?> UpdateAsync(Guid tableId, TableInput input, int version, CancellationToken cancellationToken)
    {
        if (Validate(input) is { } invalid)
        {
            return invalid;
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return await WriteAsync(tenantId, storeId, async transaction =>
            await storeAccessor.UpdateTableAsync(transaction.Tx, tenantId, storeId, tableId, input.Name.Trim(), NullIfEmpty(input.Area), input.Capacity, version, context.Now, cancellationToken) > 0
                ? null
                : new ServiceError(ErrorCodes.VersionMismatch),
            cancellationToken);
    }

    // 並びを 1 つ前か後ろの (使わなくしたものも含めた) テーブルと入れ替え、並びの番号を振り直す
    public ValueTask<ServiceError?> MoveAsync(Guid tableId, int offset, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return WriteAsync(tenantId, storeId, async transaction =>
        {
            var tables = await storeAccessor.QueryTableSetupListAsync(transaction.Tx, tenantId, storeId, cancellationToken);
            var index = tables.FindIndex(x => x.Id == tableId);
            var target = index + offset;
            if (index < 0)
            {
                return ServiceError.NotFound;
            }

            if ((target < 0) || (target >= tables.Count))
            {
                return ServiceError.Validation("offset", "これより前 (後ろ) には動かせません");
            }

            (tables[index], tables[target]) = (tables[target], tables[index]);
            for (var i = 0; i < tables.Count; i++)
            {
                if (tables[i].SortOrder != i + 1)
                {
                    await storeAccessor.UpdateTableSortOrderAsync(transaction.Tx, tenantId, storeId, tables[i].Id, i + 1, context.Now, cancellationToken);
                }
            }

            return null;
        }, cancellationToken);
    }

    // 使わなくするのは、来店中でなく、置いた端末がないときだけ (端末の置き場所を替え、来店を閉じてから)
    public ValueTask<ServiceError?> SetActiveAsync(Guid tableId, bool isActive, int version, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return WriteAsync(tenantId, storeId, async transaction =>
        {
            if (!isActive)
            {
                var tables = await storeAccessor.QueryTableSetupListAsync(transaction.Tx, tenantId, storeId, cancellationToken);
                if (tables.Find(x => x.Id == tableId) is { } table)
                {
                    if (table.HasOpenVisit)
                    {
                        return ServiceError.Validation("isActive", "来店中です。来店を閉じてから使わなくしてください");
                    }

                    if (table.DeviceCount > 0)
                    {
                        return ServiceError.Validation("isActive", "置いた端末があります。端末の置き場所を替えるか無効にしてから使わなくしてください");
                    }
                }
            }

            return await storeAccessor.UpdateTableActiveAsync(transaction.Tx, tenantId, storeId, tableId, isActive, version, context.Now, cancellationToken) > 0
                ? null
                : new ServiceError(ErrorCodes.VersionMismatch);
        }, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // テーブルを替えたら (action が失敗を返さなければ)、設定の版を上げて店舗の端末に知らせる。失敗なら書き込みを捨てる
    private async ValueTask<ServiceError?> WriteAsync(Guid tenantId, Guid storeId, Func<StoreTransaction, ValueTask<ServiceError?>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await eventService.WriteAsync<ServiceError?>(tenantId, storeId, async transaction =>
            {
                if (await action(transaction) is { } error)
                {
                    return error;
                }

                var now = contextProvider.Current.Now;
                await settingsAccessor.UpdateSettingsVersionAsync(transaction.Tx, tenantId, storeId, now, cancellationToken);
                var store = await storeAccessor.QueryAsync(transaction.Tx, tenantId, storeId, cancellationToken);
                await transaction.AppendEventAsync(EventTypes.StoreUpdated, StoreService.ToResponse(store!, now), null, null, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }, cancellationToken);
        }
        catch (DbException e) when (dialect.IsDuplicate(e))
        {
            return ServiceError.Validation("name", "同じ名前のテーブルがあります");
        }
    }

    private static ServiceError? Validate(TableInput input)
    {
        var name = input.Name.Trim();
        if ((name.Length == 0) || (name.Length > MaxNameLength))
        {
            return ServiceError.Validation("name", $"テーブルの名前は {MaxNameLength} 文字までで入れてください");
        }

        if (input.Area?.Trim().Length > MaxAreaLength)
        {
            return ServiceError.Validation("area", $"エリアは {MaxAreaLength} 文字までで入れてください");
        }

        return input.Capacity is < 1 or > MaxCapacity
            ? ServiceError.Validation("capacity", $"席の数は 1 から {MaxCapacity} で入れてください")
            : null;
    }

    private static string? NullIfEmpty(string? value) =>
        String.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
