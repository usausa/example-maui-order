namespace TableOrder.Terminal.Table.Helpers;

using CommunityToolkit.Maui.Views;

public static partial class ElementHelper
{
    public static bool MoveFocusInRoot(VisualElement current, bool forward)
    {
        var parent = current.FindRoot();
        if (parent is null)
        {
            return false;
        }

        return PlatformMoveFocus(parent, current, forward);
    }

    public static VisualElement? FindRoot(this Element element)
    {
        while (true)
        {
            var parent = element.Parent;
            if (parent is null)
            {
                return null;
            }

            if (element is Page page)
            {
                return page;
            }

            if (element is Popup popup)
            {
                return popup.Content;
            }

            element = parent;
        }
    }

    private static partial bool PlatformMoveFocus(VisualElement parent, VisualElement? current, bool forward);
}
