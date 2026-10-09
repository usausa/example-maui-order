namespace TableOrder.KitchenApp.Components.Pages;

// 端末の設定。ペアリングコードを画面のボタンで入れて登録し、アプリを読み込み直して起動からやり直す
// (動いている端末を登録し直したときに、前の店舗の通知の数え方と選んでいた持ち場を残さない)
// 接続先はアプリを配ったサーバなので入れない。登録し直しに失敗しても、前の登録は残す
public sealed partial class SetupPage
{
    // ペアリングコードの桁数
    private const int CodeLength = 6;

    private string pairingCode = string.Empty;

    private string? error;

    private bool isRunning;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required Settings Settings { get; set; }

    [Inject]
    public required KitchenUsecase KitchenUsecase { get; set; }

    private string RegistrationText =>
        Settings.DeviceId is { } id ? ViewHelper.Format(AppResources.SetupRegisteredFormat, id.ToString("D")) : AppResources.SetupNotRegistered;

    //--------------------------------------------------------------------------------
    // Setup
    //--------------------------------------------------------------------------------

    private void ChangeCode(string value)
    {
        pairingCode = value;
        error = null;
    }

    private async Task RegisterAsync()
    {
        if ((pairingCode.Length != CodeLength) || isRunning)
        {
            return;
        }

        isRunning = true;
        try
        {
            var result = await KitchenUsecase.PairAsync(pairingCode);
            if (!result.IsSuccess)
            {
                error = ViewHelper.ErrorMessage(result);
                return;
            }

            Navigation.NavigateTo(Navigation.BaseUri, forceLoad: true);
        }
        finally
        {
            isRunning = false;
        }
    }

    private void Back() => Navigation.NavigateTo(String.Empty, replace: true);
}
