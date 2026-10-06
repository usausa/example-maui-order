namespace TableOrder.Terminal.Table.Modules.Setup;

// 端末の設定。テーブル番号は電卓で入れ、保存したら起動からやり直す
// サーバができたら、端末の登録 (ペアリング) でテーブルと接続先が決まる形に替える
public sealed partial class SetupViewModel : AppViewModelBase
{
    private readonly Settings settings;

    public string VersionText { get; }

    [ObservableProperty]
    public partial string TableNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TableNoText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiEndPoint { get; set; }

    public IObserveCommand InputTableNoCommand { get; }

    public IObserveCommand SaveCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public SetupViewModel(
        IAppInfo appInfo,
        IPopupNavigator popupNavigator,
        Settings settings)
    {
        this.settings = settings;

        VersionText = ViewHelper.Version(appInfo);
        ApiEndPoint = settings.ApiEndPoint;
        UpdateTableNo(settings.TableNo);

        InputTableNoCommand = MakeAsyncCommand(async () =>
        {
            if (await popupNavigator.InputTableNoAsync(TableNo) is { } value)
            {
                UpdateTableNo(value);
            }
        });
        SaveCommand = MakeAsyncCommand(SaveAsync, () => !String.IsNullOrEmpty(TableNo));
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // 設定済みなら変えずに起動へ戻る (設定がなければ戻る先がない)
    protected override async Task OnNotifyBackAsync()
    {
        if (settings.IsConfigured)
        {
            await Navigator.ForwardAsync(ViewId.Startup);
        }
    }

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    private void UpdateTableNo(string value)
    {
        TableNo = value;
        TableNoText = String.IsNullOrEmpty(value) ? AppResources.SetupNotSet : value;
    }

    private async Task SaveAsync()
    {
        settings.TableNo = TableNo;
        settings.ApiEndPoint = ApiEndPoint.Trim();

        await Navigator.ForwardAsync(ViewId.Startup);
    }
}
