namespace TableOrder.ReceptionApp.Markup;

using Fonts;

// 記号の色はテーマの役割 (Colors.xaml) から引く。画面を作るたびに引き直すので、起動時に差し替えたテーマの色も反映される
public static class AppIcons
{
    private const double HeaderSize = 24d;

    private const double ActionSize = 36d;

    // Header

    public static FontImageSource Language => Create(MaterialIcons.Language, HeaderSize, "OnSecondaryColor");

    // Action

    public static FontImageSource Start => Create(MaterialIcons.Touch_app, ActionSize, "OnPrimaryColor");

    private static FontImageSource Create(string glyph, double size, string colorKey) =>
        new()
        {
            FontFamily = MaterialIcons.FontFamily,
            Glyph = glyph,
            Size = size,
            Color = Application.Current!.Resources.FindResource<Color>(colorKey)
        };
}
