namespace TableOrder.Server.Web.Settings;

public sealed class DatabaseSetting
{
    // テナントが 1 つもないときにサンプルのデータを入れる (開発の環境とテスト)
    public bool SampleData { get; set; }
}
