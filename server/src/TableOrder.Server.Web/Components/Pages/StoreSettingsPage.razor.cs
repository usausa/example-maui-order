namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using TableOrder.Contract.Devices;
using TableOrder.Server.Web.Application.Context;

// 店舗の設定 (選んだ店舗の来店の開き方、機能の有無、言語、支払方法、呼び出しの用件、スタッフの PIN)。保存すると店舗のテーブル端末に知らせる
public sealed partial class StoreSettingsPage : IDisposable
{
    private List<ReasonChoice> reasons = [];

    private int? version;

    private VisitOpening visitOpening;

    private bool registerCheckout;

    private bool splitPayment;

    private int lastOrderNoticeMinutes;

    private int finishSeconds;

    private int kitchenAlertMinutes;

    private bool japanese;

    private bool english;

    private bool qrCode;

    private bool creditCard;

    private string? newPin;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required SettingsService SettingsService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override Task OnInitializedAsync()
    {
        Selection.Changed += OnSelectionChanged;
        return LoadAsync();
    }

    public void Dispose() => Selection.Changed -= OnSelectionChanged;

    // 店舗を選び直したら読み直す (店舗の選択の操作の中から呼ばれるので、文脈を始め直す)
    private void OnSelectionChanged(object? sender, EventArgs e) =>
        _ = ReloadAsync(LoadAsync);

    private async Task LoadAsync()
    {
        version = null;
        newPin = null;
        if ((Selection.StoreId is null) || (await SettingsService.GetStoreSettingsAsync(CancellationToken.None) is not { } settings))
        {
            return;
        }

        visitOpening = settings.Features.VisitOpening;
        registerCheckout = settings.Features.RegisterCheckout;
        splitPayment = settings.Features.SplitPayment;
        lastOrderNoticeMinutes = settings.Features.LastOrderNoticeMinutes;
        finishSeconds = settings.Features.FinishSeconds;
        kitchenAlertMinutes = settings.Features.KitchenAlertMinutes;
        japanese = settings.Languages.Contains("ja");
        english = settings.Languages.Contains("en");
        qrCode = settings.PaymentMethods.Contains(PaymentMethod.QrCode);
        creditCard = settings.PaymentMethods.Contains(PaymentMethod.CreditCard);
        reasons = settings.CallReasons.Select(static x => new ReasonChoice(x.Code, x.Name.Ja) { IsActive = x.IsActive }).ToList();
        version = settings.Version;
    }

    //--------------------------------------------------------------------------------
    // Edit
    //--------------------------------------------------------------------------------

    private async Task SaveAsync()
    {
        if (version is not { } current)
        {
            return;
        }

        var settings = new StoreSettings(
            new DeviceConfigResponseFeatures
            {
                RegisterCheckout = registerCheckout,
                SplitPayment = splitPayment,
                LastOrderNoticeMinutes = lastOrderNoticeMinutes,
                FinishSeconds = finishSeconds,
                VisitOpening = visitOpening,
                KitchenAlertMinutes = kitchenAlertMinutes
            },
            Choose(("ja", japanese), ("en", english)),
            Choose((PaymentMethod.QrCode, qrCode), (PaymentMethod.CreditCard, creditCard)),
            reasons.Select(static x => new CallReasonSetting(x.Code, new LocalizedText { Ja = x.Name }, x.IsActive)).ToList(),
            current);
        var error = await SettingsService.UpdateStoreSettingsAsync(settings, String.IsNullOrEmpty(newPin) ? null : newPin, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add("店舗の設定を保存しました。テーブル端末は待受のときに読み直します", Severity.Success);
        await LoadAsync();
    }

    private static List<T> Choose<T>(params (T Value, bool Selected)[] choices) =>
        choices.Where(static x => x.Selected).Select(static x => x.Value).ToList();

    // 呼び出しの用件を使うかどうか (画面で替える)
    private sealed class ReasonChoice
    {
        public string Code { get; }

        public string Name { get; }

        public bool IsActive { get; set; }

        public ReasonChoice(string code, string name)
        {
            Code = code;
            Name = name;
        }
    }
}
