namespace TableOrder.Terminal.Table.Modules.Checkout;

// お会計。左に明細と合計・割り勘の目安、右に支払方法を選んでから QR コード決済 / カード / レジの案内を出す
// 支払の完了は読み直して待ち (サーバの通知ができたら通知で替える)、終わったらお礼と電子レシートを出して待受に戻る
// 割り勘は人数で割った 1 人分ずつ払い、残りがなくなるまで支払方法の選び直しに戻る
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

    private readonly ITableApi tableApi;

    private readonly OrderUsecase orderUsecase;

    private CancellationTokenSource? waiting;

    private BillResponse? bill;

    private PaymentResponse? payment;

    // 割り勘の人数 (1 は残りをまとめて払う) と、今回払う額
    private int splitCount = 1;

    private decimal payAmount;

    // 待っている支払が割り勘の 1 人分 (払い終えても残りがある)
    private bool partialPayment;

    private bool finished;

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

    [ObservableProperty]
    public partial bool HasPaid { get; set; }

    [ObservableProperty]
    public partial string PaidText { get; set; } = string.Empty;

    // 支払

    [ObservableProperty]
    public partial string PanelTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PayCaption { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PayAmountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanSplit { get; set; }

    [ObservableProperty]
    public partial string SplitCountText { get; set; } = string.Empty;

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

    // 一部を払ったあとは注文の画面に戻らない (残りを払うまで会計を続ける)
    [ObservableProperty]
    public partial bool CanBack { get; set; }

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

    [ObservableProperty]
    public partial bool HasNotice { get; set; }

    [ObservableProperty]
    public partial string NoticeText { get; set; } = string.Empty;

    public IObserveCommand SplitDecreaseCommand { get; }

    public IObserveCommand SplitIncreaseCommand { get; }

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
        ITableApi tableApi,
        OrderUsecase orderUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.settings = settings;
        this.tableApi = tableApi;
        this.visitState = visitState;
        this.languageState = languageState;
        this.orderUsecase = orderUsecase;

        TableText = ViewHelper.Table(settings.TableNo);
        GuestsText = ViewHelper.Guests(visitState.Guests);
        CanUseQrCode = menuState.Config.PaymentMethods.Contains(PaymentMethod.QrCode);
        CanUseCreditCard = menuState.Config.PaymentMethods.Contains(PaymentMethod.CreditCard);
        RegisterMessage = ViewHelper.Format(AppResources.RegisterMessageFormat, TableText);

        SplitDecreaseCommand = MakeDelegateCommand(() => ChangeSplit(splitCount - 1), () => IsMethod && (splitCount > 1));
        SplitIncreaseCommand = MakeDelegateCommand(() => ChangeSplit(splitCount + 1), () => IsMethod && (splitCount < (bill?.Guests ?? 1)));
        QrCodeCommand = MakeAsyncCommand(() => PayAsync(PaymentMethod.QrCode), () => IsMethod);
        CreditCardCommand = MakeAsyncCommand(() => PayAsync(PaymentMethod.CreditCard), () => IsMethod);
        RegisterCommand = MakeDelegateCommand(() => SetStep(CheckoutStep.Register), () => IsMethod);
        ChangeMethodCommand = MakeAsyncCommand(ChangeMethodAsync, () => CanChangeMethod);
        BackCommand = MakeAsyncCommand(BackAsync, () => CanBack);
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
    // コマンドの外で API を待つので、その間は Busy にして画面のボタンと重ならないようにする
    protected override async Task OnNotifyBackAsync()
    {
        using (BusyState.Begin())
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
    }

    // レジで払い終えたなど、この画面の外で来店が閉じたらお礼を出す (この画面で払い終えたときはお礼を出している)
    protected override Task OnVisitClosedAsync()
    {
        if (!IsCompleted)
        {
            StopWaiting();
            payment = null;
            waiting = new CancellationTokenSource();
            _ = CompleteAsync(waiting.Token);
        }

        return Task.CompletedTask;
    }

    //--------------------------------------------------------------------------------
    // Bill
    //--------------------------------------------------------------------------------

    private async Task LoadAsync()
    {
        SetStep(CheckoutStep.Loading);

        var result = await tableApi.GetBillAsync(visitState.Id);
        if (result.Content is not { } content)
        {
            log.WarnApiFailed(nameof(ITableApi.GetBillAsync), result.Status, result.ErrorCode);
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
        HasPaid = content.PaidAmount > 0;
        CanBack = !IsCompleted && !HasPaid;
        PaidText = ViewHelper.Format(AppResources.CheckoutPaidFormat, ViewHelper.Price(content.PaidAmount));

        CanSplit = (content.Guests > 1) && (content.Balance > 0);
        ChangeSplit(splitCount);
    }

    //--------------------------------------------------------------------------------
    // Split
    //--------------------------------------------------------------------------------

    // 割り勘のときは、残りを人数で割った 1 人分 (割り切れない分は先に払う方に足す) を払う
    private void ChangeSplit(int count)
    {
        var balance = bill?.Balance ?? 0;
        splitCount = Math.Clamp(count, 1, Math.Max(1, bill?.Guests ?? 1));
        payAmount = splitCount > 1 ? Pricing.Split(balance, splitCount)[0] : balance;

        PayCaption = splitCount > 1 ? AppResources.CheckoutPayPerPerson : AppResources.CheckoutTotal;
        PayAmountText = ViewHelper.Price(payAmount);
        SplitCountText = splitCount > 1 ? ViewHelper.Format(AppResources.CheckoutSplitCountFormat, splitCount) : AppResources.CheckoutSplitNone;
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
        HasNotice = false;

        if (visitState.Status != VisitStatus.Paying)
        {
            var checkout = await tableApi.StartCheckoutAsync(visitState.Id, new CheckoutRequest { BillVersion = bill.BillVersion, Version = visitState.Version });
            if (checkout.Content is not { } visit)
            {
                log.WarnApiFailed(nameof(ITableApi.StartCheckoutAsync), checkout.Status, checkout.ErrorCode);
                ShowError(ViewHelper.ErrorMessage(checkout));
                await LoadAsync();
                return;
            }

            visitState.Update(visit);
        }

        var result = await tableApi.CreatePaymentAsync(visitState.Id, new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = method, Amount = payAmount });
        if (result.Content is not { } created)
        {
            log.WarnApiFailed(nameof(ITableApi.CreatePaymentAsync), result.Status, result.ErrorCode);
            ShowError(ViewHelper.ErrorMessage(result));
            return;
        }

        payment = created;
        partialPayment = payAmount < bill.Balance;
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

            var result = await tableApi.GetPaymentAsync(paymentId, token);
            if (token.IsCancellationRequested || (result.Content is not { } current))
            {
                continue;
            }

            switch (current.Status)
            {
                case PaymentStatus.Completed:
                    await PaidAsync(token);
                    return;
                case PaymentStatus.Failed:
                case PaymentStatus.Cancelled:
                    payment = null;
                    ShowError(AppResources.PaymentFailed);
                    SetStep(CheckoutStep.Method);
                    return;
            }
        }
    }

    // 払い終えた支払が割り勘の 1 人分なら、明細を読み直して次の方の支払方法を選ぶ。残りがなければお礼を出す
    // 支払の完了は読み直しと選び直し (取り消す前に払い終わっていた) の両方から来るが、選び直しは読み直しを止めてから取り消すので 1 回になる
    // 明細を待つ前に戻るを止め、払い終えたあとに注文の画面へ戻らないようにする
    private async Task PaidAsync(CancellationToken token)
    {
        payment = null;
        if (!partialPayment)
        {
            await CompleteAsync(token);
            return;
        }

        HasPaid = true;
        CanBack = false;
        SetStep(CheckoutStep.Loading);

        var result = await tableApi.GetBillAsync(visitState.Id, token);
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (result.Content is { } content)
        {
            splitCount--;
            ApplyBill(content);
        }
        else
        {
            log.WarnApiFailed(nameof(ITableApi.GetBillAsync), result.Status, result.ErrorCode);
        }

        if (bill is { Balance: <= 0 })
        {
            await CompleteAsync(token);
            return;
        }

        NoticeText = AppResources.PaymentPartDone;
        HasNotice = true;
        SetStep(CheckoutStep.Method);
    }

    // お礼と電子レシートを出し、しばらく操作がなければ待受に戻す
    // 完了は読み直しと選び直し (取り消す前に払い終わっていた) と来店が閉じた知らせから来るので、先の 1 回だけ行う
    // レシートを待つ前にお礼の画面にし、払い終わったあとに戻る・選び直しを受け付けない (注文の画面に戻らないように)
    private async Task CompleteAsync(CancellationToken token)
    {
        if (IsCompleted)
        {
            return;
        }

        SetStep(CheckoutStep.Completed);

        // 割り勘で分けて払ったときも、払い終えたら残りと払った額ではなく明細の合計を出す (分けずに払ったときと同じ)
        if (bill is not null)
        {
            TotalText = ViewHelper.Price(bill.Total);
        }

        HasPaid = false;

        var receipt = await tableApi.GetReceiptAsync(visitState.Id, token);
        if (token.IsCancellationRequested)
        {
            return;
        }

        ReceiptValue = receipt.Content?.Url.ToString() ?? string.Empty;
        HasReceipt = ReceiptValue.Length > 0;

        try
        {
            await Task.Delay(FinishAfter, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // 操作の途中 (スタッフメニューの PIN など) なら待受に戻さない
        if (BusyState.IsBusy)
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

            var result = await tableApi.CancelPaymentAsync(current.Id);
            if (result.Content is { Status: PaymentStatus.Completed })
            {
                // 払い終えたあとの待ち (次の方の支払、お礼から待受に戻る) は、コマンドの外で行う (画面の操作を止めないように)
                waiting = new CancellationTokenSource();
                _ = PaidAsync(waiting.Token);
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
            var result = await tableApi.CancelCheckoutAsync(visitState.Id);
            if (result.Content is { } visit)
            {
                visitState.Update(visit);
            }
            else
            {
                log.WarnApiFailed(nameof(ITableApi.CancelCheckoutAsync), result.Status, result.ErrorCode);
            }
        }

        await Navigator.ForwardAsync(ViewId.Menu);
    }

    // 待受に戻すのは、お礼のタイマーと操作 (閉じる、端末の戻る) の両方から来るので、先の 1 回だけ行う
    // (タイマーの待ちが終わった直後の操作では、待ちを止めても続きが動く)
    private async Task FinishAsync()
    {
        if (finished)
        {
            return;
        }

        finished = true;
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
        CanBack = !IsCompleted && !HasPaid;
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
