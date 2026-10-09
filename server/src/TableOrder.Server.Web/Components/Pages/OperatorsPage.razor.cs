namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

using MudBlazor;

using TableOrder.Server.Web.Application.Account;
using TableOrder.Server.Web.Application.Context;

// 運営者の管理 (一覧、追加、名前の変更、仮のパスワード、多要素を外す、止める)。運営者だけが開ける
// 仮のパスワードはこの画面で作ってハッシュにし、平文は出した直後に一度だけ見せる
public sealed partial class OperatorsPage
{
    // 出した仮のパスワード (この画面を離れるまで見せる)
    private sealed record IssuedPassword(string Email, string Password);

    private List<AdminUserSummaryEntity> operators = [];

    private string addEmail = string.Empty;

    private string addName = string.Empty;

    private IssuedPassword? issued;

    private AdminUserSummaryEntity? editing;

    private string editName = string.Empty;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required TimeProvider TimeProvider { get; set; }

    [Inject]
    public required AdminScope Scope { get; set; }

    [Inject]
    public required UserManager<AdminUserEntity> UserManager { get; set; }

    [Inject]
    public required OperatorService OperatorService { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        editing = null;
        operators = await OperatorService.GetListAsync(CancellationToken.None);
    }

    //--------------------------------------------------------------------------------
    // Add
    //--------------------------------------------------------------------------------

    private async Task AddAsync()
    {
        var password = TemporaryPassword.Create();
        var result = await OperatorService.AddAsync(
            addEmail,
            UserManager.NormalizeName(addEmail.Trim()),
            addName,
            UserManager.PasswordHasher.HashPassword(new AdminUserEntity(), password),
            CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(AdminNames.ErrorMessage(result.Error), Severity.Error);
            return;
        }

        issued = new IssuedPassword(result.Value.Email, password);
        addEmail = string.Empty;
        addName = string.Empty;
        await LoadAsync();
    }

    //--------------------------------------------------------------------------------
    // Edit
    //--------------------------------------------------------------------------------

    private void Edit(AdminUserSummaryEntity item)
    {
        editing = item;
        editName = item.Name;
    }

    private void CancelEdit() => editing = null;

    private async Task SaveAsync()
    {
        if (editing is not { } item)
        {
            return;
        }

        var error = await OperatorService.UpdateAsync(item.Id, editName, item.Version, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add($"{editName.Trim()} を替えました", Severity.Success);
        await LoadAsync();
    }

    private async Task ResetPasswordAsync()
    {
        if (editing is not { } item ||
            !await ConfirmAsync("仮のパスワードを出し直す", $"{item.Name} のパスワードを仮のパスワードに替えます。開いている管理画面はサインインからやり直し、次のサインインでパスワードを替えます。", "出し直す"))
        {
            return;
        }

        var password = TemporaryPassword.Create();
        var error = await OperatorService.ResetPasswordAsync(item.Id, UserManager.PasswordHasher.HashPassword(new AdminUserEntity(), password), CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        issued = new IssuedPassword(item.Email, password);
        await LoadAsync();
    }

    private async Task ResetTwoFactorAsync()
    {
        if (editing is not { } item ||
            !await ConfirmAsync("多要素を外す", $"{item.Name} の認証アプリの登録を外します。次からはパスワードだけでサインインし、本人が登録し直します。", "外す"))
        {
            return;
        }

        var error = await OperatorService.ResetTwoFactorAsync(item.Id, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add($"{item.Name} の多要素を外しました", Severity.Success);
        await LoadAsync();
    }

    private async Task SetActiveAsync(bool isActive)
    {
        if (editing is not { } item)
        {
            return;
        }

        if (!isActive && !await ConfirmAsync("運営者を止める", $"{item.Name} を止めます。サインインできなくなり、開いている管理画面はサインインからやり直します。", "止める"))
        {
            return;
        }

        var error = await OperatorService.SetActiveAsync(item.Id, isActive, item.Version, Scope.UserId, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add(isActive ? $"{item.Name} を戻しました" : $"{item.Name} を止めました", Severity.Success);
        await LoadAsync();
    }

    private async Task<bool> ConfirmAsync(string title, string message, string yesText) =>
        await DialogService.ShowMessageBoxAsync(title, message, yesText: yesText, cancelText: "やめる") == true;

    //--------------------------------------------------------------------------------
    // Display
    //--------------------------------------------------------------------------------

    private string StatusText(AdminUserSummaryEntity user) =>
        !user.IsActive ? "止めた" :
        user.LockoutEnd > TimeProvider.GetUtcNow() ? "間違えたため止めている" :
        user.MustChangePassword ? "仮のパスワード" :
        "使える";
}
