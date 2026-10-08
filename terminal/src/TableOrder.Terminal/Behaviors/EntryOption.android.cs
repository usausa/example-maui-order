namespace TableOrder.Terminal.Behaviors;

using Android.Text;
using Android.Widget;

using Java.Lang;

using Microsoft.Maui.Handlers;

public static partial class EntryOption
{
    public static partial void UseCustomMapper(BehaviorOptions options)
    {
        // SelectAllOnFocus
        if (options.SelectAllOnFocus)
        {
            EntryHandler.Mapper.AppendToMapping(SelectAllOnFocusProperty.PropertyName, static (handler, _) => UpdateSelectAllOnFocus(handler.PlatformView, (Entry)handler.VirtualView));
            EditorHandler.Mapper.AppendToMapping(SelectAllOnFocusProperty.PropertyName, static (handler, _) => UpdateSelectAllOnFocus(handler.PlatformView, (Editor)handler.VirtualView));
        }

        // InputFilter
        if (options.InputFilter)
        {
            EntryHandler.Mapper.AppendToMapping(InputFilterProperty.PropertyName, static (handler, _) => UpdateInputFilter(handler.PlatformView, (Entry)handler.VirtualView));
            EditorHandler.Mapper.AppendToMapping(InputFilterProperty.PropertyName, static (handler, _) => UpdateInputFilter(handler.PlatformView, (Editor)handler.VirtualView));
        }
    }

    private static void UpdateSelectAllOnFocus(TextView editText, BindableObject element)
    {
        var value = GetSelectAllOnFocus(element);
        editText.SetSelectAllOnFocus(value);
    }

    private static void UpdateInputFilter(TextView editText, BindableObject element)
    {
        var rule = GetInputFilter(element);
#pragma warning disable CA2000
        editText.SetFilters(rule is null ? [] : [new InputFilterInputFilter(rule)]);
#pragma warning restore CA2000
    }

    private sealed class InputFilterInputFilter : Object, IInputFilter
    {
        private readonly Func<string, bool> rule;

        public InputFilterInputFilter(Func<string, bool> rule)
        {
            this.rule = rule;
        }

        public ICharSequence? FilterFormatted(ICharSequence? source, int start, int end, ISpanned? dest, int dstart, int dend)
        {
            var value = dest!.SubSequence(0, dstart) + source!.SubSequence(start, end) + dest!.SubSequence(dend, dest!.Length());
            return rule(value) ? source : new String(dest.SubSequence(dstart, dend));
        }
    }
}
