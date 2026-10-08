namespace TableOrder.Domain.Enums;

// 来店の開き方 (店舗の設定)。Hall はスタッフが案内して開き、Reception は受付機でお客様が人数を入れてサーバが席を決め、Table はお客様が座った席のテーブル端末で始める
// どの形でもホール端末からは開ける (席の決め直しや、困ったときのため)
public enum VisitOpening
{
    Hall,
    Reception,
    Table
}
