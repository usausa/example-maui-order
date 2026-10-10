namespace TableOrder.KitchenApp.Components.Controls;

using System.Globalization;

using TableOrder.KitchenApp.Components.Pages;

// 品の状態を選ぶ。今の状態に印を付け、売れる・残りの数を決める・品切れから選ぶ
// 残りの数は続けて数のボタンで入れる (0 はサーバが品切れにする)
public sealed partial class StockEditDialog
{
    // 残りの数の桁 (サーバが受ける上限の桁)
    private static readonly int RemainingDigits = Length.MaxStockRemaining.ToString(CultureInfo.InvariantCulture).Length;

    private bool isEnteringRemaining;

    private string remaining = string.Empty;

    [Parameter]
    [EditorRequired]
    public string Name { get; set; } = default!;

    [Parameter]
    public string Caption { get; set; } = string.Empty;

    // 今の状態 (売れる品は null)
    [Parameter]
    public StockResponseItem? Stock { get; set; }

    [Parameter]
    public EventCallback<StockDecision> OnDecide { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private StockStatus CurrentStatus => Stock?.Status ?? StockStatus.Available;

    private string CurrentText => ViewHelper.StockName(CurrentStatus, Stock?.Remaining);

    private Task DecideAsync(StockStatus status) =>
        OnDecide.InvokeAsync(new StockDecision(status, null));

    // 残りの数を入れる (今の残りの数から始める)
    private void StartRemaining()
    {
        remaining = Stock?.Remaining?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        isEnteringRemaining = true;
    }

    private void ChangeRemaining(string value) => remaining = value;

    private Task DecideRemainingAsync() =>
        Int32.TryParse(remaining, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? OnDecide.InvokeAsync(new StockDecision(StockStatus.Limited, value))
            : Task.CompletedTask;
}
