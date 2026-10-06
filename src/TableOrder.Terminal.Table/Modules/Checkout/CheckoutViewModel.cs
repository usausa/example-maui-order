namespace TableOrder.Terminal.Table.Modules.Checkout;

// お会計。左に明細と合計・割り勘の目安、右に支払方法を選んでから QR コード決済 / カード / レジの案内を出す
// 支払の完了は読み直して待ち (サーバの通知ができたら通知で替える)、終わったらお礼と電子レシートを出して待受に戻る
public sealed partial class CheckoutViewModel : AppViewModelBase
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    // お礼を出したまま操作がなければ待受に戻す
    private static readonly TimeSpan FinishAfter = TimeSpan.FromSeconds(30);

    private readonly ILogger<CheckoutViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly Settings settings;

    private readonly VisitState visitState;

    private readonly LanguageState languageState;

    private readonly IOrderApi orderApi;

    private readonly OrderUsecase orderUsecase;

    private CancellationTokenSource? waiting;

    private BillResponse? bill;

    private PaymentResponse? payment;

    public string TableText { get; }

    public string GuestsText { get; }

    public bool CanUseQrCode { get; }

    public bool CanUseCreditCard { get; }

    public string RegisterMessage { get; }

    // 明細

    public ObservableCollection<CheckoutLine> Lines { get; } = [];

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TaxText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SplitText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSplit { get; set; }

    [ObservableProperty]
    public partial bool HasUnserved { get; set; }

    // 支払

    [ObservableProperty]
    public partial string PanelTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsMethod { get; set; }

    [ObservableProperty]
    public partial bool IsQrCode { get; set; }

    [ObservableProperty]
    public partial bool IsCreditCard { get; set; }

    [ObservableProperty]
    public partial bool IsWaiting { get; set; }

    [ObservableProperty]
    public partial bool IsRegister { get; set; }

    // 支払を待っている間とレジの案内の間は、支払方法を選び直せる
    [ObservableProperty]
    public partial bool CanChangeMethod { get; set; }

    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    [ObservableProperty]
    public partial string QrValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReceiptValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasReceipt { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    public IObserveCommand QrCodeCommand { get; }

    public IObserveCommand CreditCardCommand { get; }

    public IObserveCommand RegisterCommand { get; }

    public IObserveCommand ChangeMethodCommand { get; }

    public IObserveCommand BackCommand { get; }

    public IObserveCommand FinishCommand { get; }

    public IObserveCommand StaffCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public CheckoutViewModel(
        ILogger<CheckoutViewModel> log,
        IPopupNavigator popupNavigator,
        Settings settings,
        MenuState menuState,
        VisitState visitState,
        LanguageState languageState,
        IOrderApi orderApi,
        OrderUsecase orderUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.settings = settings;
        this.orderApi = orderApi;
        this.visitState = visitState;
        this.languageState = languageState;
        this.orderUsecase = orderUsecase;

        TableText = ViewHelper.Table(settings.TableNo);
        GuestsText = ViewHelper.Guests(visitState.Guests);
        CanUseQrCode = menuState.Config.PaymentMethods.Contains(PaymentMethod.QrCode);
        CanUseCreditCard = menuState.Config.PaymentMethods.Contains(PaymentMethod.CreditCard);
        RegisterMessage = ViewHelper.Format(AppResources.RegisterMessageFormat, TableText);

        QrCodeCommand = MakeAsyncCommand(() => PayAsync(PaymentMethod.QrCode), () => IsMethod);
        CreditCardCommand = MakeAsyncCommand(() => PayAsync(PaymentMethod.CreditCard), () => IsMethod);
        RegisterCommand = MakeDelegateCommand(() => SetStep(CheckoutStep.Register), () => IsMethod);
        ChangeMethodCommand = MakeAsyncCommand(ChangeMethodAsync, () => CanChangeMethod);
        BackCommand = MakeAsyncCommand(BackAsync, () => !IsCompleted);
        FinishCommand = MakeAsyncCommand(FinishAsync, () => IsCompleted);
        StaffCommand = MakeAsyncCommand(OpenStaffAsync);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopWaiting();
        }

        base.Dispose(disposing);
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    public override async Task OnNavigatedToAsync(INavigationContext context) =>
        await Navigator.PostActionAsync(LoadAsync);

    // 戻るは画面の「戻る」と同じ (支払を待っている間とレジの案内の間は支払方法の選び直し)
    protected override async Task OnNotifyBackAsync()
    {
        if (CanChangeMethod)
        {
            await ChangeMethodAsync();
        }
        else if (IsCompleted)
        {
            await FinishAsync();
        }
        else
        {
            await BackAsync();
        }
    }

    //--------------------------------------------------------------------------------
    // Bill
    //--------------------------------------------------------------------------------

    private async Task LoadAsync()
    {
        SetStep(CheckoutStep.Loading);

        var result = await orderApi.GetBillAsync(visitState.Id);
        if (result.Content is not { } content)
        {
            log.WarnApiFailed(nameof(IOrderApi.GetBillAsync), result.Status, result.ErrorCode);
            ShowError(ViewHelper.ErrorMessage(result));
            SetStep(CheckoutStep.Method);
            return;
        }

        ApplyBill(content);
        SetStep(CheckoutStep.Method);
    }

    private void ApplyBill(BillResponse content)
    {
        bill = content;

        var language = languageState.Current;
        Lines.Clear();
        foreach (var line in content.Lines)
        {
            Lines.Add(new CheckoutLine(line, language));
        }

        CountText = ViewHelper.Count(content.Lines.Sum(static x => x.Quantity));
        TotalText = ViewHelper.Price(content.Balance);
        TaxText = String.Join(
            Environment.NewLine,
            content.Taxes.Select(static x => ViewHelper.Format(
                AppResources.CheckoutTaxFormat,
                (x.Rate * 100).ToString("0", CultureInfo.InvariantCulture),
                ViewHelper.Price(x.TaxableAmount),
                ViewHelper.Price(x.TaxAmount))));
        HasSplit = content.Guests > 1;
        SplitText = ViewHelper.Format(AppResources.CheckoutSplitFormat, content.Guests, String.Join(" / ", content.SplitAmounts.Select(ViewHelper.Price)));
        HasUnserved = content.HasUnservedLines;
    }

    //--------------------------------------------------------------------------------
    // Payment
    //--------------------------------------------------------------------------------

    // 会計を始めて (注文を止める) から支払を作り、完了を待つ
    private async Task PayAsync(PaymentMethod method)
    {
        if (bill is null)
        {
            return;
        }

        HasError = false;

        if (visitState.Status != VisitStatus.Paying)
        {
            var checkout = await orderApi.StartCheckoutAsync(visitState.Id, new CheckoutRequest { BillVersion = bill.BillVersion, Version = visitState.Version });
            if (checkout.Content is not { } visit)
            {
                log.WarnApiFailed(nameof(IOrderApi.StartCheckoutAsync), checkout.Status, checkout.ErrorCode);
                ShowError(ViewHelper.ErrorMessage(checkout));
                await LoadAsync();
                return;
            }

            visitState.Update(visit);
        }

        var result = await orderApi.CreatePaymentAsync(visitState.Id, new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = method, Amount = bill.Balance });
        if (result.Content is not { } created)
        {
            log.WarnApiFailed(nameof(IOrderApi.CreatePaymentAsync), result.Status, result.ErrorCode);
            ShowError(ViewHelper.ErrorMessage(result));
            return;
        }

        payment = created;
        QrValue = created.QrCode ?? string.Empty;
        SetStep(method == PaymentMethod.QrCode ? CheckoutStep.QrCode : CheckoutStep.CreditCard);

        StopWaiting();
        waiting = new CancellationTokenSource();
        _ = WaitPaymentAsync(created.Id, waiting.Token);
    }

    private async Task WaitPaymentAsync(Guid paymentId, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var result = await orderApi.GetPaymentAsync(paymentId, token);
            if (token.IsCancellationRequested || (result.Content is not { } current))
            {
                continue;
            }

            switch (current.Status)
            {
                case PaymentStatus.Completed:
                    payment = null;
                    await CompleteAsync(token);
                    return;
                case PaymentStatus.Failed:
                case PaymentStatus.Cancelled:
                    ShowError(AppResources.PaymentFailed);
                    SetStep(CheckoutStep.Method);
                    return;
            }
        }
    }

    // お礼と電子レシートを出し、しばらく操作がなければ待受に戻す
    private async Task CompleteAsync(CancellationToken token)
    {
        var receipt = await orderApi.GetReceiptAsync(visitState.Id, token);
        ReceiptValue = receipt.Content?.Url.ToString() ?? string.Empty;
        HasReceipt = ReceiptValue.Length > 0;
        SetStep(CheckoutStep.Completed);

        try
        {
            await Task.Delay(FinishAfter, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await FinishAsync();
    }

    // 待っている支払をやめて支払方法の選び直しに戻る。取り消す前に払い終わっていたら false
    private async Task<bool> ChangeMethodAsync()
    {
        StopWaiting();

        if (payment is { } current)
        {
            payment = null;

            var result = await orderApi.CancelPaymentAsync(current.Id);
            if (result.Content is { Status: PaymentStatus.Completed })
            {
                // お礼を出して待受に戻すまでの待ちは、コマンドの外で行う (画面の操作を止めないように)
                waiting = new CancellationTokenSource();
                _ = CompleteAsync(waiting.Token);
                return false;
            }
        }

        SetStep(CheckoutStep.Method);
        return true;
    }

    // 会計をやめて注文の画面に戻る (支払がなければ来店は注文できる状態に戻る)
    private async Task BackAsync()
    {
        if (IsWaiting && !await ChangeMethodAsync())
        {
            return;
        }

        if (visitState.Status == VisitStatus.Paying)
        {
            var result = await orderApi.CancelCheckoutAsync(visitState.Id);
            if (result.Content is { } visit)
            {
                visitState.Update(visit);
            }
            else
            {
                log.WarnApiFailed(nameof(IOrderApi.CancelCheckoutAsync), result.Status, result.ErrorCode);
            }
        }

        await Navigator.ForwardAsync(ViewId.Menu);
    }

    private async Task FinishAsync()
    {
        StopWaiting();
        orderUsecase.FinishVisit();
        await Navigator.ForwardAsync(ViewId.Standby);
    }

    private void StopWaiting()
    {
        if (waiting is not null)
        {
            waiting.Cancel();
            waiting.Dispose();
            waiting = null;
        }
    }

    //--------------------------------------------------------------------------------
    // Step
    //--------------------------------------------------------------------------------

    private void SetStep(CheckoutStep step)
    {
        IsLoading = step == CheckoutStep.Loading;
        IsMethod = step == CheckoutStep.Method;
        IsQrCode = step == CheckoutStep.QrCode;
        IsCreditCard = step == CheckoutStep.CreditCard;
        IsWaiting = step is CheckoutStep.QrCode or CheckoutStep.CreditCard;
        IsRegister = step == CheckoutStep.Register;
        CanChangeMethod = IsWaiting || IsRegister;
        IsCompleted = step == CheckoutStep.Completed;
        PanelTitle = step switch
        {
            CheckoutStep.QrCode => AppResources.MethodQrCode,
            CheckoutStep.CreditCard => AppResources.MethodCreditCard,
            CheckoutStep.Register => AppResources.MethodRegister,
            CheckoutStep.Completed => AppResources.CheckoutCompleted,
            _ => AppResources.CheckoutMethod
        };
    }

    private void ShowError(string message)
    {
        ErrorText = message;
        HasError = true;
    }

    //--------------------------------------------------------------------------------
    // Staff
    //--------------------------------------------------------------------------------

    // ブランドの印の長押しで、PIN を確かめてスタッフメニューに入る
    private async Task OpenStaffAsync()
    {
        if (await popupNavigator.VerifyStaffAsync(settings))
        {
            await Navigator.ForwardAsync(ViewId.Staff);
        }
    }
}
