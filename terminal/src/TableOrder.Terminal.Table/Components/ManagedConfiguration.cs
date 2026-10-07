namespace TableOrder.Terminal.Table.Components;

// 管理対象の構成 (外部の EMM が配る端末の設定)。キーは Platforms/Android/Resources/xml/app_restrictions.xml と揃える
// 配られた値は端末の設定より優先する (Settings が読む)。配られていない値と正しくない値は null にし、端末の設定を使う
public sealed partial class ManagedConfiguration : IDisposable
{
    private const string ApiEndPointKey = "apiEndPoint";

    private const string StaffPinKey = "staffPin";

    private const string EnrollmentTokenKey = "enrollmentToken";

    // 登録トークンの長さの上限 (ほかの値を取り違えて配ったときに使わないように)
    private const int MaxEnrollmentTokenLength = 256;

    private readonly ILogger<ManagedConfiguration> log;

    private bool started;

    private RawValues? current;

    // 配られた値が替わった (起動したとき、EMM が替えたとき、画面が前に出て読み直したとき)
    public event EventHandler? Changed;

    // 注文サーバの URL (http / https の絶対 URL)
    public string? ApiEndPoint { get; private set; }

    // スタッフメニューに入る PIN (決まった桁数の数字)
    public string? StaffPin { get; private set; }

    // 端末の登録トークン (店舗と種類に限った数日有効の値。空白を含まない)
    public string? EnrollmentToken { get; private set; }

    public ManagedConfiguration(ILogger<ManagedConfiguration> log)
    {
        this.log = log;
    }

    // 起動したときに読み、EMM が値を替えたときの知らせを受け始める
    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        Load();
        StartWatch();
    }

    // 画面が前に出たときに読み直す (止まっている間に替わった値は、知らせが届かないことがある)
    public void Refresh()
    {
        if (started)
        {
            Load();
        }
    }

    private void Load()
    {
        var values = ReadValues();
        if (values == current)
        {
            return;
        }

        current = values;
        ApiEndPoint = Accept(ApiEndPointKey, values.ApiEndPoint, IsValidEndPoint);
        StaffPin = Accept(StaffPinKey, values.StaffPin, IsValidPin);
        EnrollmentToken = Accept(EnrollmentTokenKey, values.EnrollmentToken, IsValidToken);
        log.InfoManagedConfiguration(ApiEndPoint ?? string.Empty, StaffPin is not null, EnrollmentToken is not null);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // 空の値は配られていないものとし、正しくない値は使わずに記録する
    private string? Accept(string key, string? value, Func<string, bool> isValid)
    {
        if (String.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (isValid(trimmed))
        {
            return trimmed;
        }

        log.WarnManagedConfigurationInvalid(key);
        return null;
    }

    private static bool IsValidEndPoint(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && ((uri.Scheme == Uri.UriSchemeHttp) || (uri.Scheme == Uri.UriSchemeHttps));

    private static bool IsValidPin(string value) =>
        (value.Length == Length.StaffPinDigits) && value.All(Char.IsAsciiDigit);

    private static bool IsValidToken(string value) =>
        (value.Length <= MaxEnrollmentTokenLength) && !value.Any(Char.IsWhiteSpace);

    // 配られたままの値
    private readonly record struct RawValues(string? ApiEndPoint, string? StaffPin, string? EnrollmentToken);

    private static partial RawValues ReadValues();

    private partial void StartWatch();
}
