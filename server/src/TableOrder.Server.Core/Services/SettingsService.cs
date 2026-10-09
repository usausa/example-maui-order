namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Devices;
using TableOrder.Contract.Events;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

// チェーンの設定 (名前・ロゴ・替える色。色は役割の名前と色)
public sealed record BrandSettings(LocalizedText Name, string? LogoImageName, IReadOnlyDictionary<string, string> Theme, int Version);

// 店舗の設定。呼び出しの用件は使うかどうかだけを替える
public sealed record StoreSettings(
    DeviceConfigResponseFeatures Features,
    IReadOnlyList<string> Languages,
    IReadOnlyList<PaymentMethod> PaymentMethods,
    IReadOnlyList<CallReasonSetting> CallReasons,
    int Version);

public sealed record CallReasonSetting(string Code, LocalizedText Name, bool IsActive);

// チェーンと店舗の設定 (管理画面)。替えたら設定の版を上げて store.updated を送り、テーブル端末は待受のときに起動からやり直して反映する
public sealed class SettingsService
{
    // 画面で選べる言語
    private static readonly string[] SupportedLanguages = ["ja", "en"];

    private readonly ServiceContextProvider contextProvider;

    private readonly IImageStore imageStore;

    private readonly StoreAccessor storeAccessor;

    private readonly SettingsAccessor settingsAccessor;

    private readonly EventService eventService;

    public SettingsService(
        ServiceContextProvider contextProvider,
        IImageStore imageStore,
        StoreAccessor storeAccessor,
        SettingsAccessor settingsAccessor,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.imageStore = imageStore;
        this.storeAccessor = storeAccessor;
        this.settingsAccessor = settingsAccessor;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // Brand
    //--------------------------------------------------------------------------------

    public async ValueTask<BrandSettings?> GetBrandAsync(CancellationToken cancellationToken)
    {
        var brand = await settingsAccessor.QueryBrandAsync(contextProvider.Current.RequireTenantId(), cancellationToken);
        return brand is null
            ? null
            : new BrandSettings(brand.BrandName, brand.LogoImageName, ReadTheme(brand.Theme).ToDictionary(static x => x.Role, static x => x.Color), brand.Version);
    }

    // チェーンの設定はテナントのすべての店舗に効くので、店舗ごとに設定の版を上げて知らせる
    public async ValueTask<ServiceError?> UpdateBrandAsync(BrandSettings settings, CancellationToken cancellationToken)
    {
        if (ValidateName(settings.Name) is { } invalidName)
        {
            return invalidName;
        }

        if (settings.Theme.FirstOrDefault(static x => !ThemeRoles.IsRole(x.Key) || !ThemeRoles.IsColor(x.Value)) is { Key: not null } invalidColor)
        {
            return ServiceError.Validation("theme", $"{invalidColor.Key} の色は #RRGGBB か #AARRGGBB で入れてください");
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        if ((settings.LogoImageName is { } logo) && (!ImageNames.IsValid(logo) || !await imageStore.ExistsAsync(tenantId, logo, cancellationToken)))
        {
            return ServiceError.Validation("logoImageName", "ロゴは置いてある画像から選んでください");
        }

        var theme = settings.Theme.Count > 0
            ? JsonSerializer.Serialize(ThemeRoles.All.Where(settings.Theme.ContainsKey).Select(x => new DeviceConfigResponseThemeColor { Role = x, Color = settings.Theme[x] }).ToList(), JsonDefaults.Options)
            : null;
        if (await settingsAccessor.UpdateBrandAsync(tenantId, settings.Name, settings.LogoImageName, theme, settings.Version, context.Now, cancellationToken) == 0)
        {
            return new ServiceError(ErrorCodes.VersionMismatch);
        }

        foreach (var store in await storeAccessor.QueryAllAsync(tenantId, cancellationToken))
        {
            await eventService.WriteAsync(tenantId, store.Id, async transaction =>
            {
                await settingsAccessor.UpdateSettingsVersionAsync(transaction.Tx, tenantId, store.Id, context.Now, cancellationToken);
                var updated = await storeAccessor.QueryAsync(transaction.Tx, tenantId, store.Id, cancellationToken);
                await transaction.AppendEventAsync(EventTypes.StoreUpdated, StoreService.ToResponse(updated!, context.Now), null, null, context.Now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }, cancellationToken);
        }

        return null;
    }

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    public async ValueTask<StoreSettings?> GetStoreSettingsAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var store = await storeAccessor.QueryAsync(tenantId, storeId, cancellationToken);
        if (store is null)
        {
            return null;
        }

        var reasons = await settingsAccessor.QueryCallReasonAllAsync(tenantId, storeId, cancellationToken);
        return new StoreSettings(
            ReadFeatures(store.Features),
            ReadList<string>(store.Languages),
            ReadList<PaymentMethod>(store.PaymentMethods),
            reasons.Select(static x => new CallReasonSetting(x.Code, x.Name, x.IsActive)).ToList(),
            store.Version);
    }

    // スタッフの PIN は替えるときだけ送る (null は替えない)。サーバはハッシュだけを持つ
    public async ValueTask<ServiceError?> UpdateStoreSettingsAsync(StoreSettings settings, string? newStaffPin, CancellationToken cancellationToken)
    {
        if (ValidateStore(settings, newStaffPin) is { } invalid)
        {
            return invalid;
        }

        string? staffPinHash = null;
        if (newStaffPin is not null)
        {
            var (salt, hash) = StaffPins.Create(newStaffPin);
            staffPinHash = JsonSerializer.Serialize(new DeviceConfigResponseStaffPin { Iterations = StaffPins.Iterations, Salt = salt, Hash = hash }, JsonDefaults.Options);
        }

        var languages = JsonSerializer.Serialize(settings.Languages, JsonDefaults.Options);
        var paymentMethods = JsonSerializer.Serialize(settings.PaymentMethods, JsonDefaults.Options);
        var features = JsonSerializer.Serialize(settings.Features, JsonDefaults.Options);
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return await eventService.WriteAsync<ServiceError?>(tenantId, storeId, async transaction =>
        {
            if (await settingsAccessor.UpdateStoreAsync(transaction.Tx, tenantId, storeId, languages, paymentMethods, features, staffPinHash, settings.Version, context.Now, cancellationToken) == 0)
            {
                return new ServiceError(ErrorCodes.VersionMismatch);
            }

            foreach (var reason in settings.CallReasons)
            {
                await settingsAccessor.UpdateCallReasonAsync(transaction.Tx, tenantId, storeId, reason.Code, reason.IsActive, cancellationToken);
            }

            var store = await storeAccessor.QueryAsync(transaction.Tx, tenantId, storeId, cancellationToken);
            await transaction.AppendEventAsync(EventTypes.StoreUpdated, StoreService.ToResponse(store!, context.Now), null, null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Validation
    //--------------------------------------------------------------------------------

    private static ServiceError? ValidateName(LocalizedText name) =>
        String.IsNullOrWhiteSpace(name.Ja) || (name.Ja.Length > Length.BrandName) || (name.En?.Length > Length.BrandName)
            ? ServiceError.Validation("name", $"チェーンの名前は日本語を必ず入れ、言語ごとに {Length.BrandName} 文字までで入れてください")
            : null;

    private static ServiceError? ValidateStore(StoreSettings settings, string? newStaffPin)
    {
        if ((settings.Languages.Count == 0) || !settings.Languages.All(SupportedLanguages.Contains) || (settings.Languages.Distinct().Count() != settings.Languages.Count))
        {
            return ServiceError.Validation("languages", "言語を 1 つ以上選んでください");
        }

        if (!settings.PaymentMethods.All(Enum.IsDefined) || (settings.PaymentMethods.Distinct().Count() != settings.PaymentMethods.Count))
        {
            return ServiceError.Validation("paymentMethods", "支払方法を選び直してください");
        }

        var features = settings.Features;
        if (!features.RegisterCheckout && (settings.PaymentMethods.Count == 0))
        {
            return ServiceError.Validation("registerCheckout", "テーブルで払えない店は、レジでの会計を外せません");
        }

        if (features.LastOrderNoticeMinutes is < 0 or > 180)
        {
            return ServiceError.Validation("lastOrderNoticeMinutes", "ラストオーダーの知らせは 0 から 180 分で入れてください");
        }

        if (features.FinishSeconds is < 5 or > 300)
        {
            return ServiceError.Validation("finishSeconds", "お礼の画面の時間は 5 から 300 秒で入れてください");
        }

        if (!Enum.IsDefined(features.VisitOpening))
        {
            return ServiceError.Validation("visitOpening", "来店の開き方を選び直してください");
        }

        if (features.KitchenAlertMinutes is < 0 or > 120)
        {
            return ServiceError.Validation("kitchenAlertMinutes", "キッチンの遅れの時間は 0 から 120 分で入れてください");
        }

        if ((newStaffPin is not null) && !StaffPins.IsValid(newStaffPin))
        {
            return ServiceError.Validation("staffPin", $"PIN は {Length.StaffPinDigits} 桁の数字で入れてください");
        }

        return null;
    }

    //--------------------------------------------------------------------------------
    // Json
    //--------------------------------------------------------------------------------

    internal static List<DeviceConfigResponseThemeColor> ReadTheme(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize<List<DeviceConfigResponseThemeColor>>(json, JsonDefaults.Options) ?? [];

    // ない項目は既定の値にする
    internal static DeviceConfigResponseFeatures ReadFeatures(string json) =>
        JsonSerializer.Deserialize<DeviceConfigResponseFeatures>(json, JsonDefaults.Options) ?? new DeviceConfigResponseFeatures();

    internal static DeviceConfigResponseStaffPin ReadStaffPin(string json) =>
        JsonSerializer.Deserialize<DeviceConfigResponseStaffPin>(json, JsonDefaults.Options)!;

    private static List<T> ReadList<T>(string json) =>
        JsonSerializer.Deserialize<List<T>>(json, JsonDefaults.Options) ?? [];
}
