using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Layout;

/// <summary>Punto cinsinden bir dikdörtgen. Sol üst köşe başlangıç noktasıdır.</summary>
public readonly record struct RectPt(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public static class LayoutConstants
{
    /// <summary>İçeriğin kağıt kenarına en fazla yaklaşabileceği mesafe.</summary>
    public const double MarginMm = 5.0;

    /// <summary>Çoklu yerleşimde komşu hücreler arasındaki boşluk.</summary>
    public const double GutterMm = 3.0;
}

public static class CellGrid
{
    /// <summary>
    /// Yaprağın içerik alanını ızgaraya böler. İçerik alanı, yazıcının fiziksel
    /// olarak basamadığı kenar payı ile 5 mm'nin büyüğü kadar içeridedir.
    /// </summary>
    /// <param name="paper">Kağıdın tam boyutu.</param>
    /// <param name="printable">Yazıcının basabildiği alan, kağıt koordinatlarında.</param>
    public static IReadOnlyList<RectPt> Build(SizePt paper, RectPt printable, GridSpec grid)
    {
        var margin = Paper.MmToPt(LayoutConstants.MarginMm);
        var gutter = Paper.MmToPt(LayoutConstants.GutterMm);

        var left = Math.Max(margin, printable.X);
        var top = Math.Max(margin, printable.Y);
        var right = Math.Min(paper.Width - margin, printable.Right);
        var bottom = Math.Min(paper.Height - margin, printable.Bottom);

        var contentWidth = Math.Max(0, right - left);
        var contentHeight = Math.Max(0, bottom - top);

        var cellWidth = (contentWidth - gutter * (grid.Columns - 1)) / grid.Columns;
        var cellHeight = (contentHeight - gutter * (grid.Rows - 1)) / grid.Rows;

        var cells = new List<RectPt>(grid.Capacity);
        for (var row = 0; row < grid.Rows; row++)
        for (var column = 0; column < grid.Columns; column++)
        {
            cells.Add(new RectPt(
                left + column * (cellWidth + gutter),
                top + row * (cellHeight + gutter),
                Math.Max(0, cellWidth),
                Math.Max(0, cellHeight)));
        }

        return cells;
    }
}
