namespace TableOrder.HallApp.Modules.Stock;

// 品切れのタブ。中身はこれから作る (ヘッダとタブの帯は基底が持つ)
public sealed class StockViewModel : TabViewModelBase
{
    public StockViewModel(
        StoreState storeState,
        CallState callState,
        ServingState servingState)
        : base(ViewId.Stock, storeState, callState, servingState)
    {
    }
}
