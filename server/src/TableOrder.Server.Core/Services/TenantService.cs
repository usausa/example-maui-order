namespace TableOrder.Server.Core.Services;

using System.Text.RegularExpressions;

using TableOrder.Server.Core.Accessors;

// テナントをまたぐ処理 (管理画面のテナントと店舗の選択。運営者はすべてのテナント、ほかの利用者は自分のテナントだけを引く)
// テナントの登録と止める・戻すは運営者の画面から呼ぶ
public sealed partial class TenantService
{
    public const int MaxCodeLength = 32;

    public const int MaxNameLength = 50;

    private readonly ServiceContextProvider contextProvider;

    private readonly IDbProvider provider;

    private readonly IDialect dialect;

    private readonly IRevocationList revocationList;

    private readonly TenantAccessor tenantAccessor;

    private readonly StoreAccessor storeAccessor;

    private readonly AdminUserAccessor adminUserAccessor;

    public TenantService(
        ServiceContextProvider contextProvider,
        IDbProvider provider,
        IDialect dialect,
        IRevocationList revocationList,
        TenantAccessor tenantAccessor,
        StoreAccessor storeAccessor,
        AdminUserAccessor adminUserAccessor)
    {
        this.contextProvider = contextProvider;
        this.provider = provider;
        this.dialect = dialect;
        this.revocationList = revocationList;
        this.tenantAccessor = tenantAccessor;
        this.storeAccessor = storeAccessor;
        this.adminUserAccessor = adminUserAccessor;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public ValueTask<List<TenantEntity>> GetAllAsync(CancellationToken cancellationToken) =>
        tenantAccessor.QueryAllAsync(cancellationToken);

    public ValueTask<TenantEntity?> GetAsync(Guid tenantId, CancellationToken cancellationToken) =>
        tenantAccessor.QueryAsync(tenantId, cancellationToken);

    // 選んだテナントの店舗 (管理画面で店舗を選ぶ)
    public ValueTask<List<StoreEntity>> GetStoreAllAsync(Guid tenantId, CancellationToken cancellationToken) =>
        storeAccessor.QueryAllAsync(tenantId, cancellationToken);

    // 運営者の画面の一覧 (使っている店舗の数を添える)
    public ValueTask<List<TenantSummaryEntity>> GetSummaryListAsync(CancellationToken cancellationToken) =>
        tenantAccessor.QuerySummaryAllAsync(cancellationToken);

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    // テナントを登録する (店舗と利用者は、登録したあとに店舗と利用者の画面で足す)
    public async ValueTask<ServiceResult<TenantEntity>> CreateAsync(string code, string name, LocalizedText brandName, CancellationToken cancellationToken)
    {
        var trimmedCode = code.Trim();
        if (!CodePattern().IsMatch(trimmedCode))
        {
            return new(ServiceError.Validation("code", $"コードは英小文字・数字・ハイフンの {MaxCodeLength} 文字までで入れてください (先頭は英小文字か数字)"));
        }

        var trimmedName = name.Trim();
        if ((trimmedName.Length == 0) || (trimmedName.Length > MaxNameLength))
        {
            return new(ServiceError.Validation("name", $"名前は {MaxNameLength} 文字までで入れてください"));
        }

        if (String.IsNullOrWhiteSpace(brandName.Ja) || (brandName.Ja.Length > Length.BrandName) || (brandName.En?.Length > Length.BrandName))
        {
            return new(ServiceError.Validation("brandName", $"チェーンの名前は日本語を必ず入れ、言語ごとに {Length.BrandName} 文字までで入れてください"));
        }

        var context = contextProvider.Current;
        var id = Guid.CreateVersion7(context.Now);
        var brand = new LocalizedText { Ja = brandName.Ja.Trim(), En = String.IsNullOrWhiteSpace(brandName.En) ? null : brandName.En.Trim() };
        try
        {
            await tenantAccessor.InsertAsync(id, trimmedCode, trimmedName, brand, context.Now, cancellationToken);
        }
        catch (DbException e) when (dialect.IsDuplicate(e))
        {
            return new(ServiceError.Validation("code", "このコードのテナントはあります"));
        }

        return new((await tenantAccessor.QueryAsync(id, cancellationToken))!, true);
    }

    // 止める・戻す。止めたら、テナントの利用者の開いている管理画面をサインインからやり直させる
    // (止めたテナントにはトークンを出さず、利用者もサインインできない)。すぐに拒む一覧を読み直し、端末の今のトークンも断る (戻したら通す)
    public async ValueTask<ServiceError?> SetSuspendedAsync(Guid tenantId, bool suspended, int version, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var updated = await provider.UsingTxAsync(async (_, tx) =>
        {
            var status = suspended ? TenantStatus.Suspended : TenantStatus.Active;
            if (await tenantAccessor.UpdateStatusAsync(tx, tenantId, status, suspended ? context.Now : null, context.Now, version, cancellationToken) == 0)
            {
                return false;
            }

            if (suspended)
            {
                await adminUserAccessor.UpdateStampAllAsync(tx, tenantId, Guid.NewGuid().ToString("N"), context.Now, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return true;
        }, cancellationToken);
        if (updated)
        {
            await revocationList.RefreshAsync(cancellationToken);
            return null;
        }

        return await tenantAccessor.QueryAsync(tenantId, cancellationToken) is null ? ServiceError.NotFound : new ServiceError(ErrorCodes.VersionMismatch);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,31}$")]
    private static partial Regex CodePattern();
}
