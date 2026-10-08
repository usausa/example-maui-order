namespace TableOrder.Terminal.Modules;

using BunnyTail.DependencyInjection;

// どの端末でも同じポップアップの登録 (View と ViewModel の DI と、ポップアップの ID と View の組)
public static partial class TerminalModules
{
    private const string ModulesNamespace = "TableOrder.Terminal.Modules";

    // ReSharper disable UnusedMethodReturnValue.Global
    [ComponentRegistration(Lifetime.Transient, "View$", Namespace = ModulesNamespace)]
    internal static partial IServiceCollection AddTerminalViews(this IServiceCollection services);

    [ComponentRegistration(Lifetime.Transient, "ViewModel$", Namespace = ModulesNamespace)]
    internal static partial IServiceCollection AddTerminalViewModels(this IServiceCollection services);
    // ReSharper restore UnusedMethodReturnValue.Global

    // アプリのポップアップの登録 (AddComponentsPopup の AutoRegister) に足す
    [PopupSource]
    public static partial IEnumerable<KeyValuePair<TerminalDialogId, Type>> DialogSource();
}
