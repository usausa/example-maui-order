namespace TableOrder.TableApp.Markup;

using Fonts;

// 記号の色はテーマの役割 (Colors.xaml) から引く。画面を作るたびに引き直すので、起動時に差し替えたテーマの色も反映される
public static class AppIcons
{
    private const double HeaderSize = 24d;

    private const double ActionSize = 30d;

    private const double StandbySize = 36d;

    // Header

    public static FontImageSource Language => Create(MaterialIcons.Language, HeaderSize, "OnSurfaceColor");

    // Action

    public static FontImageSource History => Create(MaterialIcons.Receipt_long, ActionSize, "OnSurfaceColor");

    public static FontImageSource Call => Create(MaterialIcons.Room_service, ActionSize, "OnSurfaceColor");

    public static FontImageSource Checkout => Create(MaterialIcons.Payments, ActionSize, "OnSecondaryColor");

    public static FontImageSource Back => Create(MaterialIcons.Arrow_back, ActionSize, "OnSurfaceColor");

    // Standby

    public static FontImageSource StandbyLanguage => Create(MaterialIcons.Language, HeaderSize, "OnSecondaryColor");

    public static FontImageSource StandbyStart => Create(MaterialIcons.Touch_app, StandbySize, "OnPrimaryColor");

    private static FontImageSource Create(string glyph, double size, string colorKey) =>
        new()
        {
            FontFamily = MaterialIcons.FontFamily,
            Glyph = glyph,
            Size = size,
            Color = Application.Current!.Resources.FindResource<Color>(colorKey)
        };
}
