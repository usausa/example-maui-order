namespace TableOrder.Server.Web.Application;

// 認可のポリシー (端末の種類で使える API を絞る)
public static class Policies
{
    // 端末 (種類を問わない)
    public const string AnyDevice = nameof(AnyDevice);

    // メニューと品切れを読む端末 (テーブル、ホール、キッチン)
    public const string MenuReader = nameof(MenuReader);
}
