namespace TableOrder.Server.Core.Models.Enums;

// 管理画面の利用者の役割 (運営者はテナントに属さず、店舗の担当は受け持つ店舗だけを扱う)
public enum AdminRole
{
    Operator,
    TenantAdmin,
    StoreStaff
}
