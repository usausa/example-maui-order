namespace TableOrder.HallApp.Modules.Dialogs;

// 会計の明細で選んだ操作
public enum BillAction
{
    StartCheckout,
    CancelCheckout
}

// 表題、会計の明細と、来店が会計中か (会計を始めるか取りやめるかのどちらかだけを出す)
public sealed record BillParameter(string Title, BillResponse Bill, bool IsPaying);

// 明細の行 (品、オプション、数量、金額)
public sealed record BillLineItem(string Name, string OptionText, string QuantityText, string AmountText)
{
    public bool HasOptions => OptionText.Length > 0;
}

// 税率ごとの内税
public sealed record BillTaxItem(string Name, string AmountText);

// 会計の手伝い。明細、内税、合計、払った額、残り、まだ出していない品の注意を出し、会計を始めるか取りやめるかを返す (閉じたら null)
public sealed partial class BillViewModel : AppDialogViewModelBase, IPopupInitialize<BillParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<BillLineItem> Lines { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<BillTaxItem> Taxes { get; set; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    // 支払の一部を払い終えている (払った額と残りを出す)
    [ObservableProperty]
    public partial bool HasPaid { get; set; }

    [ObservableProperty]
    public partial string PaidText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BalanceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasUnserved { get; set; }

    [ObservableProperty]
    public partial bool IsPaying { get; set; }

    // 会計を取りやめられる (払い終えた支払のない会計中)
    [ObservableProperty]
    public partial bool CanCancelCheckout { get; set; }

    // 払い終えた支払があるので取りやめられない (サーバは会計中のまま返す)。取りやめを出さずに理由を出し、戻るを幅いっぱいにする
    [ObservableProperty]
    public partial bool IsCancelBlocked { get; set; }

    [ObservableProperty]
    public partial int BackColumnSpan { get; set; } = 1;

    public IObserveCommand StartCommand { get; }

    public IObserveCommand CancelCheckoutCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public BillViewModel(IPopupNavigator popupNavigator)
    {
        // 結果は開く側と同じ型 (BillAction?) で返す。明細のない来店は会計を始めない
        StartCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<BillAction?>(BillAction.StartCheckout), () => !IsEmpty);
        CancelCheckoutCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<BillAction?>(BillAction.CancelCheckout));
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<BillAction?>(null));
    }

    public void Initialize(BillParameter parameter)
    {
        var bill = parameter.Bill;
        Title = parameter.Title;
        Lines = bill.Lines
            .Select(static x => new BillLineItem(
                ViewHelper.Text(x.Name),
                String.Join(" / ", x.Options.Select(ViewHelper.Text)),
                $"× {x.Quantity}",
                ViewHelper.Price(x.Amount)))
            .ToList();
        Taxes = bill.Taxes
            .Select(static x => new BillTaxItem(ViewHelper.Format(AppResources.BillTaxFormat, ViewHelper.Percent(x.Rate)), ViewHelper.Price(x.TaxAmount)))
            .ToList();
        IsEmpty = bill.Lines.Count == 0;
        TotalText = ViewHelper.Price(bill.Total);
        HasPaid = bill.PaidAmount > 0;
        PaidText = ViewHelper.Price(bill.PaidAmount);
        BalanceText = ViewHelper.Price(bill.Balance);
        HasUnserved = bill.HasUnservedLines;
        IsPaying = parameter.IsPaying;
        CanCancelCheckout = IsPaying && !HasPaid;
        IsCancelBlocked = IsPaying && HasPaid;
        BackColumnSpan = IsCancelBlocked ? 2 : 1;
    }
}
