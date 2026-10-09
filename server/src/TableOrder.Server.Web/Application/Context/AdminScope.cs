namespace TableOrder.Server.Web.Application.Context;

using TableOrder.Server.Web.Application.Account;
using TableOrder.Server.Web.Application.Authentication;

// 管理画面の利用者と扱える範囲 (回線単位)。サインインのクレーム (役割、テナント、受け持つ店舗) から作る
public sealed class AdminScope
{
    public bool IsInitialized { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AdminRole Role { get; private set; }

    // 属するテナント (運営者は null)
    public Guid? TenantId { get; private set; }

    // 店舗の担当が受け持つ店舗
    public IReadOnlyList<Guid> StoreIds { get; private set; } = [];

    public bool MustChangePassword { get; private set; }

    public bool IsOperator => Role == AdminRole.Operator;

    // チェーンの設定・店舗の追加・利用者の管理ができる (テナントの管理者と運営者)
    public bool CanManageTenant => Role is AdminRole.Operator or AdminRole.TenantAdmin;

    // クレームが足りない (サインインしていない) ときは、どのテナントも扱えないままにする
    public void Initialize(ClaimsPrincipal user)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimNames.Subject), out var userId) ||
            !Enum.TryParse<AdminRole>(user.FindFirstValue(AdminClaimNames.Role), out var role))
        {
            return;
        }

        UserId = userId;
        Name = user.FindFirstValue(AdminClaimNames.DisplayName) ?? string.Empty;
        Role = role;
        TenantId = Guid.TryParse(user.FindFirstValue(ClaimNames.TenantId), out var tenantId) ? tenantId : null;
        StoreIds = user.FindAll(AdminClaimNames.StoreId).Select(static x => Guid.Parse(x.Value)).ToList();
        MustChangePassword = user.HasClaim(static x => x.Type == AdminClaimNames.MustChangePassword);
        IsInitialized = (role == AdminRole.Operator) || (TenantId is not null);
    }

    public bool CanUseTenant(Guid tenantId) =>
        IsInitialized && (IsOperator || (TenantId == tenantId));

    public bool CanUseStore(Guid tenantId, Guid storeId) =>
        CanUseTenant(tenantId) && ((Role != AdminRole.StoreStaff) || StoreIds.Contains(storeId));
}
