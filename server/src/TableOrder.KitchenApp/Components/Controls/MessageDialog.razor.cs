namespace TableOrder.KitchenApp.Components.Controls;

// 知らせ (失敗など)。OK で閉じる
public sealed partial class MessageDialog
{
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public string Message { get; set; } = default!;

    [Parameter]
    public EventCallback OnClose { get; set; }
}
