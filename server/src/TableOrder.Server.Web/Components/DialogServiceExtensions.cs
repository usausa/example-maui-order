namespace TableOrder.Server.Web.Components;

using MudBlazor;

public static class DialogServiceExtensions
{
    // 操作を確かめる (やめるを選ぶか閉じたら false)
    public static async Task<bool> ConfirmAsync(this IDialogService dialog, string title, string message, string yesText) =>
        await dialog.ShowMessageBoxAsync(title, message, yesText: yesText, cancelText: "やめる") == true;
}
