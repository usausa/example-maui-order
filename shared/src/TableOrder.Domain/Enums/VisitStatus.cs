namespace TableOrder.Domain.Enums;

// 来店の状態 (Paying の間は注文を受け付けない)
public enum VisitStatus
{
    Open,
    Paying,
    Closed,
    Cancelled
}
