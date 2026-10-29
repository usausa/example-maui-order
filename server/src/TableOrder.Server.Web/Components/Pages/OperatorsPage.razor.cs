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

    private Task AddAsync() =>
        RunOnceAsync(async () =>
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
        });

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

        Snackbar.Add($"{editName.Trim()} を変更しました", Severity.Success);
        await LoadAsync();
    }

    private async Task ResetPasswordAsync()
    {
        if (editing is not { } item ||
            !await DialogService.ConfirmAsync("仮パスワードの再発行", $"{item.Name} の仮パスワードを再発行しますか？ 現在のパスワードは使えなくなります", "再発行"))
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
            !await DialogService.ConfirmAsync("多要素認証の解除", $"{item.Name} の多要素認証を解除しますか？ パスワードだけでサインインできるようになります", "解除"))
        {
            return;
        }

        var error = await OperatorService.ResetTwoFactorAsync(item.Id, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add($"{item.Name} の多要素認証を解除しました", Severity.Success);
        await LoadAsync();
    }

    private async Task SetActiveAsync(bool isActive)
    {
        if (editing is not { } item)
        {
            return;
        }

        if (!isActive && !await DialogService.ConfirmAsync("運営者の停止", $"{item.Name} を停止しますか？ サインインできなくなります", "停止"))
        {
            return;
        }

        var error = await OperatorService.SetActiveAsync(item.Id, isActive, item.Version, Scope.UserId, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add(isActive ? $"{item.Name} を再開しました" : $"{item.Name} を停止しました", Severity.Success);
        await LoadAsync();
    }

    //--------------------------------------------------------------------------------
    // Display
    //--------------------------------------------------------------------------------

    private string StatusText(AdminUserSummaryEntity user) => AdminNames.UserStatusName(user, TimeProvider.GetUtcNow());
}
