namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

using MudBlazor;

using TableOrder.Server.Web.Application.Account;
using TableOrder.Server.Web.Application.Context;

// テナントの利用者の管理 (選んだテナントの利用者の一覧、追加、役割と受け持つ店舗の変更、仮のパスワード、多要素を外す、止める)
// 仮のパスワードはこの画面で作ってハッシュにし、平文は出した直後に一度だけ見せる
public sealed partial class UsersPage : IDisposable
{
    private const string UtcName = "UTC";

    private static readonly AdminRole[] Roles = [AdminRole.TenantAdmin, AdminRole.StoreStaff];

    private List<AdminUserListItem> users = [];

    private List<StoreEntity> stores = [];

    private string addEmail = string.Empty;

    private string addName = string.Empty;

    private AdminRole addRole = AdminRole.StoreStaff;

    private IReadOnlyCollection<Guid> addStoreIds = [];

    private IssuedPassword? issued;

    private AdminUserListItem? editing;

    private string editName = string.Empty;

    private AdminRole editRole;

    private IReadOnlyCollection<Guid> editStoreIds = [];

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required TimeProvider TimeProvider { get; set; }

    [Inject]
    public required AdminScope Scope { get; set; }

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required UserManager<AdminUserEntity> UserManager { get; set; }

    [Inject]
    public required TenantService TenantService { get; set; }

    [Inject]
    public required AdminUserService AdminUserService { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override Task OnInitializedAsync()
    {
        Selection.Changed += OnSelectionChanged;
        return LoadAsync();
    }

    public void Dispose() => Selection.Changed -= OnSelectionChanged;

    // テナントを選び直したら、出した仮のパスワードと前のテナントの店舗を指す入力 (受け持つ店舗) を消して読み直す (店舗の選択の操作の中から呼ばれるので、文脈を始め直す)
    private void OnSelectionChanged(object? sender, EventArgs e) =>
        _ = ReloadAsync(() =>
        {
            issued = null;
            addStoreIds = [];
            return LoadAsync();
        });

    private async Task LoadAsync()
    {
        editing = null;
        if (Selection.TenantId is not { } tenantId)
        {
            users = [];
            stores = [];
            return;
        }

        stores = await TenantService.GetStoreAllAsync(tenantId, CancellationToken.None);
        users = await AdminUserService.GetListAsync(CancellationToken.None);
    }

    //--------------------------------------------------------------------------------
    // Add
    //--------------------------------------------------------------------------------

    private Task AddAsync() =>
        RunOnceAsync(async () =>
        {
            var password = TemporaryPassword.Create();
            var result = await AdminUserService.AddAsync(
                new AdminUserInput(addEmail, UserManager.NormalizeName(addEmail.Trim()), addName, addRole, addStoreIds.ToList()),
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
            addStoreIds = [];
            await LoadAsync();
        });

    //--------------------------------------------------------------------------------
    // Edit
    //--------------------------------------------------------------------------------

    private void Edit(AdminUserListItem item)
    {
        editing = item;
        editName = item.User.Name;
        editRole = item.User.Role;
        editStoreIds = item.StoreIds;
    }

    private void CancelEdit() => editing = null;

    private bool IsSelf(AdminUserListItem item) => item.User.Id == Scope.UserId;

    private async Task SaveAsync()
    {
        if (editing is not { } item)
        {
            return;
        }

        var error = await AdminUserService.UpdateAsync(item.User.Id, editName, editRole, editStoreIds.ToList(), item.User.Version, Scope.UserId, CancellationToken.None);
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
            !await DialogService.ConfirmAsync("仮のパスワードを出し直す", $"{item.User.Name} のパスワードを仮のパスワードに替えます。開いている管理画面はサインインからやり直し、次のサインインでパスワードを替えます。", "出し直す"))
        {
            return;
        }

        var password = TemporaryPassword.Create();
        var error = await AdminUserService.ResetPasswordAsync(item.User.Id, UserManager.PasswordHasher.HashPassword(new AdminUserEntity(), password), CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        issued = new IssuedPassword(item.User.Email, password);
        await LoadAsync();
    }

    private async Task ResetTwoFactorAsync()
    {
        if (editing is not { } item ||
            !await DialogService.ConfirmAsync("多要素を外す", $"{item.User.Name} の認証アプリの登録を外します。次からはパスワードだけでサインインし、利用者が登録し直します。", "外す"))
        {
            return;
        }

        var error = await AdminUserService.ResetTwoFactorAsync(item.User.Id, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add($"{item.User.Name} の多要素を外しました", Severity.Success);
        await LoadAsync();
    }

    private async Task SetActiveAsync(bool isActive)
    {
        if (editing is not { } item)
        {
            return;
        }

        if (!isActive && !await DialogService.ConfirmAsync("利用者を止める", $"{item.User.Name} を止めます。サインインできなくなり、開いている管理画面はサインインからやり直します。", "止める"))
        {
            return;
        }

        var error = await AdminUserService.SetActiveAsync(item.User.Id, isActive, item.User.Version, Scope.UserId, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add(isActive ? $"{item.User.Name} を戻しました" : $"{item.User.Name} を止めました", Severity.Success);
        await LoadAsync();
    }

    //--------------------------------------------------------------------------------
    // Display
    //--------------------------------------------------------------------------------

    private string StoreName(Guid id) =>
        stores.FirstOrDefault(x => x.Id == id) is { } store ? $"{store.Name.Ja} ({store.Code})" : id.ToString("D");

    private string StoresText(AdminUserListItem item) =>
        item.User.Role == AdminRole.StoreStaff ? String.Join("、", item.StoreIds.Select(StoreName)) : "すべて";

    private string StatusText(AdminUserSummaryEntity user) => AdminNames.UserStatusName(user, TimeProvider.GetUtcNow());

    // 時刻を出すタイムゾーン。テナントの店舗が 1 つのタイムゾーンにそろっていればそれ、そろっていない (店舗がない) なら UTC にし、見出しに出す
    private string TimeZoneName =>
        stores.Select(static x => x.TimeZone).Distinct().ToList() is [var single] ? single : UtcName;

    private string FormatTime(DateTimeOffset value) =>
        (TimeZoneName == UtcName ? value.UtcDateTime : StoreHours.LocalDateTime(value, TimeZoneName)).ToString("M/d HH:mm", CultureInfo.InvariantCulture);
}
