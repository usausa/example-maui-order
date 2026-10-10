namespace TableOrder.Server.Core.Services;

using System.Text.RegularExpressions;

using TableOrder.Contract.Devices;
using TableOrder.Contract.Events;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

// 店舗の基本 (店舗コード、名前、タイムゾーン、営業時間、ラストオーダー、税の丸め、注文の上限)
public sealed record StoreBasics(
    string Code,
    LocalizedText Name,
    string TimeZone,
    string OpenTime,
    string CloseTime,
    string? LastOrderTime,
    TaxRounding TaxRounding,
    int MaxQuantityPerLine,
    int MaxLinesPerOrder);

// 新しい店舗に入れるメニュー (写す店舗がないとき。外部の連携でメニューを公開するまでの仮)
public sealed record MenuSeed(string MenuVersion, string Content);

// 店舗の追加と基本の変更、使わなくすること (運営者とテナントの管理者が、選んだテナントの文脈で呼ぶ)
// 新しい店舗の設定 (言語、支払方法、電子レシート、機能、呼び出しの用件、メニュー) は選んだ店舗から写し、なければ既定の設定とサンプルのメニューにする
public sealed partial class StoreSetupService
{
    public const int MaxCodeLength = 16;

    public const int MaxQuantityLimit = 99;

    public const int MaxLinesLimit = 99;

    private const string DefaultLanguages = """["ja","en"]""";

    private const string DefaultPaymentMethods = """["QrCode","CreditCard"]""";

    private const string DefaultFeatures = "{}";

    private readonly ServiceContextProvider contextProvider;

    private readonly IDbProvider provider;

    private readonly IDialect dialect;

    private readonly StoreAccessor storeAccessor;

    private readonly SettingsAccessor settingsAccessor;

    private readonly MenuAccessor menuAccessor;

    private readonly EventAccessor eventAccessor;

    private readonly EventService eventService;

    public StoreSetupService(
        ServiceContextProvider contextProvider,
        IDbProvider provider,
        IDialect dialect,
        StoreAccessor storeAccessor,
        SettingsAccessor settingsAccessor,
        MenuAccessor menuAccessor,
        EventAccessor eventAccessor,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.provider = provider;
        this.dialect = dialect;
        this.storeAccessor = storeAccessor;
        this.settingsAccessor = settingsAccessor;
        this.menuAccessor = menuAccessor;
        this.eventAccessor = eventAccessor;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public ValueTask<List<StoreEntity>> GetListAsync(CancellationToken cancellationToken) =>
        storeAccessor.QueryAllAsync(contextProvider.Current.RequireTenantId(), cancellationToken);

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    public async ValueTask<ServiceResult<StoreEntity>> AddAsync(StoreBasics basics, string staffPin, Guid? sourceStoreId, MenuSeed fallbackMenu, CancellationToken cancellationToken)
    {
        if (Validate(basics) is { } invalid)
        {
            return new(invalid);
        }

        if (!StaffPins.IsValid(staffPin))
        {
            return new(ServiceError.Validation("staffPin", $"スタッフの PIN は {Length.StaffPinDigits} 桁の数字で入れてください"));
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();

        // 写す設定 (写す店舗がなければ既定の設定とサンプルのメニュー)
        var languages = DefaultLanguages;
        var paymentMethods = DefaultPaymentMethods;
        var electronicReceipt = false;
        var features = DefaultFeatures;
        var menu = fallbackMenu;
        List<CallReasonEntity> reasons =
        [
            new() { Code = "Staff", Name = new LocalizedText { Ja = "店員を呼ぶ", En = "Call staff" }, SortOrder = 1, IsActive = true },
            new() { Code = "Water", Name = new LocalizedText { Ja = "お水", En = "Water" }, SortOrder = 2, IsActive = true }
        ];
        if (sourceStoreId is { } sourceId)
        {
            var source = await storeAccessor.QueryAsync(tenantId, sourceId, cancellationToken);
            if (source is null)
            {
                return new(ServiceError.Validation("sourceStoreId", "設定を写す店舗を選び直してください"));
            }

            languages = source.Languages;
            paymentMethods = source.PaymentMethods;
            electronicReceipt = source.ElectronicReceipt;
            features = source.Features;
            reasons = await settingsAccessor.QueryCallReasonAllAsync(tenantId, sourceId, cancellationToken);
            if (await menuAccessor.QueryCurrentAsync(tenantId, sourceId, cancellationToken) is { } publication)
            {
                menu = new MenuSeed(publication.MenuVersion, publication.Content);
            }
        }

        var (salt, hash) = StaffPins.Create(staffPin);
        var staffPinHash = JsonSerializer.Serialize(new DeviceConfigResponseStaffPin { Iterations = StaffPins.Iterations, Salt = salt, Hash = hash }, JsonDefaults.Options);
        var storeId = Guid.CreateVersion7(context.Now);
        var publicationId = Guid.CreateVersion7(context.Now);
        try
        {
            await provider.UsingTxAsync(async (_, tx) =>
            {
                // 店舗と今のメニューは互いに指す (外部キーはコミットのときに確かめる)
                await storeAccessor.InsertAsync(
                    tx,
                    tenantId,
                    storeId,
                    basics.Code.Trim(),
                    TrimName(basics.Name),
                    basics.TimeZone,
                    basics.OpenTime,
                    basics.CloseTime,
                    NullIfEmpty(basics.LastOrderTime),
                    basics.TaxRounding,
                    basics.MaxQuantityPerLine,
                    basics.MaxLinesPerOrder,
                    languages,
                    paymentMethods,
                    electronicReceipt,
                    features,
                    staffPinHash,
                    publicationId,
                    context.Now,
                    cancellationToken);
                await menuAccessor.InsertAsync(tx, tenantId, publicationId, storeId, menu.MenuVersion, menu.Content, context.Now, cancellationToken);
                foreach (var reason in reasons)
                {
                    await storeAccessor.InsertCallReasonAsync(tx, tenantId, storeId, reason.Code, reason.Name, reason.SortOrder, reason.IsActive, cancellationToken);
                }

                await eventAccessor.UpsertSequenceAsync(tx, tenantId, storeId, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (DbException e) when (dialect.IsDuplicate(e))
        {
            return new(ServiceError.Validation("code", "この店舗コードの店舗はあります"));
        }

        return new((await storeAccessor.QueryAsync(tenantId, storeId, cancellationToken))!, true);
    }

    // 基本を替えたら設定の版を上げて店舗の端末に知らせる (端末は起動からやり直して読み直す)
    public async ValueTask<ServiceError?> UpdateAsync(Guid storeId, StoreBasics basics, int version, CancellationToken cancellationToken)
    {
        if (Validate(basics) is { } invalid)
        {
            return invalid;
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        if (await storeAccessor.QueryAsync(tenantId, storeId, cancellationToken) is null)
        {
            return ServiceError.NotFound;
        }

        try
        {
            return await eventService.WriteAsync<ServiceError?>(tenantId, storeId, async transaction =>
            {
                if (await storeAccessor.UpdateBasicsAsync(
                        transaction.Tx,
                        tenantId,
                        storeId,
                        basics.Code.Trim(),
                        TrimName(basics.Name),
                        basics.TimeZone,
                        basics.OpenTime,
                        basics.CloseTime,
                        NullIfEmpty(basics.LastOrderTime),
                        basics.TaxRounding,
                        basics.MaxQuantityPerLine,
                        basics.MaxLinesPerOrder,
                        version,
                        context.Now,
                        cancellationToken) == 0)
                {
                    return new ServiceError(ErrorCodes.VersionMismatch);
                }

                var store = await storeAccessor.QueryAsync(transaction.Tx, tenantId, storeId, cancellationToken);
                await transaction.AppendEventAsync(EventTypes.StoreUpdated, StoreService.ToResponse(store!, context.Now), null, null, context.Now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }, cancellationToken);
        }
        catch (DbException e) when (dialect.IsDuplicate(e))
        {
            return ServiceError.Validation("code", "この店舗コードの店舗はあります");
        }
    }

    // 使わなくするのは、使っている端末と開いている来店がないときだけ (端末を無効にし、来店を閉じてから)
    public async ValueTask<ServiceError?> SetActiveAsync(Guid storeId, bool isActive, int version, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        if (!isActive)
        {
            if (await storeAccessor.CountActiveDeviceAsync(tenantId, storeId, cancellationToken) > 0)
            {
                return ServiceError.Validation("isActive", "使っている端末があります。端末をすべて無効にしてから使わなくしてください");
            }

            if (await storeAccessor.CountOpenVisitAsync(tenantId, storeId, cancellationToken) > 0)
            {
                return ServiceError.Validation("isActive", "来店中のテーブルがあります。来店を閉じてから使わなくしてください");
            }
        }

        if (await storeAccessor.UpdateActiveAsync(tenantId, storeId, isActive, version, context.Now, cancellationToken) == 0)
        {
            return await storeAccessor.QueryAsync(tenantId, storeId, cancellationToken) is null ? ServiceError.NotFound : new ServiceError(ErrorCodes.VersionMismatch);
        }

        return null;
    }

    //--------------------------------------------------------------------------------
    // Validation
    //--------------------------------------------------------------------------------

    private static ServiceError? Validate(StoreBasics basics)
    {
        if (!CodePattern().IsMatch(basics.Code.Trim()))
        {
            return ServiceError.Validation("code", $"店舗コードは英数字とハイフンの {MaxCodeLength} 文字までで入れてください");
        }

        if (String.IsNullOrWhiteSpace(basics.Name.Ja) || (basics.Name.Ja.Trim().Length > Length.BrandName) || (basics.Name.En?.Trim().Length > Length.BrandName))
        {
            return ServiceError.Validation("name", $"店舗の名前は日本語を必ず入れ、言語ごとに {Length.BrandName} 文字までで入れてください");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(basics.TimeZone, out _))
        {
            return ServiceError.Validation("timeZone", "タイムゾーンを選び直してください");
        }

        if (!IsTime(basics.OpenTime) || !IsTime(basics.CloseTime))
        {
            return ServiceError.Validation("openTime", "営業時間は HH:mm で入れてください");
        }

        if (!String.IsNullOrEmpty(basics.LastOrderTime) && !IsTime(basics.LastOrderTime))
        {
            return ServiceError.Validation("lastOrderTime", "ラストオーダーは HH:mm で入れてください (ないときは空にする)");
        }

        if (!Enum.IsDefined(basics.TaxRounding))
        {
            return ServiceError.Validation("taxRounding", "税の丸めを選び直してください");
        }

        if (basics.MaxQuantityPerLine is < 1 or > MaxQuantityLimit)
        {
            return ServiceError.Validation("maxQuantityPerLine", $"1 明細の数量の上限は 1 から {MaxQuantityLimit} で入れてください");
        }

        if (basics.MaxLinesPerOrder is < 1 or > MaxLinesLimit)
        {
            return ServiceError.Validation("maxLinesPerOrder", $"1 回の注文の明細の上限は 1 から {MaxLinesLimit} で入れてください");
        }

        return null;
    }

    private static bool IsTime(string value) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static LocalizedText TrimName(LocalizedText name) =>
        new() { Ja = name.Ja.Trim(), En = String.IsNullOrWhiteSpace(name.En) ? null : name.En.Trim() };

    private static string? NullIfEmpty(string? value) =>
        String.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[0-9A-Za-z-]{1,16}$")]
    private static partial Regex CodePattern();
}
