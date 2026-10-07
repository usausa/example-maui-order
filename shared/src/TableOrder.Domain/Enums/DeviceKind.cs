namespace TableOrder.Domain.Enums;

// 端末の種類 (使える API の範囲と、受ける通知が種類で決まる)
public enum DeviceKind
{
    Table,
    Hall,
    Kitchen,
    Reception
}
