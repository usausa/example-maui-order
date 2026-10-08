namespace TableOrder.TableApp.Controls;

// QR コードの代わりの模様 (モック)。内容から決まる模様と 3 隅の位置合わせの印を描く
// 決済サービスとつなぐときに、本物の QR コードの生成に替える
public sealed class QrCodeView : GraphicsView
{
    // 縦横のセルの数 (QR コードの型番 3 と同じ)
    private const int Modules = 29;

    private const int FinderSize = 7;

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(QrCodeView),
        string.Empty,
        propertyChanged: static (bindable, _, _) => ((QrCodeView)bindable).Invalidate());

    public static readonly BindableProperty ForegroundColorProperty = BindableProperty.Create(
        nameof(ForegroundColor),
        typeof(Color),
        typeof(QrCodeView),
        propertyChanged: static (bindable, _, _) => ((QrCodeView)bindable).Invalidate());

    // QR にする内容 (URL など)
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 色はスタイルで役割の色を渡す
    public Color? ForegroundColor
    {
        get => (Color?)GetValue(ForegroundColorProperty);
        set => SetValue(ForegroundColorProperty, value);
    }

    public QrCodeView()
    {
        Drawable = new QrDrawable(this);
    }

    private sealed class QrDrawable : IDrawable
    {
        private readonly QrCodeView owner;

        public QrDrawable(QrCodeView owner)
        {
            this.owner = owner;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (String.IsNullOrEmpty(owner.Text) || (owner.ForegroundColor is not { } color))
            {
                return;
            }

            var cell = MathF.Floor(Math.Min(dirtyRect.Width, dirtyRect.Height) / Modules);
            var offsetX = dirtyRect.Left + ((dirtyRect.Width - (cell * Modules)) / 2);
            var offsetY = dirtyRect.Top + ((dirtyRect.Height - (cell * Modules)) / 2);
            canvas.FillColor = color;

            // 位置合わせの印
            DrawFinder(canvas, offsetX, offsetY, cell);
            DrawFinder(canvas, offsetX + ((Modules - FinderSize) * cell), offsetY, cell);
            DrawFinder(canvas, offsetX, offsetY + ((Modules - FinderSize) * cell), cell);

            // 内容から決まる模様 (印と、その周りの 1 セルの余白は描かない)
            var state = Hash(owner.Text);
            for (var y = 0; y < Modules; y++)
            {
                for (var x = 0; x < Modules; x++)
                {
                    state = Next(state);
                    if (IsFinderArea(x, y) || ((state & 1) == 0))
                    {
                        continue;
                    }

                    canvas.FillRectangle(offsetX + (x * cell), offsetY + (y * cell), cell, cell);
                }
            }
        }

        private static void DrawFinder(ICanvas canvas, float x, float y, float cell)
        {
            canvas.FillRectangle(x, y, cell * FinderSize, cell);
            canvas.FillRectangle(x, y + (cell * (FinderSize - 1)), cell * FinderSize, cell);
            canvas.FillRectangle(x, y, cell, cell * FinderSize);
            canvas.FillRectangle(x + (cell * (FinderSize - 1)), y, cell, cell * FinderSize);
            canvas.FillRectangle(x + (cell * 2), y + (cell * 2), cell * 3, cell * 3);
        }

        private static bool IsFinderArea(int x, int y)
        {
            const int area = FinderSize + 1;
            return ((x < area) && (y < area)) ||
                   ((x >= Modules - area) && (y < area)) ||
                   ((x < area) && (y >= Modules - area));
        }

        // FNV-1a (内容が同じなら同じ模様にする)
        private static uint Hash(string value)
        {
            var hash = 2166136261u;
            foreach (var c in value)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return hash == 0 ? 1u : hash;
        }

        // xorshift32
        private static uint Next(uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
