namespace TableOrder.Server.Web.Application.Account;

// 管理画面のサインインのクレームの名前 (利用者の id は端末と同じ sub、テナントは端末と同じ tenant_id にする)
public static class AdminClaimNames
{
    public const string Role = "admin_role";

    // 店舗の担当が受け持つ店舗 (店舗ごとに 1 つ)
    public const string StoreId = "admin_store_id";

    // 画面に出す名前
    public const string DisplayName = "admin_name";

    // 仮のパスワードでサインインした (パスワードを替えるまで、ほかの画面を開かない)
    public const string MustChangePassword = "admin_must_change_password";
}
