namespace TableOrder.Domain.Enums;

// 来店を開いたところ (Hall はスタッフ。ホール端末と管理画面の案内。Reception と Table は来店の開き方で許した店だけ)
public enum VisitOpenedBy
{
    Hall,
    Reception,
    Table
}
