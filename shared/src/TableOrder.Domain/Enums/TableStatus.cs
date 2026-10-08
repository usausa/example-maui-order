namespace TableOrder.Domain.Enums;

// テーブルの状態 (席の一覧の絞り込み)。来店がなければ Vacant、会計中の来店は Paying、ほかの来店は Occupied
public enum TableStatus
{
    Vacant,
    Occupied,
    Paying
}
