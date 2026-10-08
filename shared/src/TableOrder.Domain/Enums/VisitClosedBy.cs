namespace TableOrder.Domain.Enums;

// 来店を終えたところ (TablePayment はテーブルで払い終えた、Register はレジで払った、Hall はスタッフが閉じた)
public enum VisitClosedBy
{
    TablePayment,
    Register,
    Hall
}
