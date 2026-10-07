namespace TableOrder.Server.Web.Settings;

public sealed class LogSetting
{
    public bool HttpLog { get; set; }

    public bool HttpDump { get; set; }

    [Range(1, 1_048_576)]
    public int HttpDumpLimit { get; set; }
}
