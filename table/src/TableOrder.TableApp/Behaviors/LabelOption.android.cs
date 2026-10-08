namespace TableOrder.TableApp.Behaviors;

using Android.Util;
using Android.Widget;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

public static partial class LabelOption
{
    public static partial void UseCustomMapper(BehaviorOptions options)
    {
        if (options.AutoSize)
        {
            LabelHandler.Mapper.AppendToMapping(AutoSizeProperty.PropertyName, (handler, view) =>
            {
                var label = (Label)view;
                if (GetAutoSize(label))
                {
                    label.LineBreakMode = LineBreakMode.NoWrap;
#pragma warning disable CA1416
                    handler.PlatformView.SetAutoSizeTextTypeWithDefaults(AutoSizeTextType.Uniform);
#pragma warning restore CA1416

                    UpdateLabelSize(handler, label);
                }
            });
            LabelHandler.Mapper.AppendToMapping(MaxSizeProperty.PropertyName, (handler, view) =>
            {
                var label = (Label)view;
                if (GetAutoSize(label))
                {
                    UpdateLabelSize(handler, label);
                }
            });
        }

        if (options.FixedLineHeight)
        {
            // 和文は字形を補うフォント (CJK) の高さで行が広がり、英字の行より高くなる
            // 行を広げずに高さを固定して、英字と和文で同じ行の高さにする
            // 1 行目の上だけに付くフォントの余白も外し、どの行も同じ組み方にする (別のラベルを行の位置にそろえられるように)
            // 行の間隔は設定したときのフォントの高さから決まるので、字の大きさを後から変えるラベルには使わない
            LabelHandler.Mapper.AppendToMapping(FixedLineHeightProperty.PropertyName, static (handler, view) =>
            {
                var label = (Label)view;
                var height = GetFixedLineHeight(label);
                if (height > 0)
                {
                    var textView = handler.PlatformView;
                    // 装飾付きの文字 (FormattedText) では MAUI が TextView 自体のフォントを設定せず、既定の字の大きさで行の間隔が決まってしまう
                    // 表示は文字の装飾の側のフォントで決まるので、TextView にラベルのフォントを設定しても見た目は変わらない
                    if (label.FormattedText is not null)
                    {
                        textView.UpdateFont(label, handler.MauiContext!.Services.GetRequiredService<IFontManager>());
                    }

                    textView.FallbackLineSpacing = false;
                    textView.SetIncludeFontPadding(false);
                    textView.LineHeight = (int)Math.Round(TypedValue.ApplyDimension(ComplexUnitType.Dip, (float)height, textView.Resources!.DisplayMetrics));
                }
            });
        }
    }

    public static void UpdateLabelSize(ILabelHandler handler, Label label)
    {
        var max = (int)GetMaxSize(label);
#pragma warning disable CA1416
        handler.PlatformView.SetAutoSizeTextTypeUniformWithConfiguration(1, max, 1, 1);
#pragma warning restore CA1416
        label.MinimumHeightRequest = max;
    }
}
