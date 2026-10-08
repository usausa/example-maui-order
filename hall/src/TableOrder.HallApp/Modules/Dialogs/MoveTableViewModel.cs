namespace TableOrder.HallApp.Modules.Dialogs;

// 移れる席 (空いている席)
public sealed record MoveTableParameter(IReadOnlyList<TableChoice> Tables);

// 席の選択肢 (テーブルの名前と定員)
public sealed record TableChoice(Guid Id, string Name, string CapacityText);

// 移る席を空いている席から選ぶ。選んだテーブルの id を返す (閉じたら null)
public sealed partial class MoveTableViewModel : AppDialogViewModelBase, IPopupInitialize<MoveTableParameter>
{
    [ObservableProperty]
    public partial IReadOnlyList<TableChoice> Tables { get; set; } = [];

    // 空いている席がない
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public MoveTableViewModel(IPopupNavigator popupNavigator)
    {
        // 結果は開く側と同じ型 (Guid?) で返す
        SelectCommand = MakeAsyncCommand<TableChoice>(async x => await popupNavigator.CloseAsync<Guid?>(x.Id));
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<Guid?>(null));
    }

    public void Initialize(MoveTableParameter parameter)
    {
        Tables = parameter.Tables;
        IsEmpty = parameter.Tables.Count == 0;
    }
}
