namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using TableOrder.Server.Web.Application.Context;

// チェーンの設定 (選んだテナントの名前・ロゴ・色)。保存すると、テナントのすべての店舗のテーブル端末に知らせる
public sealed partial class BrandPage : IDisposable
{
    private static readonly (string Name, IReadOnlyList<string> Roles)[] Groups =
    [
        ("Brand (主色と補助色)", ThemeRoles.Brand),
        ("Neutral (地・面・文字・罫線)", ThemeRoles.Neutral),
        ("Status (失敗・完了・注意)", ThemeRoles.Status)
    ];

    private readonly Dictionary<string, string?> colors = ThemeRoles.All.ToDictionary(static x => x, static _ => (string?)null);

    private List<string> images = [];

    private int? version;

    private string nameJa = string.Empty;

    private string? nameEn;

    private string? logo;

    private string? logoPreview;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required SettingsService SettingsService { get; set; }

    [Inject]
    public required ImageService ImageService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override Task OnInitializedAsync()
    {
        Selection.Changed += OnSelectionChanged;
        return LoadAsync();
    }

    public void Dispose() => Selection.Changed -= OnSelectionChanged;

    // テナントを選び直したら読み直す (店舗の選択の操作の中から呼ばれるので、文脈を始め直す)
    private void OnSelectionChanged(object? sender, EventArgs e) =>
        _ = InvokeAsync(async () =>
        {
            using (BeginServiceScope())
            {
                await LoadAsync();
            }

            StateHasChanged();
        });

    private async Task LoadAsync()
    {
        version = null;
        if (Selection.TenantId is null)
        {
            return;
        }

        if (await SettingsService.GetBrandAsync(CancellationToken.None) is not { } brand)
        {
            return;
        }

        images = (await ImageService.GetNamesAsync(CancellationToken.None)).ToList();
        nameJa = brand.Name.Ja;
        nameEn = brand.Name.En;
        foreach (var role in ThemeRoles.All)
        {
            colors[role] = brand.Theme.GetValueOrDefault(role);
        }

        version = brand.Version;
        await SelectLogoAsync(brand.LogoImageName);
    }

    //--------------------------------------------------------------------------------
    // Edit
    //--------------------------------------------------------------------------------

    // 選んだロゴを小さく見せる (画像は小さいので、ページに埋めて出す)
    private async Task SelectLogoAsync(string? name)
    {
        logo = name;
        logoPreview = null;
        if ((name is not null) && (await ImageService.GetAsync(name, CancellationToken.None) is { Succeeded: true } image))
        {
            logoPreview = $"data:{image.Value.ContentType};base64,{Convert.ToBase64String(image.Value.Content.Span)}";
        }
    }

    private async Task SaveAsync()
    {
        if (version is not { } current)
        {
            return;
        }

        var theme = colors
            .Where(static x => !String.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(static x => x.Key, static x => x.Value!.Trim());
        var name = new LocalizedText { Ja = nameJa.Trim(), En = String.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim() };
        var error = await SettingsService.UpdateBrandAsync(new BrandSettings(name, logo, theme, current), CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add("チェーンの設定を保存しました。テーブル端末は待受のときに読み直します", Severity.Success);
        await LoadAsync();
    }

    //--------------------------------------------------------------------------------
    // Display
    //--------------------------------------------------------------------------------

    // 入れた色の見本 (形の違う色は見本を出さない)
    private string SwatchStyle(string role) =>
        ThemeRoles.IsColor(colors[role]) ? $"background-color: {ToCss(colors[role]!)}" : "background-color: transparent";

    // #AARRGGBB は CSS では #RRGGBBAA にする
    private static string ToCss(string color) =>
        color.Length == 9 ? "#" + color[3..] + color[1..3] : color;

    private static string ErrorMessage(ServiceError error) =>
        error.Errors?.Values.SelectMany(static x => x).FirstOrDefault() ?? error.ErrorCode switch
        {
            ErrorCodes.VersionMismatch => "ほかで替えられています。読み直してください",
            _ => error.ErrorCode
        };
}
