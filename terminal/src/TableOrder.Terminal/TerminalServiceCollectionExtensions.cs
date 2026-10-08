namespace TableOrder.Terminal;

using TableOrder.Client.Rest;
using TableOrder.Client.SignalR;
using TableOrder.Terminal.Components;
using TableOrder.Terminal.Modules;
using TableOrder.Terminal.Shell;

// 端末に共通の部品の登録 (各アプリの MauiProgram から呼ぶ)。生成の DI が作り方を作れるように、アプリの GeneratedFactory にも型を足す
public static class TerminalServiceCollectionExtensions
{
    // 端末の部品、端末の設定と状態、登録と状態の報告、注文サーバの登録と通知の窓口、どの端末でも同じポップアップ
    // 端末の種類ごとの窓口 (ITableApi など) と通知の扱い (OrderEventReceiverBase を継いだもの) はアプリで登録する
    // ポップアップの ID と View の組 (TerminalModules.DialogSource) は、アプリのポップアップの登録に足す
    public static IServiceCollection AddTerminalComponents(this IServiceCollection services, TerminalOptions options, KioskOptions kioskOptions)
    {
        // Options
        services.AddSingleton(options);
        services.AddSingleton(kioskOptions);

        // Components
        services.AddSingleton<DeviceInformation>();
        services.AddSingleton<DeviceKey>();
        services.AddSingleton<IStorageManager, StorageManager>();
        services.AddSingleton<KioskManager>();
        services.AddSingleton<ManagedConfiguration>();

        // State
        services.AddSingleton<DeviceState>();
        services.AddSingleton<Settings>();
        services.AddSingleton<StaffLock>();
        services.AddSingleton<IDeviceContext>(static p => p.GetRequiredService<Settings>());

        // Service (注文サーバの REST の窓口と SignalR の通知)
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new OrderServerOptions());
        services.AddSingleton<RestConnection>();
        services.AddSingleton<IDeviceApi, RestDeviceApi>();
        services.AddSingleton<IOrderEvents, SignalROrderEvents>();

        // Usecase
        services.AddSingleton<DeviceUsecase>();

        // Shell
        services.AddSingleton<StatusReporter>();

        // View & ViewModel (どの端末でも同じポップアップ)
        services.AddTerminalViews();
        services.AddTerminalViewModels();

        return services;
    }
}
