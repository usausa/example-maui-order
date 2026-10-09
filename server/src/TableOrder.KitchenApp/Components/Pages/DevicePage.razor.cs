namespace TableOrder.KitchenApp.Components.Pages;

// 端末の画面。端末の情報 (店舗、端末の名前、受け持つ持ち場、端末の id、接続先、音の知らせ、アプリの版) と、端末の設定への入口
// 端末の設定で登録し直しても、前の登録は登録できるまで残す
public sealed partial class DevicePage : IDisposable
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required KioskScreen Kiosk { get; set; }

    [Inject]
    public required Settings Settings { get; set; }

    [Inject]
    public required StoreState StoreState { get; set; }

    private string StoreText => ViewHelper.Text(StoreState.Config.StoreName);

    private string StationsText => String.Join(AppResources.ListSeparator, StoreState.Stations.Select(static x => x.Name));

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override void OnInitialized() => Kiosk.SoundReady += OnSoundReady;

    public void Dispose() => Kiosk.SoundReady -= OnSoundReady;

    private void OnSoundReady(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    private void OpenSetup() => Navigation.NavigateTo("setup");

    private void Close() => Navigation.NavigateTo("tickets", replace: true);
}
