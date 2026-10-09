namespace TableOrder.ReceptionApp.State;

// 端末の設定 (チェーンと店舗の設定) と空席。起動のときに読み、空席は来店の通知で読み直す
public sealed class ReceptionState
{
    public DeviceConfigResponse Config { get; private set; } = default!;

    // 店舗で選べる言語 (知らないコードは使わない)
    public IReadOnlyList<Language> Languages =>
        Config.Languages.Select(LanguageExtensions.FromCode).OfType<Language>().Distinct().ToList();

    // 来店の開き方が受付機の店 (ほかの店では受け付けない)
    public bool IsReceptionStore => Config.Features.VisitOpening == VisitOpening.Reception;

    public string? LogoImageName => Config.Brand.LogoImageName;

    // 端末に保存する画像 (チェーンのロゴ)
    public IReadOnlyCollection<string> ImageNames => LogoImageName is { } logo ? [logo] : [];

    // 空いているテーブルの数 (ひとつもなければ待受を満席にする。人数が定員に入るかは、席を決めるときにサーバが確かめる)
    public int VacantTables { get; private set; }

    // 空席を読み直せなかった (待受がしばらくごとに読み直す。次の来店の通知を待たない)
    public bool IsVacancyStale { get; private set; }

    public string BrandName(Language language) => Config.Brand.Name.Get(language);

    public string StoreName(Language language) => Config.StoreName.Get(language);

    public void Update(DeviceConfigResponse config) => Config = config;

    public void UpdateVacancy(TableListResponse tables)
    {
        VacantTables = tables.Items.Count;
        IsVacancyStale = false;
    }

    public void MarkVacancyStale() => IsVacancyStale = true;
}
