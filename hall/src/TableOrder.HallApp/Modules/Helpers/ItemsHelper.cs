namespace TableOrder.HallApp.Modules.Helpers;

// 一覧の項目の出し直し
public static class ItemsHelper
{
    // 読み直した並びに合わせる。残る項目は作り直さずに中身を替え (一覧のスクロールの位置を保つ)、なくなった項目を外して新しい項目を入れる
    public static void Sync<TItem, TSource>(
        this ObservableCollection<TItem> items,
        IReadOnlyList<TSource> sources,
        Func<TItem, TSource, bool> match,
        Func<TSource, TItem> create,
        Action<TItem, TSource> update)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (!sources.Any(x => match(item, x)))
            {
                items.RemoveAt(i);
            }
        }

        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            var index = IndexOf(items, i, x => match(x, source));
            if (index < 0)
            {
                items.Insert(i, create(source));
                continue;
            }

            if (index != i)
            {
                items.Move(index, i);
            }

            update(items[i], source);
        }
    }

    private static int IndexOf<TItem>(ObservableCollection<TItem> items, int start, Func<TItem, bool> predicate)
    {
        for (var i = start; i < items.Count; i++)
        {
            if (predicate(items[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
