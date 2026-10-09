namespace TableOrder.Server.Web.Application;

// 管理画面の認可のポリシー (役割で開ける画面を絞る。確かめ方は管理画面の Cookie に限る)
public static class AdminPolicies
{
    // サインインした利用者 (役割を問わない)
    public const string SignedIn = "Admin" + nameof(SignedIn);

    // テナントの管理者と運営者
    public const string TenantAdmin = "Admin" + nameof(TenantAdmin);

    // 運営者
    public const string Operator = "Admin" + nameof(Operator);
}
