using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Holdhint.Core;

namespace Holdhint;

internal sealed record HudRow(string Key, string Title, string? Note);

/// <summary>
/// Draws the dark panel into a premultiplied bitmap. The origin is the top left.
/// Sizes are physical pixels so a per-monitor DPI scale of 2 draws twice as many pixels.
/// </summary>
internal static class HudPainter
{
    static readonly Color Background = Color.FromArgb(230, 18, 20, 26);
    static readonly Color Stroke = Color.FromArgb(36, 255, 255, 255);
    static readonly Color Text = Color.FromArgb(255, 250, 250, 247);
    static readonly Color Secondary = Color.FromArgb(173, 255, 255, 255);
    static readonly Color Pill = Color.FromArgb(36, 255, 255, 255);
    static readonly Color BrandColor = Color.FromArgb(107, 255, 255, 255);
    static readonly Color Divider = Color.FromArgb(36, 255, 255, 255);
    static readonly Color Shadow = Color.FromArgb(80, 0, 0, 0);

    public static Bitmap Paint(
        IReadOnlyList<string> symbols,
        string subtitle,
        IReadOnlyList<HudRow> rows,
        float scale,
        int maxWidth,
        int maxHeight)
    {
        scale = scale < 1f || float.IsNaN(scale) ? 1f : scale > 3f ? 3f : scale;
        maxWidth = Math.Max((int)(360 * scale), maxWidth);
        maxHeight = Math.Max((int)(180 * scale), maxHeight);

        var source = rows.Count > 80 ? rows.Take(80).ToList() : rows.ToList();
        using var measureBitmap = new Bitmap(8, 8, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var measure = Graphics.FromImage(measureBitmap);
        Prepare(measure);
        using var fonts = HudFonts.Create(scale);

        Layout layout;
        var omitted = 0;
        while (true)
        {
            var shown = Slice(source, omitted);
            layout = Layout.Compute(measure, fonts, symbols, subtitle, shown, scale, maxWidth, maxHeight);
            if (layout.PixelHeight <= maxHeight || shown.Count <= 2 || omitted >= source.Count)
                break;
            omitted++;
        }

        var bitmap = new Bitmap(layout.PixelWidth, layout.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var draw = Graphics.FromImage(bitmap);
        Prepare(draw);
        layout.Draw(draw, fonts);
        return bitmap;
    }

    public static bool HasOpaquePixel(Bitmap bitmap)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            var stride = Math.Abs(data.Stride);
            var buffer = new byte[stride];
            for (var y = 0; y < bitmap.Height; y += 3)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, buffer, 0, stride);
                for (var x = 3; x < buffer.Length; x += 16)
                {
                    if (buffer[x] > 200) return true;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return false;
    }

    static List<HudRow> Slice(List<HudRow> source, int omitted)
    {
        if (omitted <= 0) return source;
        var keep = Math.Max(0, source.Count - omitted);
        var shown = source.Take(keep).ToList();
        shown.Add(new HudRow("…", omitted + " more", null));
        return shown;
    }

    static void Prepare(Graphics graphics)
    {
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.PageUnit = GraphicsUnit.Pixel;
    }

    sealed class HudFonts : IDisposable
    {
        public Font Symbol { get; }
        public Font Brand { get; }
        public Font Key { get; }
        public Font Title { get; }
        public Font Note { get; }
        public Font Subtitle { get; }

        HudFonts(Font symbol, Font brand, Font key, Font title, Font note, Font subtitle)
        {
            Symbol = symbol;
            Brand = brand;
            Key = key;
            Title = title;
            Note = note;
            Subtitle = subtitle;
        }

        public static HudFonts Create(float scale)
        {
            return new HudFonts(
                Face("Segoe UI", 30 * scale, FontStyle.Regular),
                Face("Segoe UI", 13 * scale, FontStyle.Bold),
                Face("Consolas", 20 * scale, FontStyle.Bold, monospace: true),
                Face("Segoe UI", 18 * scale, FontStyle.Regular),
                Face("Segoe UI", 14 * scale, FontStyle.Regular),
                Face("Segoe UI", 14 * scale, FontStyle.Regular));
        }

        static Font Face(string name, float pixels, FontStyle style, bool monospace = false)
        {
            try
            {
                var font = new Font(name, pixels, style, GraphicsUnit.Pixel);
                if (font.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return font;
                font.Dispose();
            }
            catch
            {
                // The generic family below is the fallback.
            }

            var family = monospace ? FontFamily.GenericMonospace : FontFamily.GenericSansSerif;
            return new Font(family, pixels, style, GraphicsUnit.Pixel);
        }

        public void Dispose()
        {
            Symbol.Dispose();
            Brand.Dispose();
            Key.Dispose();
            Title.Dispose();
            Note.Dispose();
            Subtitle.Dispose();
        }
    }

    sealed class Layout
    {
        public int PixelWidth { get; private init; }
        public int PixelHeight { get; private init; }
        float _panelX;
        float _panelY;
        float _panelWidth;
        float _panelHeight;
        float _radius;
        string[] _symbols = Array.Empty<string>();
        RectangleF[] _symbolFrames = Array.Empty<RectangleF>();
        RectangleF _brand;
        string _subtitle = "";
        RectangleF _subtitleRect;
        RectangleF? _divider;
        Placed[] _rows = Array.Empty<Placed>();

        public static Layout Compute(
            Graphics graphics,
            HudFonts fonts,
            IReadOnlyList<string> symbols,
            string subtitle,
            IReadOnlyList<HudRow> rows,
            float scale,
            int maxWidth,
            int maxHeight)
        {
            float S(float value) => value * scale;
            var padX = S(28);
            var padTop = S(22);
            var padBottom = S(20);
            var shadow = S(18);
            var radius = S(20);
            var colGap = S(36);
            var keyGap = S(16);
            var minWidth = S(440);
            var symbolGap = S(14);

            using var wrap = new StringFormat { Trimming = StringTrimming.Word };
            var symbolSizes = new SizeF[symbols.Count];
            float symbolsWidth = 0;
            float symbolHeight = S(36);
            for (var i = 0; i < symbols.Count; i++)
            {
                var size = graphics.MeasureString(symbols[i], fonts.Symbol, 1000, StringFormat.GenericTypographic);
                symbolSizes[i] = size;
                symbolsWidth += size.Width + 4;
                symbolHeight = Math.Max(symbolHeight, size.Height);
            }

            if (symbols.Count > 1) symbolsWidth += (symbols.Count - 1) * symbolGap;
            var brandSize = graphics.MeasureString("Holdhint", fonts.Brand);
            var headerMin = padX + symbolsWidth + S(24) + brandSize.Width + padX;

            float keyWidth = S(56);
            foreach (var row in rows)
            {
                var width = graphics.MeasureString(row.Key, fonts.Key).Width + S(22);
                keyWidth = Math.Max(keyWidth, Math.Min(S(168), width));
            }

            float naturalTitle = S(220);
            foreach (var row in rows)
            {
                naturalTitle = Math.Max(naturalTitle, Math.Min(S(480), graphics.MeasureString(row.Title, fonts.Title).Width + 8));
                if (!string.IsNullOrEmpty(row.Note))
                    naturalTitle = Math.Max(naturalTitle, Math.Min(S(480), graphics.MeasureString(row.Note, fonts.Note).Width + 8));
            }

            var naturalColumn = keyWidth + keyGap + naturalTitle;
            Layout? fit = null;
            for (var columns = 1; columns <= 3; columns++)
            {
                fit = Place(
                    graphics, fonts, wrap, symbols, symbolSizes, symbolsWidth, symbolHeight, brandSize,
                    subtitle, rows, columns, naturalColumn, headerMin, minWidth, maxWidth,
                    padX, padTop, padBottom, shadow, radius, colGap, keyGap, keyWidth, scale);
                if (fit.PixelHeight <= maxHeight || columns == 3) break;
            }

            return fit ?? throw new InvalidOperationException("The hint panel has no layout.");
        }

        static Layout Place(
            Graphics graphics,
            HudFonts fonts,
            StringFormat wrap,
            IReadOnlyList<string> symbols,
            SizeF[] symbolSizes,
            float symbolsWidth,
            float symbolHeight,
            SizeF brandSize,
            string subtitle,
            IReadOnlyList<HudRow> rows,
            int columns,
            float naturalColumn,
            float headerMin,
            float minWidth,
            int maxWidth,
            float padX,
            float padTop,
            float padBottom,
            float shadow,
            float radius,
            float colGap,
            float keyGap,
            float keyWidth,
            float scale)
        {
            var gaps = Math.Max(0, columns - 1) * colGap;
            var natural = naturalColumn * columns + gaps + padX * 2;
            var available = Math.Max(240 * scale, maxWidth - shadow * 2);
            var panelWidth = Math.Min(available, Math.Max(Math.Min(minWidth, available), Math.Max(headerMin, natural)));
            var columnWidth = (panelWidth - padX * 2 - gaps) / columns;
            var titleWidth = Math.Max(80 * scale, columnWidth - keyWidth - keyGap);

            var placedColumns = new List<Placed>[columns];
            var heights = new float[columns];
            for (var i = 0; i < columns; i++) placedColumns[i] = new List<Placed>();
            var perColumn = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)columns));
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var column = Math.Min(columns - 1, index / perColumn);
                var titleSize = graphics.MeasureString(row.Title, fonts.Title, new SizeF(Math.Max(40, titleWidth - 8), 2000), wrap);
                float noteHeight = 0;
                if (!string.IsNullOrEmpty(row.Note))
                    noteHeight = graphics.MeasureString(row.Note, fonts.Note, new SizeF(Math.Max(40, titleWidth - 8), 2000), wrap).Height;
                var block = titleSize.Height + (noteHeight > 0 ? 3 * scale + noteHeight : 0);
                var rowHeight = Math.Max(44 * scale, block + 14 * scale);
                var pillHeight = 36 * scale;
                var y = heights[column];
                var pill = new RectangleF(0, y + (rowHeight - pillHeight) / 2, keyWidth, pillHeight);
                var titleRect = new RectangleF(0, y + (rowHeight - block) / 2, titleWidth, titleSize.Height + 1);
                RectangleF? noteRect = noteHeight > 0
                    ? new RectangleF(0, titleRect.Y + titleSize.Height + 3 * scale, titleWidth, noteHeight + 1)
                    : null;
                placedColumns[column].Add(new Placed(pill, row.Key, titleRect, row.Title, noteRect, row.Note));
                heights[column] = y + rowHeight + 4 * scale;
            }

            var subtitleHeight = 22 * scale;
            var headerBottom = padTop + symbolHeight + 8 * scale + subtitleHeight + (rows.Count == 0 ? 8 * scale : 28 * scale);
            var body = heights.Length == 0 ? 0 : heights.Max();
            var panelHeight = Math.Max(120 * scale, headerBottom + body + padBottom);

            var symbolFrames = new RectangleF[symbols.Count];
            float x = padX;
            for (var i = 0; i < symbols.Count; i++)
            {
                symbolFrames[i] = new RectangleF(x, padTop + (symbolHeight - symbolSizes[i].Height) / 2, symbolSizes[i].Width + 4, symbolSizes[i].Height + 2);
                x += symbolSizes[i].Width + 4 + (i < symbols.Count - 1 ? 14 * scale : 0);
            }

            var absolute = new List<Placed>();
            for (var column = 0; column < columns; column++)
            {
                var originX = padX + column * (columnWidth + colGap);
                foreach (var row in placedColumns[column])
                {
                    absolute.Add(new Placed(
                        Offset(row.Pill, originX, headerBottom),
                        row.Key,
                        Offset(row.TitleRect, originX + keyWidth + keyGap, headerBottom),
                        row.Title,
                        row.NoteRect is RectangleF note ? Offset(note, originX + keyWidth + keyGap, headerBottom) : null,
                        row.Note));
                }
            }

            var pixelWidth = (int)Math.Ceiling(panelWidth + shadow * 2);
            var pixelHeight = (int)Math.Ceiling(panelHeight + shadow * 2);
            return new Layout
            {
                PixelWidth = Math.Max(1, pixelWidth),
                PixelHeight = Math.Max(1, pixelHeight),
                _panelX = shadow,
                _panelY = shadow,
                _panelWidth = panelWidth,
                _panelHeight = panelHeight,
                _radius = radius,
                _symbols = symbols.ToArray(),
                _symbolFrames = symbolFrames,
                _brand = new RectangleF(panelWidth - padX - brandSize.Width, padTop + (symbolHeight - brandSize.Height) / 2, brandSize.Width + 2, brandSize.Height + 2),
                _subtitle = subtitle,
                _subtitleRect = new RectangleF(padX, padTop + symbolHeight + 8 * scale, panelWidth - padX * 2, subtitleHeight + 4),
                _divider = rows.Count == 0 ? null : new RectangleF(padX, padTop + symbolHeight + 8 * scale + subtitleHeight + 12 * scale, panelWidth - padX * 2, Math.Max(1, scale)),
                _rows = absolute.ToArray(),
            };
        }

        public void Draw(Graphics graphics, HudFonts fonts)
        {
            graphics.Clear(Color.Transparent);
            var panel = new RectangleF(_panelX, _panelY, _panelWidth, _panelHeight);
            var shadowRect = panel;
            shadowRect.Offset(0, Math.Max(4, _radius * 0.25f));
            using (var shadowPath = Round(shadowRect, _radius))
            using (var shadowBrush = new SolidBrush(Shadow))
                graphics.FillPath(shadowBrush, shadowPath);

            using (var path = Round(panel, _radius))
            using (var fill = new SolidBrush(Background))
            using (var pen = new Pen(Stroke, 1))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(pen, path);
            }

            using var textBrush = new SolidBrush(Text);
            using var secondaryBrush = new SolidBrush(Secondary);
            using var brandBrush = new SolidBrush(BrandColor);
            using var pillBrush = new SolidBrush(Pill);
            using var dividerBrush = new SolidBrush(Divider);
            using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var wrap = new StringFormat { Trimming = StringTrimming.Word };

            for (var i = 0; i < _symbols.Length; i++)
            {
                var frame = Offset(_symbolFrames[i], _panelX, _panelY);
                graphics.DrawString(_symbols[i], fonts.Symbol, textBrush, frame, wrap);
            }

            graphics.DrawString("Holdhint", fonts.Brand, brandBrush, Offset(_brand, _panelX, _panelY), wrap);
            graphics.DrawString(_subtitle, fonts.Subtitle, secondaryBrush, Offset(_subtitleRect, _panelX, _panelY), wrap);
            if (_divider is RectangleF divider)
                graphics.FillRectangle(dividerBrush, Offset(divider, _panelX, _panelY));

            foreach (var row in _rows)
            {
                var pill = Offset(row.Pill, _panelX, _panelY);
                using var pillPath = Round(pill, Math.Min(10, pill.Height / 2));
                graphics.FillPath(pillBrush, pillPath);
                graphics.DrawString(row.Key, fonts.Key, textBrush, pill, center);
                graphics.DrawString(row.Title, fonts.Title, textBrush, Offset(row.TitleRect, _panelX, _panelY), wrap);
                if (row.Note != null && row.NoteRect is RectangleF note)
                    graphics.DrawString(row.Note, fonts.Note, secondaryBrush, Offset(note, _panelX, _panelY), wrap);
            }
        }

        static RectangleF Offset(RectangleF rect, float dx, float dy) =>
            new(rect.X + dx, rect.Y + dy, rect.Width, rect.Height);

        static GraphicsPath Round(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            var diameter = Math.Max(2, radius * 2);
            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    sealed record Placed(RectangleF Pill, string Key, RectangleF TitleRect, string Title, RectangleF? NoteRect, string? Note);
}
