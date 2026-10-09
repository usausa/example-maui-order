namespace TableOrder.Server.Web.Application;

// サーバに同梱するファイルの置き場 (起動のときに実行のフォルダを今のフォルダにするので、そこからの経路)
public static class AssetPaths
{
    public const string Schema = "Assets/Data/Schema.sql";

    public const string SampleData = "Assets/Data/SampleData.sql";

    // サンプルのメニュー (サンプルのデータと、写す店舗のない新しい店舗に入れる)
    public const string SampleMenu = "Assets/Data/Menu.json";

    public const string SampleImages = "Assets/Images";
}
