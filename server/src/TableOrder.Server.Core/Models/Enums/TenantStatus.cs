namespace TableOrder.Server.Core.Models.Enums;

// テナントの契約の状態 (Active のほかにはトークンを出さない)
public enum TenantStatus
{
    Active,
    Suspended,
    Closed
}
