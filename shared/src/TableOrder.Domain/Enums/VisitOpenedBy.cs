namespace TableOrder.Domain.Enums;

// 来店を開いたところ (Hall はスタッフ。ホール端末と管理画面の案内。テーブル端末からは開かない)
public enum VisitOpenedBy
{
    Hall,
    Reception
}
