namespace TableOrder.Server.Web.Application;

// 認可のポリシー (端末の種類で使える API を絞る)
public static class Policies
{
    // 端末 (種類を問わない)
    public const string AnyDevice = nameof(AnyDevice);

    // メニューと品切れを読む端末 (テーブル、ホール、キッチン)
    public const string MenuReader = nameof(MenuReader);

    // 品切れを変える端末 (ホール、キッチン)
    public const string StockWriter = nameof(StockWriter);

    // テーブルの一覧を読む端末 (ホール、受付)
    public const string TableReader = nameof(TableReader);

    // 来店を開く端末 (ホール、受付、テーブル。受付とテーブルは来店の開き方で許した店だけで、Service が確かめる)
    public const string VisitOpener = nameof(VisitOpener);

    // 来店を読む端末と、確認のルールの記録と注文を入れる端末 (テーブル、ホール)
    public const string VisitReader = nameof(VisitReader);

    public const string TableDevice = nameof(TableDevice);

    public const string HallDevice = nameof(HallDevice);

    public const string KitchenDevice = nameof(KitchenDevice);
}
