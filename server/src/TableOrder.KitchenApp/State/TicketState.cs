namespace TableOrder.KitchenApp.State;

// まだ下げていないチケット (受け持つすべての持ち場) と、直近に下げたチケット。起動で読み、チケットの通知で読み直す
// 選んだ持ち場のタブは、ほかの画面から戻っても保つ
public sealed class TicketState
{
    // 選んだ持ち場 (null はすべて)
    public Guid? SelectedStationId { get; set; }

    public IReadOnlyList<KitchenTicketListResponseItem> Open { get; private set; } = [];

    public IReadOnlyList<KitchenTicketListResponseItem> Done { get; private set; } = [];

    public void UpdateOpen(KitchenTicketListResponse tickets) => Open = tickets.Items;

    public void UpdateDone(KitchenTicketListResponse tickets) => Done = tickets.Items;
}
