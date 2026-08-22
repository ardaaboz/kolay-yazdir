# Kolay Yazdır Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kırtasiyede günlük çıktı işini tek pencerede bitiren, Windows'un yazdırma diyaloglarını tamamen gizleyen bir masaüstü uygulaması.

**Architecture:** Yerleşim hesabı (`LayoutEngine`) hiçbir çizim yapmayan saf bir fonksiyondur; girdi olarak sayfa boyutlarını ve ayarları alır, çıktı olarak "hangi sayfa hangi dikdörtgene, kaç derece dönük" bilgisini veren bir `Sheet` listesi üretir. Aynı `Sheet` listesi hem önizlemeye (~110 DPI) hem yazıcıya (300 DPI) `SheetRenderer` ile çizilir. Önizleme ile çıktının farklı olması bu yüzden yapısal olarak imkansızdır. Word/Excel dosyaları önce PDF'e çevrilir, böylece PDF ve görsel dışında ayrı bir kod yolu kalmaz.

**Tech Stack:** C# / .NET 8 (`net8.0-windows`) / WPF · PDFtoImage 5.4.0 (PDFium) · System.Drawing.Common (yazdırma) · Velopack 1.2.0 (güncelleme) · xUnit + PDFsharp (test fixture üretimi)

**Spec:** `docs/superpowers/specs/2026-08-21-kolay-yazdir-design.md`

## Global Constraints

- Hedef çatı: `net8.0-windows`. SDK 9.0.300 kurulu, `net8.0-windows` hedefi bununla derlenir. `net9.0` kullanma.
- Uygulama adı her yerde **Kolay Yazdır**. Pencere başlığı, kısayol adı, kurulum adı bu.
- Arayüz metinleri Türkçedir. Kod içindeki tanımlayıcılar (sınıf, metot, değişken adları) İngilizcedir.
- Tema renkleri birebir: zemin `#0A0A0A`, panel `#141414`, kenarlık `#2E2E2E`, birincil metin `#FFFFFF`, ikincil metin `#A8A8A8`, vurgu `#FFD84D`, seçili durum zemin `#FFFFFF` metin `#0A0A0A`, hata `#FF6B6B`. Bu altı ton dışında renk kullanma; daha soluk bir üçüncü metin tonu yok.
- Tüm uzunluklar iç modelde **punto** (1/72 inç) cinsindendir. Milimetre veya piksel sadece sınırlarda (kullanıcı arayüzü, yazıcı API'si) çevrilir.
- Baskı çözünürlüğü sabit 300 DPI (`RenderConstants.PrintDpi`).
- Kenar boşluğu 5 mm, hücre arası boşluk 3 mm (`LayoutConstants`).
- `Core` projesi Windows'a özgü hiçbir API kullanmaz (`System.Drawing` dahil). Bu kural testlerin yazıcısız ve hızlı çalışmasını garanti eder.
- Her görev sonunda commit at. Commit mesajları Türkçe, konu satırı 60 karakteri geçmesin.

---

## Dosya yapısı

```
KolayYazdir.sln
src/
  KolayYazdir.Core/                       net8.0 — saf, platform bağımsız
    Models/PaperFormat.cs                 kağıt boyutları, punto tablosu
    Models/PrintSettings.cs               kullanıcı ayarlarının tamamı (record)
    Models/SourcePageInfo.cs              bir kaynak sayfanın kimliği + boyutu
    Models/Sheet.cs                       Sheet, PlacedPage, SheetSide
    Layout/PageRangeParser.cs             "1-5, 8" ayrıştırma
    Layout/GridSpec.cs                    N'li → satır/sütun
    Layout/CellGrid.cs                    hücre dikdörtgenleri
    Layout/Placement.cs                   ölçek / döndürme / ortalama
    Layout/LayoutEngine.cs                yaprak kurgusu, dupleks, kopya
  KolayYazdir.Documents/                  net8.0-windows — dosya okuma
    IPageRasterizer.cs, RasterPage.cs
    PdfRasterizer.cs                      PDFtoImage sarmalayıcısı
    ImageRasterizer.cs
    SourceDocument.cs, DocumentLoader.cs
    Office/IOfficeConverter.cs
    Office/LibreOfficeConverter.cs
    Office/OfficeComConverter.cs
    Office/OfficeConverterChain.cs
    Office/ConversionCache.cs
  KolayYazdir.Printing/                   net8.0-windows — yazıcı
    Interop/NativeMethods.cs, Interop/DevMode.cs
    PrinterCapabilities.cs
    SheetRenderer.cs
    PrintJobRunner.cs
    ManualDuplexPlan.cs
  KolayYazdir.App/                        net8.0-windows — WPF
    App.xaml(.cs), MainWindow.xaml(.cs)
    Theme/Dark.xaml
    Controls/SegmentedControl.cs
    Controls/PreviewPane.xaml(.cs)
    ViewModels/MainViewModel.cs
    ViewModels/FileEntry.cs
    Services/SettingsStore.cs
    Services/UpdateService.cs
tests/
  KolayYazdir.Core.Tests/                 hızlı, saf birim testleri
  KolayYazdir.Documents.Tests/            fixture üreten entegrasyon testleri
  KolayYazdir.Printing.Tests/
.github/workflows/release.yml
```

**Neden dört proje:** `Core` içinde Windows API'si olmaması, işin en karmaşık ve en hataya açık parçası olan yerleşim matematiğinin yazıcısız, saniyeler içinde ve tam kapsamlı test edilmesini sağlar. Diğer üç proje ince kabuklardır.

---

### Task 1: Çözüm iskeleti ve kağıt boyutları

**Files:**
- Create: `KolayYazdir.sln`
- Create: `src/KolayYazdir.Core/KolayYazdir.Core.csproj`
- Create: `src/KolayYazdir.Core/Models/PaperFormat.cs`
- Create: `tests/KolayYazdir.Core.Tests/KolayYazdir.Core.Tests.csproj`
- Test: `tests/KolayYazdir.Core.Tests/PaperFormatTests.cs`

**Interfaces:**
- Produces: `enum PaperFormat { A4, A5, A3 }`, `enum Orientation { Portrait, Landscape }`, `readonly record struct SizePt(double Width, double Height)`, `static class Paper { SizePt SizeOf(PaperFormat, Orientation); double MmToPt(double mm); }`

- [ ] **Step 1: Çözümü ve projeleri oluştur**

```bash
cd "D:/Desktop/Software/Personal Projects/Printer Tool"
dotnet new sln -n KolayYazdir
dotnet new classlib -o src/KolayYazdir.Core -f net8.0
dotnet new xunit -o tests/KolayYazdir.Core.Tests -f net8.0
rm src/KolayYazdir.Core/Class1.cs tests/KolayYazdir.Core.Tests/UnitTest1.cs
dotnet sln add src/KolayYazdir.Core tests/KolayYazdir.Core.Tests
dotnet add tests/KolayYazdir.Core.Tests reference src/KolayYazdir.Core
```

`src/KolayYazdir.Core/KolayYazdir.Core.csproj` içindeki `PropertyGroup`'a ekle:

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<LangVersion>12</LangVersion>
```

- [ ] **Step 2: Başarısız testi yaz**

`tests/KolayYazdir.Core.Tests/PaperFormatTests.cs`:

```csharp
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class PaperFormatTests
{
    [Theory]
    [InlineData(PaperFormat.A4, 595.276, 841.890)]
    [InlineData(PaperFormat.A5, 419.528, 595.276)]
    [InlineData(PaperFormat.A3, 841.890, 1190.551)]
    public void SizeOf_portrait_returns_iso_dimensions_in_points(
        PaperFormat format, double expectedWidth, double expectedHeight)
    {
        var size = Paper.SizeOf(format, Orientation.Portrait);

        Assert.Equal(expectedWidth, size.Width, 3);
        Assert.Equal(expectedHeight, size.Height, 3);
    }

    [Fact]
    public void SizeOf_landscape_swaps_width_and_height()
    {
        var portrait = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
        var landscape = Paper.SizeOf(PaperFormat.A4, Orientation.Landscape);

        Assert.Equal(portrait.Height, landscape.Width, 3);
        Assert.Equal(portrait.Width, landscape.Height, 3);
    }

    [Fact]
    public void MmToPt_converts_one_inch_correctly()
    {
        Assert.Equal(72.0, Paper.MmToPt(25.4), 6);
    }
}
```

- [ ] **Step 3: Testin derlenmediğini/başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests`
Expected: FAIL — `The type or namespace name 'Paper' could not be found`

- [ ] **Step 4: Asgari uygulamayı yaz**

`src/KolayYazdir.Core/Models/PaperFormat.cs`:

```csharp
namespace KolayYazdir.Core.Models;

public enum PaperFormat { A4, A5, A3 }

public enum Orientation { Portrait, Landscape }

/// <summary>Punto (1/72 inç) cinsinden bir genişlik-yükseklik çifti.</summary>
public readonly record struct SizePt(double Width, double Height);

public static class Paper
{
    private const double PointsPerInch = 72.0;
    private const double MmPerInch = 25.4;

    public static double MmToPt(double mm) => mm / MmPerInch * PointsPerInch;

    /// <summary>ISO 216 kağıt boyutunu istenen yönde punto olarak verir.</summary>
    public static SizePt SizeOf(PaperFormat format, Orientation orientation)
    {
        var portrait = format switch
        {
            PaperFormat.A4 => new SizePt(MmToPt(210), MmToPt(297)),
            PaperFormat.A5 => new SizePt(MmToPt(148), MmToPt(210)),
            PaperFormat.A3 => new SizePt(MmToPt(297), MmToPt(420)),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        return orientation == Orientation.Portrait
            ? portrait
            : new SizePt(portrait.Height, portrait.Width);
    }
}
```

- [ ] **Step 5: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add KolayYazdir.sln src tests
git commit -m "Çözüm iskeleti ve kağıt boyutu tablosu"
```

---

### Task 2: Sayfa aralığı ayrıştırıcı

Kullanıcının yazdığı `1-5, 8, 11-13` metnini sayfa indekslerine çevirir. Boş metin "tümü" demektir. Bu parça küçük ama kullanıcı girdisiyle doğrudan temas ettiği için hatalı girdilere karşı sağlam olmalı.

**Files:**
- Create: `src/KolayYazdir.Core/Layout/PageRangeParser.cs`
- Test: `tests/KolayYazdir.Core.Tests/PageRangeParserTests.cs`

**Interfaces:**
- Produces: `static class PageRangeParser { IReadOnlyList<int> Parse(string? text, int pageCount); }` — sıfır tabanlı indeks listesi döner, sıralı ve tekrarsız.

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.Core.Tests/PageRangeParserTests.cs`:

```csharp
using KolayYazdir.Core.Layout;

namespace KolayYazdir.Core.Tests;

public class PageRangeParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_text_selects_every_page(string? text)
    {
        Assert.Equal(new[] { 0, 1, 2, 3 }, PageRangeParser.Parse(text, 4));
    }

    [Fact]
    public void Single_page_selects_that_page()
    {
        Assert.Equal(new[] { 2 }, PageRangeParser.Parse("3", 10));
    }

    [Fact]
    public void Range_is_inclusive_on_both_ends()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, PageRangeParser.Parse("1-5", 10));
    }

    [Fact]
    public void Mixed_list_keeps_document_order()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 7, 10, 11, 12 },
            PageRangeParser.Parse("1-5, 8, 11-13", 20));
    }

    [Fact]
    public void Overlapping_ranges_are_deduplicated()
    {
        Assert.Equal(new[] { 0, 1, 2, 3 }, PageRangeParser.Parse("1-3, 2-4", 10));
    }

    [Fact]
    public void Descending_range_is_read_as_ascending()
    {
        Assert.Equal(new[] { 2, 3, 4 }, PageRangeParser.Parse("5-3", 10));
    }

    [Fact]
    public void Pages_past_the_end_are_dropped()
    {
        Assert.Equal(new[] { 8, 9 }, PageRangeParser.Parse("9-40", 10));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-")]
    [InlineData("5-")]
    [InlineData("99")]
    public void Unusable_text_selects_nothing(string text)
    {
        Assert.Empty(PageRangeParser.Parse(text, 10));
    }

    [Fact]
    public void Semicolon_works_as_a_separator_too()
    {
        Assert.Equal(new[] { 0, 4 }, PageRangeParser.Parse("1; 5", 10));
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests --filter PageRangeParserTests`
Expected: FAIL — `PageRangeParser` bulunamıyor

- [ ] **Step 3: Uygulamayı yaz**

`src/KolayYazdir.Core/Layout/PageRangeParser.cs`:

```csharp
using System.Globalization;

namespace KolayYazdir.Core.Layout;

/// <summary>
/// Kullanıcının yazdığı "1-5, 8, 11-13" biçimindeki metni sıfır tabanlı sayfa
/// indekslerine çevirir. Anlaşılmayan parçalar sessizce atlanır; kullanıcı
/// yazarken her tuş vuruşunda hata göstermek yerine önizleme boş kalır.
/// </summary>
public static class PageRangeParser
{
    private static readonly char[] Separators = [',', ';'];

    public static IReadOnlyList<int> Parse(string? text, int pageCount)
    {
        if (pageCount <= 0) return [];
        if (string.IsNullOrWhiteSpace(text)) return Enumerable.Range(0, pageCount).ToList();

        var selected = new SortedSet<int>();

        foreach (var part in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = part.Trim();
            if (token.Length == 0) continue;

            var dash = token.IndexOf('-');
            if (dash < 0)
            {
                // Tek sayfa: belge sonunu aşıyorsa kırpma yapılmaz, atılır.
                if (TryBound(token, pageCount, clampToEnd: false, out var single)) selected.Add(single);
                continue;
            }

            if (!TryBound(token[..dash], pageCount, clampToEnd: false, out var from)) continue;
            if (!TryBound(token[(dash + 1)..], pageCount, clampToEnd: true, out var to)) continue;

            if (from > to) (from, to) = (to, from);
            for (var i = from; i <= to; i++) selected.Add(i);
        }

        return selected.ToList();
    }

    /// <summary>
    /// 1 tabanlı sayfa numarasını sıfır tabanlı indekse çevirir.
    /// <paramref name="clampToEnd"/> açıkken belge sonunu aşan değer hata
    /// sayılmaz, son sayfaya kırpılır — "9-40" yazan kullanıcı çoğu zaman
    /// "sonuna kadar" demek istemiştir. Tek sayfa ve aralığın alt ucu için
    /// kapalıdır: "99" yazan kullanıcı son sayfayı kastetmiyordur.
    /// </summary>
    private static bool TryBound(ReadOnlySpan<char> token, int pageCount, bool clampToEnd, out int index)
    {
        index = -1;
        if (!int.TryParse(token.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var oneBased))
            return false;
        if (oneBased < 1) return false;

        if (oneBased > pageCount)
        {
            if (!clampToEnd) return false;
            oneBased = pageCount;
        }

        index = oneBased - 1;
        return true;
    }
}
```

- [ ] **Step 4: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests --filter PageRangeParserTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/KolayYazdir.Core/Layout/PageRangeParser.cs tests/KolayYazdir.Core.Tests/PageRangeParserTests.cs
git commit -m "Sayfa aralığı ayrıştırıcı"
```

---

### Task 3: Izgara tanımı ve hücre dikdörtgenleri

N'li yerleşimin satır/sütun sayısını ve her hücrenin kağıt üzerindeki dikdörtgenini hesaplar.

**Files:**
- Create: `src/KolayYazdir.Core/Layout/GridSpec.cs`
- Create: `src/KolayYazdir.Core/Layout/CellGrid.cs`
- Test: `tests/KolayYazdir.Core.Tests/GridSpecTests.cs`
- Test: `tests/KolayYazdir.Core.Tests/CellGridTests.cs`

**Interfaces:**
- Consumes: `SizePt`, `Orientation`, `Paper.MmToPt` (Task 1)
- Produces:
  - `enum PagesPerSheet { One = 1, Two = 2, Four = 4, Nine = 9, Sixteen = 16, ThirtyFive = 35 }`
  - `readonly record struct GridSpec(int Columns, int Rows) { int Capacity; static GridSpec For(PagesPerSheet, Orientation); }`
  - `readonly record struct RectPt(double X, double Y, double Width, double Height)`
  - `static class CellGrid { IReadOnlyList<RectPt> Build(SizePt paper, RectPt printable, GridSpec grid); }`
  - `static class LayoutConstants { double MarginMm = 5.0; double GutterMm = 3.0; }`

- [ ] **Step 1: GridSpec testini yaz**

`tests/KolayYazdir.Core.Tests/GridSpecTests.cs`:

```csharp
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class GridSpecTests
{
    [Theory]
    [InlineData(PagesPerSheet.One, 1, 1)]
    [InlineData(PagesPerSheet.Two, 1, 2)]
    [InlineData(PagesPerSheet.Four, 2, 2)]
    [InlineData(PagesPerSheet.Nine, 3, 3)]
    [InlineData(PagesPerSheet.Sixteen, 4, 4)]
    [InlineData(PagesPerSheet.ThirtyFive, 5, 7)]
    public void Portrait_grids_match_the_spec(PagesPerSheet nUp, int columns, int rows)
    {
        var grid = GridSpec.For(nUp, Orientation.Portrait);

        Assert.Equal(columns, grid.Columns);
        Assert.Equal(rows, grid.Rows);
    }

    [Theory]
    [InlineData(PagesPerSheet.Two, 2, 1)]
    [InlineData(PagesPerSheet.ThirtyFive, 7, 5)]
    [InlineData(PagesPerSheet.Four, 2, 2)]
    public void Landscape_swaps_columns_and_rows(PagesPerSheet nUp, int columns, int rows)
    {
        var grid = GridSpec.For(nUp, Orientation.Landscape);

        Assert.Equal(columns, grid.Columns);
        Assert.Equal(rows, grid.Rows);
    }

    [Fact]
    public void Capacity_is_columns_times_rows()
    {
        Assert.Equal(35, GridSpec.For(PagesPerSheet.ThirtyFive, Orientation.Portrait).Capacity);
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests --filter GridSpecTests`
Expected: FAIL — `GridSpec` bulunamıyor

- [ ] **Step 3: GridSpec'i yaz**

`src/KolayYazdir.Core/Layout/GridSpec.cs`:

```csharp
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Layout;

/// <summary>Bir yaprağa kaç kaynak sayfa yerleşeceği.</summary>
public enum PagesPerSheet
{
    One = 1,
    Two = 2,
    Four = 4,
    Nine = 9,
    Sixteen = 16,
    ThirtyFive = 35
}

/// <summary>Yaprak üzerindeki hücre ızgarasının sütun ve satır sayısı.</summary>
public readonly record struct GridSpec(int Columns, int Rows)
{
    public int Capacity => Columns * Rows;

    /// <summary>
    /// Dikey kağıtta tablo spec'teki gibidir; yatay kağıtta sütun ve satır
    /// yer değiştirir, böylece hücre oranı kağıt oranını takip eder.
    /// </summary>
    public static GridSpec For(PagesPerSheet pagesPerSheet, Orientation orientation)
    {
        var portrait = pagesPerSheet switch
        {
            PagesPerSheet.One => new GridSpec(1, 1),
            PagesPerSheet.Two => new GridSpec(1, 2),
            PagesPerSheet.Four => new GridSpec(2, 2),
            PagesPerSheet.Nine => new GridSpec(3, 3),
            PagesPerSheet.Sixteen => new GridSpec(4, 4),
            PagesPerSheet.ThirtyFive => new GridSpec(5, 7),
            _ => throw new ArgumentOutOfRangeException(nameof(pagesPerSheet))
        };

        return orientation == Orientation.Portrait
            ? portrait
            : new GridSpec(portrait.Rows, portrait.Columns);
    }
}
```

- [ ] **Step 4: CellGrid testini yaz**

`tests/KolayYazdir.Core.Tests/CellGridTests.cs`:

```csharp
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class CellGridTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);

    /// <summary>Basılabilir alan kısıtı olmayan, tüm kağıdı kaplayan bir yazıcı.</summary>
    private static RectPt FullBleed(SizePt paper) => new(0, 0, paper.Width, paper.Height);

    [Fact]
    public void Single_cell_is_the_page_inset_by_the_margin()
    {
        var cells = CellGrid.Build(A4, FullBleed(A4), new GridSpec(1, 1));
        var margin = Paper.MmToPt(5);

        var cell = Assert.Single(cells);
        Assert.Equal(margin, cell.X, 3);
        Assert.Equal(margin, cell.Y, 3);
        Assert.Equal(A4.Width - 2 * margin, cell.Width, 3);
        Assert.Equal(A4.Height - 2 * margin, cell.Height, 3);
    }

    [Fact]
    public void Two_by_two_grid_splits_the_content_area_with_a_gutter()
    {
        var cells = CellGrid.Build(A4, FullBleed(A4), new GridSpec(2, 2));
        var margin = Paper.MmToPt(5);
        var gutter = Paper.MmToPt(3);
        var expectedWidth = (A4.Width - 2 * margin - gutter) / 2;
        var expectedHeight = (A4.Height - 2 * margin - gutter) / 2;

        Assert.Equal(4, cells.Count);
        Assert.All(cells, c =>
        {
            Assert.Equal(expectedWidth, c.Width, 3);
            Assert.Equal(expectedHeight, c.Height, 3);
        });
    }

    [Fact]
    public void Cells_are_ordered_left_to_right_then_top_to_bottom()
    {
        var cells = CellGrid.Build(A4, FullBleed(A4), new GridSpec(2, 2));

        Assert.True(cells[0].X < cells[1].X, "birinci hücre ikincinin solunda olmalı");
        Assert.Equal(cells[0].Y, cells[1].Y, 3);
        Assert.True(cells[2].Y > cells[0].Y, "üçüncü hücre birincinin altında olmalı");
        Assert.Equal(cells[0].X, cells[2].X, 3);
    }

    [Fact]
    public void Hardware_margin_larger_than_five_millimetres_wins()
    {
        var hardMargin = Paper.MmToPt(12);
        var printable = new RectPt(hardMargin, hardMargin,
            A4.Width - 2 * hardMargin, A4.Height - 2 * hardMargin);

        var cell = Assert.Single(CellGrid.Build(A4, printable, new GridSpec(1, 1)));

        Assert.Equal(hardMargin, cell.X, 3);
        Assert.Equal(A4.Width - 2 * hardMargin, cell.Width, 3);
    }

    [Fact]
    public void Thirty_five_up_produces_thirty_five_cells_that_fit_the_page()
    {
        var cells = CellGrid.Build(A4, FullBleed(A4), GridSpec.For(PagesPerSheet.ThirtyFive, Orientation.Portrait));

        Assert.Equal(35, cells.Count);
        Assert.All(cells, c =>
        {
            Assert.True(c.Width > 0 && c.Height > 0);
            Assert.True(c.X + c.Width <= A4.Width + 0.001);
            Assert.True(c.Y + c.Height <= A4.Height + 0.001);
        });
    }
}
```

- [ ] **Step 5: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests --filter CellGridTests`
Expected: FAIL — `CellGrid` bulunamıyor

- [ ] **Step 6: CellGrid'i yaz**

`src/KolayYazdir.Core/Layout/CellGrid.cs`:

```csharp
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
```

- [ ] **Step 7: Tüm testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add src/KolayYazdir.Core/Layout tests/KolayYazdir.Core.Tests
git commit -m "Izgara tanımı ve hücre dikdörtgeni hesabı"
```

---

### Task 4: Yerleştirme matematiği

Spec'teki beş adımlı ölçekleme kuralının tam karşılığı. Yerleşim motorunun en kritik parçası budur — "sayfaya sığdır kapalıyken gerçek boyut" davranışı burada yaşar.

**Files:**
- Create: `src/KolayYazdir.Core/Layout/Placement.cs`
- Test: `tests/KolayYazdir.Core.Tests/PlacementTests.cs`

**Interfaces:**
- Consumes: `RectPt`, `SizePt` (Task 1, 3)
- Produces: `readonly record struct PlacedPage(int SourceIndex, RectPt Destination, int RotationDegrees)`, `static class Placement { PlacedPage Fit(int sourceIndex, SizePt source, RectPt cell, bool fitToPage, bool autoRotate); }`

`RotationDegrees` 0 veya 90'dır; 90 saat yönünde dönüşü ifade eder. `Destination` döndürülmüş hâlin kapladığı dikdörtgendir.

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.Core.Tests/PlacementTests.cs`:

```csharp
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class PlacementTests
{
    private static readonly RectPt Cell = new(100, 200, 400, 300);

    [Fact]
    public void Smaller_content_keeps_its_real_size_when_fit_is_off()
    {
        var placed = Placement.Fit(0, new SizePt(200, 150), Cell, fitToPage: false, autoRotate: false);

        Assert.Equal(200, placed.Destination.Width, 3);
        Assert.Equal(150, placed.Destination.Height, 3);
    }

    [Fact]
    public void Smaller_content_is_enlarged_when_fit_is_on()
    {
        var placed = Placement.Fit(0, new SizePt(200, 150), Cell, fitToPage: true, autoRotate: false);

        Assert.Equal(400, placed.Destination.Width, 3);
        Assert.Equal(300, placed.Destination.Height, 3);
    }

    [Fact]
    public void Oversized_content_is_shrunk_even_when_fit_is_off()
    {
        var placed = Placement.Fit(0, new SizePt(800, 600), Cell, fitToPage: false, autoRotate: false);

        Assert.Equal(400, placed.Destination.Width, 3);
        Assert.Equal(300, placed.Destination.Height, 3);
    }

    [Fact]
    public void Aspect_ratio_is_preserved()
    {
        var placed = Placement.Fit(0, new SizePt(1000, 250), Cell, fitToPage: true, autoRotate: false);

        Assert.Equal(400, placed.Destination.Width, 3);
        Assert.Equal(100, placed.Destination.Height, 3);
    }

    [Fact]
    public void Content_is_centred_inside_the_cell()
    {
        var placed = Placement.Fit(0, new SizePt(200, 150), Cell, fitToPage: false, autoRotate: false);

        Assert.Equal(100 + (400 - 200) / 2.0, placed.Destination.X, 3);
        Assert.Equal(200 + (300 - 150) / 2.0, placed.Destination.Y, 3);
    }

    [Fact]
    public void Portrait_content_rotates_into_a_landscape_cell()
    {
        var placed = Placement.Fit(0, new SizePt(300, 400), Cell, fitToPage: false, autoRotate: true);

        Assert.Equal(90, placed.RotationDegrees);
        Assert.Equal(400, placed.Destination.Width, 3);
        Assert.Equal(300, placed.Destination.Height, 3);
    }

    [Fact]
    public void Rotation_is_skipped_when_auto_rotate_is_off()
    {
        var placed = Placement.Fit(0, new SizePt(300, 400), Cell, fitToPage: false, autoRotate: false);

        Assert.Equal(0, placed.RotationDegrees);
    }

    [Fact]
    public void Matching_orientation_is_left_alone()
    {
        var placed = Placement.Fit(0, new SizePt(200, 150), Cell, fitToPage: false, autoRotate: true);

        Assert.Equal(0, placed.RotationDegrees);
    }

    [Fact]
    public void Rotation_lets_tall_content_print_larger()
    {
        var withoutRotation = Placement.Fit(0, new SizePt(300, 400), Cell, fitToPage: true, autoRotate: false);
        var withRotation = Placement.Fit(0, new SizePt(300, 400), Cell, fitToPage: true, autoRotate: true);

        var areaWithout = withoutRotation.Destination.Width * withoutRotation.Destination.Height;
        var areaWith = withRotation.Destination.Width * withRotation.Destination.Height;

        Assert.True(areaWith > areaWithout, "döndürünce daha büyük basılmalı");
    }

    [Fact]
    public void Square_cell_never_triggers_rotation()
    {
        var square = new RectPt(0, 0, 300, 300);
        var placed = Placement.Fit(0, new SizePt(200, 400), square, fitToPage: false, autoRotate: true);

        Assert.Equal(0, placed.RotationDegrees);
    }

    [Fact]
    public void Source_index_is_carried_through()
    {
        var placed = Placement.Fit(7, new SizePt(100, 100), Cell, fitToPage: false, autoRotate: false);

        Assert.Equal(7, placed.SourceIndex);
    }

    [Fact]
    public void Degenerate_source_size_produces_an_empty_destination()
    {
        var placed = Placement.Fit(0, new SizePt(0, 0), Cell, fitToPage: true, autoRotate: true);

        Assert.Equal(0, placed.Destination.Width, 3);
        Assert.Equal(0, placed.Destination.Height, 3);
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests --filter PlacementTests`
Expected: FAIL — `Placement` bulunamıyor

- [ ] **Step 3: Uygulamayı yaz**

`src/KolayYazdir.Core/Layout/Placement.cs`:

```csharp
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Layout;

/// <summary>
/// Bir kaynak sayfanın yaprak üzerindeki nihai yeri. <see cref="RotationDegrees"/>
/// 0 veya 90'dır ve saat yönünde dönüşü ifade eder; <see cref="Destination"/>
/// döndürülmüş hâlin kapladığı dikdörtgendir.
/// </summary>
public readonly record struct PlacedPage(int SourceIndex, RectPt Destination, int RotationDegrees);

public static class Placement
{
    /// <summary>
    /// Spec'teki beş adımlı kural: gerekiyorsa döndür, oranı koruyarak ölçekle,
    /// sığdırma kapalıysa büyütme, hücrenin ortasına yerleştir.
    /// </summary>
    public static PlacedPage Fit(int sourceIndex, SizePt source, RectPt cell, bool fitToPage, bool autoRotate)
    {
        if (source.Width <= 0 || source.Height <= 0 || cell.Width <= 0 || cell.Height <= 0)
        {
            return new PlacedPage(sourceIndex, cell with { Width = 0, Height = 0 }, 0);
        }

        var rotate = autoRotate && WouldRotationHelp(source, cell);
        var effective = rotate ? new SizePt(source.Height, source.Width) : source;

        var scale = Math.Min(cell.Width / effective.Width, cell.Height / effective.Height);

        // Sığdırma kapalıyken gerçek boyut korunur: sadece taşıyorsa küçültülür,
        // asla büyütülmez. Vesikalık gibi ölçüsü önemli işler bozulmasın.
        if (!fitToPage) scale = Math.Min(scale, 1.0);

        var width = effective.Width * scale;
        var height = effective.Height * scale;

        var destination = new RectPt(
            cell.X + (cell.Width - width) / 2,
            cell.Y + (cell.Height - height) / 2,
            width,
            height);

        return new PlacedPage(sourceIndex, destination, rotate ? 90 : 0);
    }

    /// <summary>
    /// Kaynak ile hücrenin yön oranları zıt işaretliyse döndürmek içeriği
    /// büyütür. Kare hücrede (veya kare içerikte) kazanç yoktur, döndürülmez.
    /// </summary>
    private static bool WouldRotationHelp(SizePt source, RectPt cell)
    {
        var sourceIsWide = source.Width > source.Height;
        var cellIsWide = cell.Width > cell.Height;

        var sourceIsSquare = Math.Abs(source.Width - source.Height) < 1e-9;
        var cellIsSquare = Math.Abs(cell.Width - cell.Height) < 1e-9;

        if (sourceIsSquare || cellIsSquare) return false;

        return sourceIsWide != cellIsWide;
    }
}
```

- [ ] **Step 4: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests --filter PlacementTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/KolayYazdir.Core/Layout/Placement.cs tests/KolayYazdir.Core.Tests/PlacementTests.cs
git commit -m "Ölçekleme, döndürme ve ortalama kuralları"
```

---

### Task 5: Ayar modeli ve tek yönlü yaprak kurgusu

Yerleşim motorunun ilk hâli: sayfaları sırayla ızgaraya doldurup yaprak listesi üretir. Dupleks ve kopya sonraki görevlerde eklenir.

**Files:**
- Create: `src/KolayYazdir.Core/Models/PrintSettings.cs`
- Create: `src/KolayYazdir.Core/Models/SourcePageInfo.cs`
- Create: `src/KolayYazdir.Core/Models/Sheet.cs`
- Create: `src/KolayYazdir.Core/Layout/LayoutEngine.cs`
- Test: `tests/KolayYazdir.Core.Tests/LayoutEngineTests.cs`

**Interfaces:**
- Consumes: her şey Task 1–4'ten
- Produces:
  - `enum ColorMode { Color, Monochrome }`, `enum DuplexMode { Simplex, Duplex }`, `enum SheetSide { Front, Back }`
  - `readonly record struct SourcePageInfo(int Index, SizePt Size)`
  - `sealed record Sheet(int Index, SheetSide Side, SizePt Paper, IReadOnlyList<PlacedPage> Pages)`
  - `sealed record PrintSettings` — aşağıdaki tüm alanlar
  - `static class LayoutEngine { IReadOnlyList<Sheet> Build(IReadOnlyList<SourcePageInfo> pages, PrintSettings settings, RectPt printableArea); }`

- [ ] **Step 1: Modelleri yaz**

`src/KolayYazdir.Core/Models/SourcePageInfo.cs`:

```csharp
namespace KolayYazdir.Core.Models;

/// <summary>
/// Yerleşim motorunun bir kaynak sayfa hakkında bilmesi gereken her şey.
/// Motor sayfanın içeriğini görmez, sadece sırasını ve boyutunu bilir.
/// </summary>
/// <param name="Index">Birleştirilmiş sayfa dizisindeki sıfır tabanlı yeri.</param>
/// <param name="Size">Sayfanın punto cinsinden boyutu.</param>
public readonly record struct SourcePageInfo(int Index, SizePt Size);
```

`src/KolayYazdir.Core/Models/Sheet.cs`:

```csharp
using KolayYazdir.Core.Layout;

namespace KolayYazdir.Core.Models;

/// <summary>Yaprağın hangi yüzü.</summary>
public enum SheetSide { Front, Back }

/// <summary>
/// Basılacak tek bir kağıt yüzü. Çizim yapan taraf sadece bunu görür;
/// hangi ayarların bu sonucu doğurduğunu bilmesi gerekmez.
/// </summary>
/// <param name="Index">Kaçıncı fiziksel yaprak (sıfır tabanlı).</param>
/// <param name="Pages">Boş yüzlerde boş liste.</param>
public sealed record Sheet(int Index, SheetSide Side, SizePt Paper, IReadOnlyList<PlacedPage> Pages)
{
    public bool IsBlank => Pages.Count == 0;
}
```

`src/KolayYazdir.Core/Models/PrintSettings.cs`:

```csharp
using KolayYazdir.Core.Layout;

namespace KolayYazdir.Core.Models;

public enum ColorMode { Color, Monochrome }

public enum DuplexMode { Simplex, Duplex }

/// <summary>
/// Kullanıcının seçtiği her şey. Değişmez bir kayıt olduğu için önizleme
/// yeniden hesaplanırken yarı değişmiş bir ara duruma düşmek imkansızdır.
/// </summary>
public sealed record PrintSettings
{
    public PaperFormat Paper { get; init; } = PaperFormat.A4;
    public Orientation Orientation { get; init; } = Orientation.Portrait;
    public ColorMode Color { get; init; } = ColorMode.Monochrome;
    public DuplexMode Duplex { get; init; } = DuplexMode.Simplex;
    public PagesPerSheet PagesPerSheet { get; init; } = PagesPerSheet.One;

    /// <summary>Spec gereği varsayılan kapalı: gerçek boyut korunur.</summary>
    public bool FitToPage { get; init; }

    public bool AutoRotate { get; init; } = true;

    /// <summary>Boş veya null ise tüm sayfalar.</summary>
    public string? PageRange { get; init; }

    public int Copies { get; init; } = 1;

    /// <summary>Sürücüye gönderilecek kağıt cinsi kimliği; null ise dokunulmaz.</summary>
    public int? MediaTypeId { get; init; }

    /// <summary>
    /// Çevirme kenarı kullanıcıya sorulmaz, yönden türetilir: dikeyse uzun
    /// kenar, yataysa kısa kenar.
    /// </summary>
    public DuplexBinding Binding =>
        Orientation == Orientation.Portrait ? DuplexBinding.LongEdge : DuplexBinding.ShortEdge;
}

public enum DuplexBinding { LongEdge, ShortEdge }
```

- [ ] **Step 2: Başarısız testi yaz**

`tests/KolayYazdir.Core.Tests/LayoutEngineTests.cs`:

```csharp
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class LayoutEngineTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    /// <summary>A4 boyutunda <paramref name="count"/> adet kaynak sayfa.</summary>
    private static List<SourcePageInfo> Pages(int count) =>
        Enumerable.Range(0, count).Select(i => new SourcePageInfo(i, A4)).ToList();

    [Fact]
    public void One_up_produces_one_sheet_per_page()
    {
        var sheets = LayoutEngine.Build(Pages(3), new PrintSettings(), FullBleed);

        Assert.Equal(3, sheets.Count);
        Assert.All(sheets, s => Assert.Single(s.Pages));
    }

    [Fact]
    public void Four_up_packs_four_pages_onto_each_sheet()
    {
        var settings = new PrintSettings { PagesPerSheet = PagesPerSheet.Four };

        var sheets = LayoutEngine.Build(Pages(8), settings, FullBleed);

        Assert.Equal(2, sheets.Count);
        Assert.All(sheets, s => Assert.Equal(4, s.Pages.Count));
    }

    [Fact]
    public void Last_sheet_is_partially_filled_when_pages_run_out()
    {
        var settings = new PrintSettings { PagesPerSheet = PagesPerSheet.Four };

        var sheets = LayoutEngine.Build(Pages(6), settings, FullBleed);

        Assert.Equal(2, sheets.Count);
        Assert.Equal(4, sheets[0].Pages.Count);
        Assert.Equal(2, sheets[1].Pages.Count);
    }

    [Fact]
    public void Pages_are_placed_in_document_order()
    {
        var settings = new PrintSettings { PagesPerSheet = PagesPerSheet.Four };

        var sheets = LayoutEngine.Build(Pages(4), settings, FullBleed);

        Assert.Equal([0, 1, 2, 3], sheets[0].Pages.Select(p => p.SourceIndex));
    }

    [Fact]
    public void Page_range_narrows_the_job()
    {
        var settings = new PrintSettings { PageRange = "2-3" };

        var sheets = LayoutEngine.Build(Pages(10), settings, FullBleed);

        Assert.Equal(2, sheets.Count);
        Assert.Equal(1, sheets[0].Pages[0].SourceIndex);
        Assert.Equal(2, sheets[1].Pages[0].SourceIndex);
    }

    [Fact]
    public void Landscape_sheets_use_swapped_paper_dimensions()
    {
        var settings = new PrintSettings { Orientation = Orientation.Landscape };

        var sheet = LayoutEngine.Build(Pages(1), settings, FullBleed).Single();

        Assert.Equal(A4.Height, sheet.Paper.Width, 3);
        Assert.Equal(A4.Width, sheet.Paper.Height, 3);
    }

    [Fact]
    public void Empty_input_produces_no_sheets()
    {
        Assert.Empty(LayoutEngine.Build([], new PrintSettings(), FullBleed));
    }

    [Fact]
    public void Every_simplex_sheet_is_a_front()
    {
        var sheets = LayoutEngine.Build(Pages(3), new PrintSettings(), FullBleed);

        Assert.All(sheets, s => Assert.Equal(SheetSide.Front, s.Side));
    }

    [Fact]
    public void Sheet_indexes_are_sequential_from_zero()
    {
        var sheets = LayoutEngine.Build(Pages(3), new PrintSettings(), FullBleed);

        Assert.Equal([0, 1, 2], sheets.Select(s => s.Index));
    }
}
```

- [ ] **Step 3: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests --filter LayoutEngineTests`
Expected: FAIL — `LayoutEngine` bulunamıyor

- [ ] **Step 4: Motorun tek yönlü hâlini yaz**

`src/KolayYazdir.Core/Layout/LayoutEngine.cs`:

```csharp
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Layout;

/// <summary>
/// Yerleşimin tamamı. Saf bir fonksiyondur: hiçbir çizim yapmaz, hiçbir dosya
/// okumaz, hiçbir yazıcıya dokunmaz. Bu yüzden tümüyle ve hızla test edilebilir.
/// </summary>
public static class LayoutEngine
{
    /// <param name="pages">Tüm dosyaların birleştirilmiş sayfa dizisi.</param>
    /// <param name="printableArea">Yazıcının basabildiği alan, kağıt koordinatlarında.</param>
    public static IReadOnlyList<Sheet> Build(
        IReadOnlyList<SourcePageInfo> pages,
        PrintSettings settings,
        RectPt printableArea)
    {
        var selected = PageRangeParser.Parse(settings.PageRange, pages.Count);
        if (selected.Count == 0) return [];

        var paper = Paper.SizeOf(settings.Paper, settings.Orientation);
        var grid = GridSpec.For(settings.PagesPerSheet, settings.Orientation);
        var cells = CellGrid.Build(paper, printableArea, grid);

        var sheets = new List<Sheet>();
        for (var offset = 0; offset < selected.Count; offset += grid.Capacity)
        {
            var placed = new List<PlacedPage>(grid.Capacity);
            var take = Math.Min(grid.Capacity, selected.Count - offset);

            for (var slot = 0; slot < take; slot++)
            {
                var page = pages[selected[offset + slot]];
                placed.Add(Placement.Fit(page.Index, page.Size, cells[slot], settings.FitToPage, settings.AutoRotate));
            }

            sheets.Add(new Sheet(sheets.Count, SheetSide.Front, paper, placed));
        }

        return sheets;
    }
}
```

- [ ] **Step 5: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/KolayYazdir.Core tests/KolayYazdir.Core.Tests
git commit -m "Ayar modeli ve tek yönlü yaprak kurgusu"
```

---

### Task 6: Önlü arkalı yaprak eşleşmesi

Dupleks açıkken her fiziksel yaprak iki `Sheet` üretir: ön ve arka. 4'lü + önlü arkalıda 1. yaprağın önü 1–4, arkası 5–8 sayfalarıdır.

**Files:**
- Modify: `src/KolayYazdir.Core/Layout/LayoutEngine.cs`
- Test: `tests/KolayYazdir.Core.Tests/LayoutEngineDuplexTests.cs`

**Interfaces:**
- Produces: `LayoutEngine.Build` imzası değişmez; dupleks davranışı `settings.Duplex` üzerinden gelir.

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.Core.Tests/LayoutEngineDuplexTests.cs`:

```csharp
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class LayoutEngineDuplexTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    private static List<SourcePageInfo> Pages(int count) =>
        Enumerable.Range(0, count).Select(i => new SourcePageInfo(i, A4)).ToList();

    private static PrintSettings Duplex(PagesPerSheet nUp = PagesPerSheet.One) =>
        new() { Duplex = DuplexMode.Duplex, PagesPerSheet = nUp };

    [Fact]
    public void Sides_alternate_front_then_back()
    {
        var sheets = LayoutEngine.Build(Pages(4), Duplex(), FullBleed);

        Assert.Equal(
            [SheetSide.Front, SheetSide.Back, SheetSide.Front, SheetSide.Back],
            sheets.Select(s => s.Side));
    }

    [Fact]
    public void Both_sides_of_a_leaf_share_a_sheet_index()
    {
        var sheets = LayoutEngine.Build(Pages(4), Duplex(), FullBleed);

        Assert.Equal([0, 0, 1, 1], sheets.Select(s => s.Index));
    }

    [Fact]
    public void Four_up_duplex_puts_pages_one_to_four_on_the_front()
    {
        var sheets = LayoutEngine.Build(Pages(8), Duplex(PagesPerSheet.Four), FullBleed);

        Assert.Equal([0, 1, 2, 3], sheets[0].Pages.Select(p => p.SourceIndex));
        Assert.Equal([4, 5, 6, 7], sheets[1].Pages.Select(p => p.SourceIndex));
    }

    [Fact]
    public void Odd_page_count_leaves_a_blank_back()
    {
        var sheets = LayoutEngine.Build(Pages(3), Duplex(), FullBleed);

        Assert.Equal(4, sheets.Count);
        Assert.True(sheets[3].IsBlank);
        Assert.Equal(SheetSide.Back, sheets[3].Side);
    }

    [Fact]
    public void A_single_page_still_gets_a_blank_back()
    {
        var sheets = LayoutEngine.Build(Pages(1), Duplex(), FullBleed);

        Assert.Equal(2, sheets.Count);
        Assert.False(sheets[0].IsBlank);
        Assert.True(sheets[1].IsBlank);
    }

    [Fact]
    public void Blank_back_carries_the_paper_size()
    {
        var sheets = LayoutEngine.Build(Pages(1), Duplex(), FullBleed);

        Assert.Equal(A4.Width, sheets[1].Paper.Width, 3);
    }

    [Fact]
    public void Simplex_never_produces_a_blank_side()
    {
        var sheets = LayoutEngine.Build(Pages(3), new PrintSettings(), FullBleed);

        Assert.DoesNotContain(sheets, s => s.IsBlank);
    }

    [Fact]
    public void Portrait_binds_on_the_long_edge()
    {
        Assert.Equal(DuplexBinding.LongEdge, new PrintSettings().Binding);
    }

    [Fact]
    public void Landscape_binds_on_the_short_edge()
    {
        var settings = new PrintSettings { Orientation = Orientation.Landscape };

        Assert.Equal(DuplexBinding.ShortEdge, settings.Binding);
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests --filter LayoutEngineDuplexTests`
Expected: FAIL — kenarlar hep `Front`, yaprak indeksleri 0,1,2,3

- [ ] **Step 3: Motoru dupleks üretecek şekilde değiştir**

`LayoutEngine.Build` gövdesindeki yaprak döngüsünü şununla değiştir:

```csharp
        var sheets = new List<Sheet>();
        var sidesPerLeaf = settings.Duplex == DuplexMode.Duplex ? 2 : 1;
        var leafIndex = 0;
        var side = SheetSide.Front;

        for (var offset = 0; offset < selected.Count; offset += grid.Capacity)
        {
            var placed = new List<PlacedPage>(grid.Capacity);
            var take = Math.Min(grid.Capacity, selected.Count - offset);

            for (var slot = 0; slot < take; slot++)
            {
                var page = pages[selected[offset + slot]];
                placed.Add(Placement.Fit(page.Index, page.Size, cells[slot], settings.FitToPage, settings.AutoRotate));
            }

            sheets.Add(new Sheet(leafIndex, side, paper, placed));

            if (sidesPerLeaf == 1)
            {
                leafIndex++;
                continue;
            }

            if (side == SheetSide.Front)
            {
                side = SheetSide.Back;
            }
            else
            {
                side = SheetSide.Front;
                leafIndex++;
            }
        }

        // Son yaprağın arkası doldurulamadıysa boş bir yüz olarak eklenir;
        // yazıcı yaprağı çevirip boş basar, sıra kaymaz.
        if (sidesPerLeaf == 2 && side == SheetSide.Back)
        {
            sheets.Add(new Sheet(leafIndex, SheetSide.Back, paper, []));
        }

        return sheets;
```

- [ ] **Step 4: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/KolayYazdir.Core/Layout/LayoutEngine.cs tests/KolayYazdir.Core.Tests/LayoutEngineDuplexTests.cs
git commit -m "Önlü arkalı yaprak eşleşmesi ve boş arka yüz"
```

---

### Task 7: Kopya ve harmanlama

Sürücü kopyalamayı desteklemiyorsa yapraklar uygulama tarafında tekrarlanır. Harmanlama her zaman açıktır: 1,2,3 – 1,2,3.

**Files:**
- Modify: `src/KolayYazdir.Core/Layout/LayoutEngine.cs`
- Test: `tests/KolayYazdir.Core.Tests/LayoutEngineCopiesTests.cs`

**Interfaces:**
- Produces: `static IReadOnlyList<Sheet> LayoutEngine.Repeat(IReadOnlyList<Sheet> sheets, int copies)` — yaprak listesini harmanlanmış şekilde çoğaltır, `Index` alanlarını yeniden numaralar.

`Build` kopya uygulamaz; kopyayı sürücüye bırakıp bırakmama kararı yazdırma katmanına aittir (Task 14). Bu ayrım motorun saf kalmasını sağlar.

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.Core.Tests/LayoutEngineCopiesTests.cs`:

```csharp
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class LayoutEngineCopiesTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    private static IReadOnlyList<Sheet> ThreeSheets() =>
        LayoutEngine.Build(
            Enumerable.Range(0, 3).Select(i => new SourcePageInfo(i, A4)).ToList(),
            new PrintSettings(),
            FullBleed);

    [Fact]
    public void One_copy_returns_the_list_unchanged()
    {
        var sheets = ThreeSheets();

        Assert.Equal(3, LayoutEngine.Repeat(sheets, 1).Count);
    }

    [Fact]
    public void Copies_are_collated_not_grouped_by_page()
    {
        var repeated = LayoutEngine.Repeat(ThreeSheets(), 2);

        var sourceOrder = repeated.Select(s => s.Pages[0].SourceIndex).ToList();

        Assert.Equal([0, 1, 2, 0, 1, 2], sourceOrder);
    }

    [Fact]
    public void Sheet_count_multiplies_by_the_copy_count()
    {
        Assert.Equal(9, LayoutEngine.Repeat(ThreeSheets(), 3).Count);
    }

    [Fact]
    public void Sheet_indexes_are_renumbered_across_copies()
    {
        var repeated = LayoutEngine.Repeat(ThreeSheets(), 2);

        Assert.Equal([0, 1, 2, 3, 4, 5], repeated.Select(s => s.Index));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Non_positive_copy_counts_are_treated_as_one(int copies)
    {
        Assert.Equal(3, LayoutEngine.Repeat(ThreeSheets(), copies).Count);
    }

    [Fact]
    public void Duplex_leaf_pairing_survives_repetition()
    {
        var duplex = LayoutEngine.Build(
            [new SourcePageInfo(0, A4)],
            new PrintSettings { Duplex = DuplexMode.Duplex },
            FullBleed);

        var repeated = LayoutEngine.Repeat(duplex, 2);

        Assert.Equal([0, 0, 1, 1], repeated.Select(s => s.Index));
        Assert.Equal(
            [SheetSide.Front, SheetSide.Back, SheetSide.Front, SheetSide.Back],
            repeated.Select(s => s.Side));
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests --filter LayoutEngineCopiesTests`
Expected: FAIL — `Repeat` metodu yok

- [ ] **Step 3: `Repeat`'i yaz**

`LayoutEngine` sınıfının içine ekle:

```csharp
    /// <summary>
    /// Yaprak listesini harmanlanmış olarak çoğaltır (1,2,3 – 1,2,3). Sürücü
    /// kopyalamayı desteklemediğinde kullanılır. Dupleks yapraklarda ön/arka
    /// eşleşmesi korunur, yaprak numaraları baştan verilir.
    /// </summary>
    public static IReadOnlyList<Sheet> Repeat(IReadOnlyList<Sheet> sheets, int copies)
    {
        if (copies <= 1 || sheets.Count == 0) return sheets;

        // Bir kopyadaki farklı fiziksel yaprak sayısı; sonraki kopyanın
        // numaraları buradan devam eder.
        var leavesPerCopy = sheets[^1].Index + 1;

        var result = new List<Sheet>(sheets.Count * copies);
        for (var copy = 0; copy < copies; copy++)
        {
            foreach (var sheet in sheets)
            {
                result.Add(sheet with { Index = sheet.Index + copy * leavesPerCopy });
            }
        }

        return result;
    }
```

- [ ] **Step 4: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/KolayYazdir.Core/Layout/LayoutEngine.cs tests/KolayYazdir.Core.Tests/LayoutEngineCopiesTests.cs
git commit -m "Harmanlanmış kopya çoğaltma"
```

---

### Task 8: PDF rasterleştirici

PDFium'u projenin geri kalanından yalıtan ince bir kabuk. **Önemli:** testler bizim arayüzümüze karşı yazılır, PDFtoImage'ın API'sine karşı değil. Paketin metot adları beklenenden farklıysa sadece bu dosya değişir, başka hiçbir görev etkilenmez.

**Files:**
- Create: `src/KolayYazdir.Documents/KolayYazdir.Documents.csproj`
- Create: `src/KolayYazdir.Documents/IPageRasterizer.cs`
- Create: `src/KolayYazdir.Documents/PdfRasterizer.cs`
- Create: `tests/KolayYazdir.Documents.Tests/KolayYazdir.Documents.Tests.csproj`
- Create: `tests/KolayYazdir.Documents.Tests/PdfFixtures.cs`
- Test: `tests/KolayYazdir.Documents.Tests/PdfRasterizerTests.cs`

**Interfaces:**
- Consumes: `SizePt` (Task 1)
- Produces:
  - `sealed record RasterPage(int WidthPx, int HeightPx, byte[] Bgra)` — satır dolgusu yok, uzunluk `WidthPx * HeightPx * 4`
  - `interface IPageRasterizer : IDisposable { int PageCount { get; } SizePt PageSize(int index); RasterPage Render(int index, double dpi); }`
  - `sealed class PdfRasterizer : IPageRasterizer { PdfRasterizer(string path); }`

- [ ] **Step 1: Projeleri kur**

```bash
cd "D:/Desktop/Software/Personal Projects/Printer Tool"
dotnet new classlib -o src/KolayYazdir.Documents -f net8.0-windows
dotnet new xunit -o tests/KolayYazdir.Documents.Tests -f net8.0-windows
rm src/KolayYazdir.Documents/Class1.cs tests/KolayYazdir.Documents.Tests/UnitTest1.cs
dotnet sln add src/KolayYazdir.Documents tests/KolayYazdir.Documents.Tests
dotnet add src/KolayYazdir.Documents reference src/KolayYazdir.Core
dotnet add tests/KolayYazdir.Documents.Tests reference src/KolayYazdir.Documents
dotnet add src/KolayYazdir.Documents package PDFtoImage --version 5.4.0
dotnet add tests/KolayYazdir.Documents.Tests package PDFsharp --version 6.1.1
```

`src/KolayYazdir.Documents/KolayYazdir.Documents.csproj` içine `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<UseWPF>false</UseWPF>` ekle.

> PDFsharp yalnızca test fixture'ı üretmek için kullanılır; ürün kodunda yeri yoktur.
> Sürüm reddedilirse `dotnet package search PDFsharp --exact-match` ile güncel
> kararlı sürümü bul ve onu kullan.

- [ ] **Step 2: Fixture üreticisini yaz**

`tests/KolayYazdir.Documents.Tests/PdfFixtures.cs`:

```csharp
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace KolayYazdir.Documents.Tests;

/// <summary>
/// Testler için PDF üretir. Depoya ikili dosya koymak yerine her çalıştırmada
/// yeniden üretmek, fixture'ın içeriğini testin yanında görünür kılar.
/// </summary>
public static class PdfFixtures
{
    /// <summary>Verilen punto boyutlarında, her sayfasında bir dikdörtgen olan PDF.</summary>
    public static string Create(params (double WidthPt, double HeightPt)[] pages)
    {
        var path = Path.Combine(Path.GetTempPath(), $"kolayyazdir-{Guid.NewGuid():N}.pdf");

        using var document = new PdfDocument();
        foreach (var (width, height) in pages)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(width);
            page.Height = XUnit.FromPoint(height);

            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.Black, 10, 10, width - 20, height - 20);
        }

        document.Save(path);
        return path;
    }

    /// <summary>A4 dikey, iki sayfa.</summary>
    public static string TwoPageA4() => Create((595.276, 841.890), (595.276, 841.890));
}
```

- [ ] **Step 3: Başarısız testi yaz**

`tests/KolayYazdir.Documents.Tests/PdfRasterizerTests.cs`:

```csharp
namespace KolayYazdir.Documents.Tests;

public class PdfRasterizerTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    private string Fixture(params (double, double)[] pages)
    {
        var path = PdfFixtures.Create(pages);
        _temporaryFiles.Add(path);
        return path;
    }

    [Fact]
    public void Page_count_matches_the_document()
    {
        using var rasterizer = new PdfRasterizer(Fixture((595.276, 841.890), (595.276, 841.890)));

        Assert.Equal(2, rasterizer.PageCount);
    }

    [Fact]
    public void Page_size_is_reported_in_points()
    {
        using var rasterizer = new PdfRasterizer(Fixture((595.276, 841.890)));

        var size = rasterizer.PageSize(0);

        Assert.Equal(595.276, size.Width, 0);
        Assert.Equal(841.890, size.Height, 0);
    }

    [Fact]
    public void Pages_of_different_sizes_are_reported_separately()
    {
        using var rasterizer = new PdfRasterizer(Fixture((595.276, 841.890), (419.528, 595.276)));

        Assert.Equal(595.276, rasterizer.PageSize(0).Width, 0);
        Assert.Equal(419.528, rasterizer.PageSize(1).Width, 0);
    }

    [Fact]
    public void Render_produces_pixels_matching_the_requested_dpi()
    {
        using var rasterizer = new PdfRasterizer(Fixture((595.276, 841.890)));

        var raster = rasterizer.Render(0, dpi: 150);

        // 595.276 pt / 72 * 150 = 1240 px
        Assert.InRange(raster.WidthPx, 1238, 1242);
        Assert.InRange(raster.HeightPx, 1751, 1755);
    }

    [Fact]
    public void Render_returns_four_bytes_per_pixel()
    {
        using var rasterizer = new PdfRasterizer(Fixture((595.276, 841.890)));

        var raster = rasterizer.Render(0, dpi: 36);

        Assert.Equal(raster.WidthPx * raster.HeightPx * 4, raster.Bgra.Length);
    }

    [Fact]
    public void Render_draws_actual_content()
    {
        using var rasterizer = new PdfRasterizer(Fixture((595.276, 841.890)));

        var raster = rasterizer.Render(0, dpi: 72);

        // Fixture her sayfaya siyah bir dikdörtgen çizer; en az bir koyu piksel olmalı.
        var hasDarkPixel = false;
        for (var i = 0; i < raster.Bgra.Length; i += 4)
        {
            if (raster.Bgra[i] < 64 && raster.Bgra[i + 1] < 64 && raster.Bgra[i + 2] < 64)
            {
                hasDarkPixel = true;
                break;
            }
        }

        Assert.True(hasDarkPixel, "render edilen sayfa tamamen boş çıktı");
    }

    [Fact]
    public void Unreadable_file_throws_a_document_exception()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bozuk-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, "bu bir pdf değil");
        _temporaryFiles.Add(path);

        Assert.Throws<DocumentLoadException>(() => new PdfRasterizer(path));
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
```

- [ ] **Step 4: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests`
Expected: FAIL — `PdfRasterizer` bulunamıyor

- [ ] **Step 5: Arayüzü ve uygulamayı yaz**

`src/KolayYazdir.Documents/IPageRasterizer.cs`:

```csharp
using KolayYazdir.Core.Models;

namespace KolayYazdir.Documents;

/// <summary>
/// Render edilmiş bir sayfa. Piksel düzeni BGRA, satır dolgusu yoktur;
/// hem WPF <c>WriteableBitmap</c>'i hem GDI+ <c>Bitmap</c>'i bu düzeni doğrudan kabul eder.
/// </summary>
public sealed record RasterPage(int WidthPx, int HeightPx, byte[] Bgra);

/// <summary>
/// Bir kaynak belgenin sayfalarını okuyup istenen çözünürlükte piksele çevirir.
/// PDF, görsel ve Office dosyaları bu tek arayüzün arkasında birleşir.
/// </summary>
public interface IPageRasterizer : IDisposable
{
    int PageCount { get; }

    /// <summary>Sayfanın punto cinsinden gerçek boyutu.</summary>
    SizePt PageSize(int index);

    RasterPage Render(int index, double dpi);
}

/// <summary>Bir dosya açılamadığında veya okunamadığında atılır.</summary>
public sealed class DocumentLoadException(string message, Exception? inner = null)
    : Exception(message, inner);
```

`src/KolayYazdir.Documents/PdfRasterizer.cs`:

```csharp
using KolayYazdir.Core.Models;
using SkiaSharp;

namespace KolayYazdir.Documents;

/// <summary>
/// PDFium (PDFtoImage paketi üzerinden) ile PDF okur. Bu sınıf projedeki tek
/// PDFtoImage kullanıcısıdır; paketin API'si değişirse etkisi buraya hapsolur.
/// </summary>
public sealed class PdfRasterizer : IPageRasterizer
{
    private readonly byte[] _bytes;
    private readonly IReadOnlyList<SizePt> _pageSizes;

    public PdfRasterizer(string path)
    {
        try
        {
            _bytes = File.ReadAllBytes(path);
            _pageSizes = PDFtoImage.Conversion
                .GetPageSizes(_bytes)
                .Select(s => new SizePt(s.Width, s.Height))
                .ToList();
        }
        catch (Exception ex) when (ex is not DocumentLoadException)
        {
            throw new DocumentLoadException($"PDF açılamadı: {Path.GetFileName(path)}", ex);
        }

        if (_pageSizes.Count == 0)
            throw new DocumentLoadException($"PDF hiç sayfa içermiyor: {Path.GetFileName(path)}");
    }

    public int PageCount => _pageSizes.Count;

    public SizePt PageSize(int index) => _pageSizes[index];

    public RasterPage Render(int index, double dpi)
    {
        using var bitmap = PDFtoImage.Conversion.ToImage(
            _bytes,
            page: index,
            options: new PDFtoImage.RenderOptions(Dpi: (int)Math.Round(dpi), WithAnnotations: true));

        return ToRasterPage(bitmap);
    }

    /// <summary>Skia'nın verdiği bitmap'i dolgusuz BGRA dizisine çevirir.</summary>
    private static RasterPage ToRasterPage(SKBitmap bitmap)
    {
        var target = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var buffer = new byte[target.Width * target.Height * 4];

        using (var pinned = new SKBitmap())
        {
            if (!bitmap.CopyTo(pinned, SKColorType.Bgra8888))
                throw new DocumentLoadException("Sayfa BGRA biçimine çevrilemedi.");

            pinned.Bytes.CopyTo(buffer, 0);
        }

        return new RasterPage(target.Width, target.Height, buffer);
    }

    public void Dispose() { }
}
```

- [ ] **Step 6: Testleri çalıştır ve API'yi doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests`
Expected: PASS

**Derleme hatası alırsan** (`GetPageSizes`, `ToImage` veya `RenderOptions` bulunamıyor):
PDFtoImage'ın 5.x API'si beklenenden farklı demektir. Şu sırayı izle:
1. `dotnet build src/KolayYazdir.Documents` çıktısındaki hatayı oku.
2. Paketin gerçek yüzeyini gör: `ls ~/.nuget/packages/pdftoimage/5.4.0/lib/` altındaki hedefe bak; gerekirse `https://github.com/sungaila/PDFtoImage` README'sini aç.
3. Sadece `PdfRasterizer.cs` içindeki üç çağrıyı düzelt. **Testleri değiştirme** — testler bizim arayüzümüzü tanımlıyor, paketi değil.

- [ ] **Step 7: Commit**

```bash
git add src/KolayYazdir.Documents tests/KolayYazdir.Documents.Tests KolayYazdir.sln
git commit -m "PDFium tabanlı PDF rasterleştirici"
```

---

### Task 9: Görsel rasterleştirici

Tek sayfalık bir belge gibi davranır. Kritik nokta: görselin gerçek boyutu DPI meta verisinden gelir; meta veri yoksa Windows'un davranışı olan 96 DPI varsayılır.

**Files:**
- Create: `src/KolayYazdir.Documents/ImageRasterizer.cs`
- Create: `tests/KolayYazdir.Documents.Tests/ImageFixtures.cs`
- Test: `tests/KolayYazdir.Documents.Tests/ImageRasterizerTests.cs`

**Interfaces:**
- Produces: `sealed class ImageRasterizer : IPageRasterizer { ImageRasterizer(string path); }` — `PageCount` her zaman 1.

- [ ] **Step 1: Fixture üreticisini yaz**

`tests/KolayYazdir.Documents.Tests/ImageFixtures.cs`:

```csharp
using System.Drawing;
using System.Drawing.Imaging;

namespace KolayYazdir.Documents.Tests;

public static class ImageFixtures
{
    /// <summary>Verilen piksel boyutunda ve çözünürlükte kırmızı bir görsel yazar.</summary>
    public static string Create(int widthPx, int heightPx, float dpi, ImageFormat format)
    {
        var extension = Equals(format, ImageFormat.Png) ? "png" : "jpg";
        var path = Path.Combine(Path.GetTempPath(), $"kolayyazdir-{Guid.NewGuid():N}.{extension}");

        using var bitmap = new Bitmap(widthPx, heightPx);
        bitmap.SetResolution(dpi, dpi);
        using (var gfx = Graphics.FromImage(bitmap))
        {
            gfx.Clear(Color.Red);
        }

        bitmap.Save(path, format);
        return path;
    }
}
```

Test projesine `System.Drawing.Common` ekle:

```bash
dotnet add tests/KolayYazdir.Documents.Tests package System.Drawing.Common --version 8.0.10
dotnet add src/KolayYazdir.Documents package System.Drawing.Common --version 8.0.10
```

- [ ] **Step 2: Başarısız testi yaz**

`tests/KolayYazdir.Documents.Tests/ImageRasterizerTests.cs`:

```csharp
using System.Drawing.Imaging;

namespace KolayYazdir.Documents.Tests;

public class ImageRasterizerTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    private string Fixture(int widthPx, int heightPx, float dpi, ImageFormat? format = null)
    {
        var path = ImageFixtures.Create(widthPx, heightPx, dpi, format ?? ImageFormat.Png);
        _temporaryFiles.Add(path);
        return path;
    }

    [Fact]
    public void An_image_is_a_single_page_document()
    {
        using var rasterizer = new ImageRasterizer(Fixture(600, 400, 96));

        Assert.Equal(1, rasterizer.PageCount);
    }

    [Fact]
    public void Ninety_six_dpi_maps_pixels_to_three_quarters_of_a_point()
    {
        using var rasterizer = new ImageRasterizer(Fixture(600, 400, 96));

        var size = rasterizer.PageSize(0);

        Assert.Equal(450, size.Width, 1);   // 600 / 96 * 72
        Assert.Equal(300, size.Height, 1);  // 400 / 96 * 72
    }

    [Fact]
    public void Three_hundred_dpi_photo_reports_its_real_physical_size()
    {
        using var rasterizer = new ImageRasterizer(Fixture(1200, 1800, 300, ImageFormat.Jpeg));

        var size = rasterizer.PageSize(0);

        Assert.Equal(288, size.Width, 1);   // 1200 / 300 * 72 = 4 inç
        Assert.Equal(432, size.Height, 1);  // 1800 / 300 * 72 = 6 inç
    }

    [Fact]
    public void Render_returns_the_original_pixels_regardless_of_dpi()
    {
        using var rasterizer = new ImageRasterizer(Fixture(600, 400, 96));

        var raster = rasterizer.Render(0, dpi: 300);

        Assert.Equal(600, raster.WidthPx);
        Assert.Equal(400, raster.HeightPx);
        Assert.Equal(600 * 400 * 4, raster.Bgra.Length);
    }

    [Fact]
    public void Rendered_pixels_carry_the_source_colour()
    {
        using var rasterizer = new ImageRasterizer(Fixture(10, 10, 96));

        var raster = rasterizer.Render(0, dpi: 96);

        // BGRA düzeninde kırmızı: B=0, G=0, R=255
        Assert.Equal(0, raster.Bgra[0]);
        Assert.Equal(0, raster.Bgra[1]);
        Assert.Equal(255, raster.Bgra[2]);
    }

    [Fact]
    public void Unreadable_file_throws_a_document_exception()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bozuk-{Guid.NewGuid():N}.png");
        File.WriteAllText(path, "bu bir görsel değil");
        _temporaryFiles.Add(path);

        Assert.Throws<DocumentLoadException>(() => new ImageRasterizer(path));
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
```

- [ ] **Step 3: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests --filter ImageRasterizerTests`
Expected: FAIL — `ImageRasterizer` bulunamıyor

- [ ] **Step 4: Uygulamayı yaz**

`src/KolayYazdir.Documents/ImageRasterizer.cs`:

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Documents;

/// <summary>
/// Bir görsel dosyasını tek sayfalık belge gibi sunar. Render her zaman
/// görselin kendi piksellerini döner — büyütme kararı yerleşim motorunun ve
/// çizim katmanının işidir, burada yeniden örnekleme yapılmaz.
/// </summary>
public sealed class ImageRasterizer : IPageRasterizer
{
    private const float FallbackDpi = 96f;

    private readonly Bitmap _bitmap;
    private readonly SizePt _size;

    public ImageRasterizer(string path)
    {
        try
        {
            // Dosya kilidini tutmamak için akıştan yükleyip kopyalıyoruz;
            // kullanıcı dosyayı silmek isterse uygulama engel olmasın.
            using var stream = File.OpenRead(path);
            using var loaded = new Bitmap(stream);
            _bitmap = new Bitmap(loaded);
        }
        catch (Exception ex)
        {
            throw new DocumentLoadException($"Görsel açılamadı: {Path.GetFileName(path)}", ex);
        }

        // GDI+ çözünürlük bilgisi olmayan dosyalarda ekran DPI'sını uydurur;
        // makul olmayan değerlerde Windows'un varsayımına düşüyoruz.
        var horizontal = _bitmap.HorizontalResolution is > 1f and < 5000f ? _bitmap.HorizontalResolution : FallbackDpi;
        var vertical = _bitmap.VerticalResolution is > 1f and < 5000f ? _bitmap.VerticalResolution : FallbackDpi;

        _size = new SizePt(_bitmap.Width / horizontal * 72.0, _bitmap.Height / vertical * 72.0);
    }

    public int PageCount => 1;

    public SizePt PageSize(int index) => _size;

    public RasterPage Render(int index, double dpi)
    {
        var rectangle = new Rectangle(0, 0, _bitmap.Width, _bitmap.Height);
        var data = _bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            var buffer = new byte[_bitmap.Width * _bitmap.Height * 4];
            for (var row = 0; row < _bitmap.Height; row++)
            {
                Marshal.Copy(data.Scan0 + row * data.Stride, buffer, row * _bitmap.Width * 4, _bitmap.Width * 4);
            }

            return new RasterPage(_bitmap.Width, _bitmap.Height, buffer);
        }
        finally
        {
            _bitmap.UnlockBits(data);
        }
    }

    public void Dispose() => _bitmap.Dispose();
}
```

- [ ] **Step 5: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/KolayYazdir.Documents/ImageRasterizer.cs tests/KolayYazdir.Documents.Tests
git commit -m "Görsel rasterleştirici ve gerçek boyut hesabı"
```

---

### Task 10: Office dönüştürme zinciri

Word/Excel dosyalarını PDF'e çevirir. Önce Office COM, olmazsa LibreOffice. Dönüşüm sonuçları önbelleklenir.

**Geliştirme makinesi notu:** Bu makinede Microsoft Office kurulu, LibreOffice kurulu **değil**. Dükkandaki makinelerde LibreOffice her yerde var, Office kimilerinde. Bu yüzden LibreOffice testi ortam yoksa atlanacak şekilde yazılır; her iki yol da CI'da değil, yerinde doğrulanır.

**Files:**
- Create: `src/KolayYazdir.Documents/Office/IOfficeConverter.cs`
- Create: `src/KolayYazdir.Documents/Office/LibreOfficeConverter.cs`
- Create: `src/KolayYazdir.Documents/Office/OfficeComConverter.cs`
- Create: `src/KolayYazdir.Documents/Office/OfficeConverterChain.cs`
- Create: `src/KolayYazdir.Documents/Office/ConversionCache.cs`
- Create: `tests/KolayYazdir.Documents.Tests/OfficeFixtures.cs`
- Test: `tests/KolayYazdir.Documents.Tests/OfficeConverterTests.cs`
- Test: `tests/KolayYazdir.Documents.Tests/ConversionCacheTests.cs`

**Interfaces:**
- Produces:
  - `interface IOfficeConverter { string Name { get; } bool IsAvailable { get; } Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct); }`
  - `sealed class OfficeConverterChain : IOfficeConverter` — sırayla dener
  - `sealed class ConversionCache { string? Lookup(string sourcePath); string Store(string sourcePath, string pdfPath); }`
  - `sealed class OfficeConversionException(string message, Exception? inner = null) : Exception`

- [ ] **Step 1: Önbellek testini yaz**

`tests/KolayYazdir.Documents.Tests/ConversionCacheTests.cs`:

```csharp
using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents.Tests;

public class ConversionCacheTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kolayyazdir-cache").FullName;

    private string WriteFile(string content)
    {
        var path = Path.Combine(_root, $"{Guid.NewGuid():N}.docx");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Unknown_source_is_a_miss()
    {
        var cache = new ConversionCache(_root);

        Assert.Null(cache.Lookup(WriteFile("merhaba")));
    }

    [Fact]
    public void Stored_conversion_is_found_again()
    {
        var cache = new ConversionCache(_root);
        var source = WriteFile("merhaba");
        var pdf = Path.Combine(_root, "cikti.pdf");
        File.WriteAllText(pdf, "%PDF-1.4");

        var stored = cache.Store(source, pdf);

        Assert.Equal(stored, cache.Lookup(source));
        Assert.True(File.Exists(stored));
    }

    [Fact]
    public void Editing_the_source_invalidates_the_entry()
    {
        var cache = new ConversionCache(_root);
        var source = WriteFile("merhaba");
        var pdf = Path.Combine(_root, "cikti.pdf");
        File.WriteAllText(pdf, "%PDF-1.4");
        cache.Store(source, pdf);

        File.WriteAllText(source, "değişti");
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddSeconds(5));

        Assert.Null(cache.Lookup(source));
    }

    [Fact]
    public void A_deleted_cache_file_is_reported_as_a_miss()
    {
        var cache = new ConversionCache(_root);
        var source = WriteFile("merhaba");
        var pdf = Path.Combine(_root, "cikti.pdf");
        File.WriteAllText(pdf, "%PDF-1.4");
        var stored = cache.Store(source, pdf);

        File.Delete(stored);

        Assert.Null(cache.Lookup(source));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests --filter ConversionCacheTests`
Expected: FAIL — `ConversionCache` bulunamıyor

- [ ] **Step 3: Önbelleği yaz**

`src/KolayYazdir.Documents/Office/ConversionCache.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace KolayYazdir.Documents.Office;

/// <summary>
/// Çevrilmiş PDF'leri dosya yolu + değişiklik tarihi + boyut anahtarıyla saklar.
/// Aynı Word dosyası ikinci kez seçildiğinde dönüşüm beklenmez.
/// </summary>
public sealed class ConversionCache(string rootDirectory)
{
    public static ConversionCache Default => new(Path.Combine(Path.GetTempPath(), "KolayYazdir", "donusum"));

    /// <returns>Önbellekteki PDF'in yolu, yoksa null.</returns>
    public string? Lookup(string sourcePath)
    {
        var path = PathFor(sourcePath);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Üretilen PDF'i önbelleğe taşır ve yeni yolunu döner.</summary>
    public string Store(string sourcePath, string pdfPath)
    {
        var target = PathFor(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        if (File.Exists(target)) File.Delete(target);
        File.Move(pdfPath, target);

        return target;
    }

    private string PathFor(string sourcePath)
    {
        var info = new FileInfo(sourcePath);
        var key = $"{sourcePath.ToLowerInvariant()}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..24];

        return Path.Combine(rootDirectory, $"{hash}.pdf");
    }
}
```

- [ ] **Step 4: Office fixture üreticisini yaz**

`tests/KolayYazdir.Documents.Tests/OfficeFixtures.cs`:

```csharp
using System.IO.Compression;
using System.Text;

namespace KolayYazdir.Documents.Tests;

/// <summary>
/// Asgari ama geçerli bir .docx üretir. Bir .docx aslında birkaç XML parçası
/// içeren bir zip'tir; depoya ikili fixture koymamak için elle kuruyoruz.
/// </summary>
public static class OfficeFixtures
{
    public static string CreateDocx(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), $"kolayyazdir-{Guid.NewGuid():N}.docx");

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        Write(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
            </Types>
            """);

        Write(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
            </Relationships>
            """);

        Write(archive, "word/document.xml", $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:body>
            </w:document>
            """);

        return path;
    }

    private static void Write(ZipArchive archive, string entryName, string content)
    {
        using var stream = archive.CreateEntry(entryName).Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
```

- [ ] **Step 5: Dönüştürücü testini yaz**

`tests/KolayYazdir.Documents.Tests/OfficeConverterTests.cs`:

```csharp
using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents.Tests;

public class OfficeConverterTests : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("kolayyazdir-office").FullName;
    private readonly List<string> _temporaryFiles = [];

    private string Docx(string text = "Merhaba kırtasiye")
    {
        var path = OfficeFixtures.CreateDocx(text);
        _temporaryFiles.Add(path);
        return path;
    }

    [Fact]
    public void Chain_reports_available_when_any_link_is_available()
    {
        var chain = new OfficeConverterChain([
            new StubConverter("yok", available: false),
            new StubConverter("var", available: true)
        ]);

        Assert.True(chain.IsAvailable);
    }

    [Fact]
    public void Chain_reports_unavailable_when_every_link_is_missing()
    {
        var chain = new OfficeConverterChain([new StubConverter("yok", available: false)]);

        Assert.False(chain.IsAvailable);
    }

    [Fact]
    public async Task Chain_falls_through_to_the_next_converter_on_failure()
    {
        var second = new StubConverter("ikinci", available: true);
        var chain = new OfficeConverterChain([
            new StubConverter("birinci", available: true, throws: true),
            second
        ]);

        await chain.ToPdfAsync(Docx(), _output, CancellationToken.None);

        Assert.True(second.WasCalled);
    }

    [Fact]
    public async Task Chain_with_no_available_converter_names_libre_office()
    {
        var chain = new OfficeConverterChain([new StubConverter("yok", available: false)]);

        var error = await Assert.ThrowsAsync<OfficeConversionException>(
            () => chain.ToPdfAsync(Docx(), _output, CancellationToken.None));

        Assert.Contains("LibreOffice", error.Message);
    }

    [SkippableFact]
    public async Task LibreOffice_converts_a_docx_to_pdf()
    {
        var converter = new LibreOfficeConverter();
        Skip.IfNot(converter.IsAvailable, "Bu makinede LibreOffice kurulu değil.");

        var pdf = await converter.ToPdfAsync(Docx(), _output, CancellationToken.None);

        Assert.True(File.Exists(pdf));
        Assert.Equal(".pdf", Path.GetExtension(pdf));
        using var rasterizer = new PdfRasterizer(pdf);
        Assert.True(rasterizer.PageCount >= 1);
    }

    [SkippableFact]
    public async Task Office_com_converts_a_docx_to_pdf()
    {
        var converter = new OfficeComConverter();
        Skip.IfNot(converter.IsAvailable, "Bu makinede Microsoft Word kurulu değil.");

        var pdf = await converter.ToPdfAsync(Docx(), _output, CancellationToken.None);

        Assert.True(File.Exists(pdf));
        using var rasterizer = new PdfRasterizer(pdf);
        Assert.True(rasterizer.PageCount >= 1);
    }

    private sealed class StubConverter(string name, bool available, bool throws = false) : IOfficeConverter
    {
        public bool WasCalled { get; private set; }
        public string Name => name;
        public bool IsAvailable => available;

        public Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
        {
            WasCalled = true;
            if (throws) throw new OfficeConversionException($"{name} başarısız");

            var path = Path.Combine(targetDirectory, $"{Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, "%PDF-1.4");
            return Task.FromResult(path);
        }
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
        try { Directory.Delete(_output, recursive: true); } catch (IOException) { }
    }
}
```

`SkippableFact` için paket ekle:

```bash
dotnet add tests/KolayYazdir.Documents.Tests package Xunit.SkippableFact --version 1.4.13
```

- [ ] **Step 6: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests --filter OfficeConverterTests`
Expected: FAIL — `IOfficeConverter` bulunamıyor

- [ ] **Step 7: Arayüzü ve LibreOffice dönüştürücüsünü yaz**

`src/KolayYazdir.Documents/Office/IOfficeConverter.cs`:

```csharp
namespace KolayYazdir.Documents.Office;

/// <summary>Word/Excel dosyasını PDF'e çeviren bir yol.</summary>
public interface IOfficeConverter
{
    /// <summary>Hata mesajlarında görünecek insan okunur ad.</summary>
    string Name { get; }

    /// <summary>Bu makinede kullanılabilir mi.</summary>
    bool IsAvailable { get; }

    /// <returns>Üretilen PDF'in tam yolu.</returns>
    Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct);
}

public sealed class OfficeConversionException(string message, Exception? inner = null)
    : Exception(message, inner);
```

`src/KolayYazdir.Documents/Office/LibreOfficeConverter.cs`:

```csharp
using System.Diagnostics;
using Microsoft.Win32;

namespace KolayYazdir.Documents.Office;

/// <summary>
/// LibreOffice'i başsız kipte çalıştırır. Dükkandaki her makinede kurulu
/// olduğu için bu, garantili yedek yoldur.
/// </summary>
public sealed class LibreOfficeConverter : IOfficeConverter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private readonly string? _executable = Locate();

    public string Name => "LibreOffice";

    public bool IsAvailable => _executable is not null;

    public async Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
    {
        if (_executable is null)
            throw new OfficeConversionException("LibreOffice bu bilgisayarda bulunamadı.");

        Directory.CreateDirectory(targetDirectory);

        // Kendi kullanıcı profilimizi veriyoruz: kullanıcının açık LibreOffice
        // penceresi varsa başsız süreç ona takılmasın.
        var profile = Path.Combine(Path.GetTempPath(), "KolayYazdir", "lo-profile");
        Directory.CreateDirectory(profile);

        var startInfo = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add($"-env:UserInstallation=file:///{profile.Replace('\\', '/')}");
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--norestore");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add("pdf");
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(targetDirectory);
        startInfo.ArgumentList.Add(sourcePath);

        using var process = Process.Start(startInfo)
            ?? throw new OfficeConversionException("LibreOffice başlatılamadı.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new OfficeConversionException("LibreOffice dönüşümü zaman aşımına uğradı.");
        }

        var expected = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");
        if (!File.Exists(expected))
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            throw new OfficeConversionException(
                $"LibreOffice dosyayı çeviremedi: {Path.GetFileName(sourcePath)}. {error}".Trim());
        }

        return expected;
    }

    /// <summary>Kayıt defterinden, sonra bilinen kurulum yollarından arar.</summary>
    private static string? Locate()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = root.OpenSubKey(@"SOFTWARE\LibreOffice\UNO\InstallPath");
                if (key?.GetValue(null) is string installPath)
                {
                    var candidate = Path.Combine(installPath, "soffice.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Kayıt defteri okunamadıysa dosya sistemine düş.
            }
        }

        string[] fallbacks =
        [
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        ];

        return fallbacks.FirstOrDefault(File.Exists);
    }
}
```

`Microsoft.Win32.Registry` .NET 8'de `net8.0-windows` hedefinde yerleşiktir, ek paket gerekmez.

- [ ] **Step 8: Office COM dönüştürücüsünü yaz**

`src/KolayYazdir.Documents/Office/OfficeComConverter.cs`:

```csharp
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KolayYazdir.Documents.Office;

/// <summary>
/// Kurulu Microsoft Office'i geç bağlama (late binding) ile kullanır. Geç
/// bağlama sayesinde Office sürümüne özel bir birlikte çalışma derlemesine
/// bağımlı olmayız; dükkandaki makinelerde sürümler farklı.
/// </summary>
public sealed class OfficeComConverter : IOfficeConverter
{
    private const int WdFormatPdf = 17;
    private const int XlTypePdf = 0;

    public string Name => "Microsoft Office";

    public bool IsAvailable => IsRegistered("Word.Application") || IsRegistered("Excel.Application");

    public Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(targetDirectory);
        var target = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");

        // COM otomasyonu tek iş parçacıklı apartman gerektirir ve engelleyicidir;
        // arayüzü kilitlememek için ayrı bir STA iş parçacığında koşturuyoruz.
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                if (IsSpreadsheet(sourcePath)) ConvertWithExcel(sourcePath, target);
                else ConvertWithWord(sourcePath, target);

                completion.SetResult(target);
            }
            catch (Exception ex)
            {
                completion.SetException(new OfficeConversionException(
                    $"Office dosyayı çeviremedi: {Path.GetFileName(sourcePath)}", ex));
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return completion.Task.WaitAsync(ct);
    }

    private static bool IsSpreadsheet(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".xls" or ".xlsx" or ".xlsm" or ".csv";

    private static void ConvertWithWord(string sourcePath, string target)
    {
        dynamic? application = null;
        dynamic? document = null;
        try
        {
            application = CreateInstance("Word.Application");
            application.Visible = false;
            application.DisplayAlerts = 0;

            document = application.Documents.Open(sourcePath, ReadOnly: true, AddToRecentFiles: false);
            document.SaveAs2(target, WdFormatPdf);
        }
        finally
        {
            if (document is not null) { document.Close(0); Marshal.FinalReleaseComObject(document); }
            if (application is not null) { application.Quit(0); Marshal.FinalReleaseComObject(application); }
        }
    }

    private static void ConvertWithExcel(string sourcePath, string target)
    {
        dynamic? application = null;
        dynamic? workbook = null;
        try
        {
            application = CreateInstance("Excel.Application");
            application.Visible = false;
            application.DisplayAlerts = false;

            workbook = application.Workbooks.Open(sourcePath, ReadOnly: true, AddToMru: false);
            workbook.ExportAsFixedFormat(XlTypePdf, target);
        }
        finally
        {
            if (workbook is not null) { workbook.Close(false); Marshal.FinalReleaseComObject(workbook); }
            if (application is not null) { application.Quit(); Marshal.FinalReleaseComObject(application); }
        }
    }

    private static dynamic CreateInstance(string progId)
    {
        var type = Type.GetTypeFromProgID(progId)
            ?? throw new OfficeConversionException($"{progId} bu bilgisayarda kayıtlı değil.");

        return Activator.CreateInstance(type)
            ?? throw new OfficeConversionException($"{progId} başlatılamadı.");
    }

    private static bool IsRegistered(string progId)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(progId);
            return key is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
```

`src/KolayYazdir.Documents/KolayYazdir.Documents.csproj` içine ekle (geç bağlama `dynamic` için gerekir):

```xml
<PropertyGroup>
  <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
</PropertyGroup>
<ItemGroup>
  <Reference Include="Microsoft.CSharp" />
</ItemGroup>
```

> `Microsoft.CSharp` .NET 8'de zaten örtük olarak gelir; yukarıdaki `Reference`
> gerekmiyorsa derleme uyarısı verir, o durumda satırı kaldır.

- [ ] **Step 9: Zinciri yaz**

`src/KolayYazdir.Documents/Office/OfficeConverterChain.cs`:

```csharp
namespace KolayYazdir.Documents.Office;

/// <summary>
/// Dönüştürücüleri sırayla dener. Varsayılan sıra Office → LibreOffice'tir:
/// Office kuruluysa biçim sadakati daha yüksektir, ama LibreOffice her makinede
/// bulunduğu için güvenilir yedektir.
/// </summary>
public sealed class OfficeConverterChain(IReadOnlyList<IOfficeConverter> converters) : IOfficeConverter
{
    public static OfficeConverterChain Default =>
        new([new OfficeComConverter(), new LibreOfficeConverter()]);

    public string Name => "Office dönüştürme zinciri";

    public bool IsAvailable => converters.Any(c => c.IsAvailable);

    public async Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
    {
        var failures = new List<string>();

        foreach (var converter in converters.Where(c => c.IsAvailable))
        {
            try
            {
                return await converter.ToPdfAsync(sourcePath, targetDirectory, ct);
            }
            catch (OfficeConversionException ex)
            {
                failures.Add($"{converter.Name}: {ex.Message}");
            }
        }

        if (failures.Count == 0)
        {
            throw new OfficeConversionException(
                "Word ve Excel dosyalarını yazdırmak için LibreOffice veya Microsoft Office gerekiyor. " +
                "Bu bilgisayarda ikisi de bulunamadı.");
        }

        throw new OfficeConversionException(
            $"Dosya çevrilemedi: {Path.GetFileName(sourcePath)}. " + string.Join(" · ", failures));
    }
}
```

- [ ] **Step 10: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests`
Expected: PASS — LibreOffice testi bu makinede `Skipped`, Office COM testi `Passed`

- [ ] **Step 11: Commit**

```bash
git add src/KolayYazdir.Documents/Office tests/KolayYazdir.Documents.Tests
git commit -m "Office ve LibreOffice dönüştürme zinciri"
```

---

### Task 11: DocumentLoader

Dosya uzantısına bakıp doğru rasterleştiriciyi seçer, Office dosyalarını önce PDF'e çevirir. Uygulamanın geri kalanı dosya türlerini bilmez.

**Files:**
- Create: `src/KolayYazdir.Documents/SourceDocument.cs`
- Create: `src/KolayYazdir.Documents/DocumentLoader.cs`
- Test: `tests/KolayYazdir.Documents.Tests/DocumentLoaderTests.cs`

**Interfaces:**
- Produces:
  - `sealed class SourceDocument : IDisposable { string Path; string FileName; int PageCount; SizePt PageSize(int); RasterPage Render(int, double); }`
  - `sealed class DocumentLoader { DocumentLoader(IOfficeConverter, ConversionCache); Task<SourceDocument> LoadAsync(string path, CancellationToken ct); static bool IsSupported(string path); static string FileDialogFilter { get; } }`

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.Documents.Tests/DocumentLoaderTests.cs`:

```csharp
using System.Drawing.Imaging;
using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents.Tests;

public class DocumentLoaderTests : IDisposable
{
    private readonly string _cacheRoot = Directory.CreateTempSubdirectory("kolayyazdir-loader").FullName;
    private readonly List<string> _temporaryFiles = [];

    private DocumentLoader Loader(IOfficeConverter? converter = null) =>
        new(converter ?? OfficeConverterChain.Default, new ConversionCache(_cacheRoot));

    private string Track(string path)
    {
        _temporaryFiles.Add(path);
        return path;
    }

    [Theory]
    [InlineData("a.pdf")]
    [InlineData("a.PDF")]
    [InlineData("a.jpg")]
    [InlineData("a.jpeg")]
    [InlineData("a.png")]
    [InlineData("a.bmp")]
    [InlineData("a.gif")]
    [InlineData("a.tif")]
    [InlineData("a.tiff")]
    [InlineData("a.webp")]
    [InlineData("a.docx")]
    [InlineData("a.doc")]
    [InlineData("a.xlsx")]
    [InlineData("a.xls")]
    public void Supported_extensions_are_recognised(string name)
    {
        Assert.True(DocumentLoader.IsSupported(name));
    }

    [Theory]
    [InlineData("a.txt")]
    [InlineData("a.exe")]
    [InlineData("a")]
    public void Other_extensions_are_rejected(string name)
    {
        Assert.False(DocumentLoader.IsSupported(name));
    }

    [Fact]
    public async Task A_pdf_loads_with_its_real_page_count()
    {
        var path = Track(PdfFixtures.Create((595.276, 841.890), (595.276, 841.890)));

        using var document = await Loader().LoadAsync(path, CancellationToken.None);

        Assert.Equal(2, document.PageCount);
        Assert.Equal(Path.GetFileName(path), document.FileName);
    }

    [Fact]
    public async Task An_image_loads_as_a_single_page()
    {
        var path = Track(ImageFixtures.Create(600, 400, 96, ImageFormat.Png));

        using var document = await Loader().LoadAsync(path, CancellationToken.None);

        Assert.Equal(1, document.PageCount);
        Assert.Equal(450, document.PageSize(0).Width, 1);
    }

    [Fact]
    public async Task An_office_file_is_converted_before_loading()
    {
        var docx = Track(OfficeFixtures.CreateDocx("Merhaba"));
        var converter = new RecordingConverter();

        using var document = await Loader(converter).LoadAsync(docx, CancellationToken.None);

        Assert.True(converter.WasCalled);
        Assert.True(document.PageCount >= 1);
    }

    [Fact]
    public async Task A_repeated_office_file_hits_the_cache()
    {
        var docx = Track(OfficeFixtures.CreateDocx("Merhaba"));
        var converter = new RecordingConverter();
        var loader = Loader(converter);

        (await loader.LoadAsync(docx, CancellationToken.None)).Dispose();
        converter.Reset();
        (await loader.LoadAsync(docx, CancellationToken.None)).Dispose();

        Assert.False(converter.WasCalled);
    }

    [Fact]
    public async Task An_unsupported_extension_is_rejected_with_a_clear_message()
    {
        var path = Track(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt"));
        await File.WriteAllTextAsync(path, "merhaba");

        var error = await Assert.ThrowsAsync<DocumentLoadException>(
            () => Loader().LoadAsync(path, CancellationToken.None));

        Assert.Contains(".txt", error.Message);
    }

    [Fact]
    public void The_file_dialog_filter_covers_every_supported_type()
    {
        var filter = DocumentLoader.FileDialogFilter;

        Assert.Contains("*.pdf", filter);
        Assert.Contains("*.jpg", filter);
        Assert.Contains("*.docx", filter);
        Assert.Contains("*.xlsx", filter);
    }

    /// <summary>Gerçek bir PDF üreten, çağrıldığını kaydeden sahte dönüştürücü.</summary>
    private sealed class RecordingConverter : IOfficeConverter
    {
        public bool WasCalled { get; private set; }
        public void Reset() => WasCalled = false;

        public string Name => "sahte";
        public bool IsAvailable => true;

        public Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
        {
            WasCalled = true;
            Directory.CreateDirectory(targetDirectory);

            var produced = PdfFixtures.Create((595.276, 841.890));
            var target = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");
            File.Move(produced, target, overwrite: true);

            return Task.FromResult(target);
        }
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
        try { Directory.Delete(_cacheRoot, recursive: true); } catch (IOException) { }
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests --filter DocumentLoaderTests`
Expected: FAIL — `DocumentLoader` bulunamıyor

- [ ] **Step 3: SourceDocument'ı yaz**

`src/KolayYazdir.Documents/SourceDocument.cs`:

```csharp
using KolayYazdir.Core.Models;

namespace KolayYazdir.Documents;

/// <summary>
/// Kullanıcının seçtiği tek bir dosya. Türü ne olursa olsun (görsel, PDF,
/// Word, Excel) dışarıya aynı yüzü gösterir.
/// </summary>
public sealed class SourceDocument(string path, IPageRasterizer rasterizer) : IDisposable
{
    public string Path { get; } = path;

    public string FileName { get; } = System.IO.Path.GetFileName(path);

    public int PageCount => rasterizer.PageCount;

    public SizePt PageSize(int index) => rasterizer.PageSize(index);

    public RasterPage Render(int index, double dpi) => rasterizer.Render(index, dpi);

    public void Dispose() => rasterizer.Dispose();
}
```

- [ ] **Step 4: DocumentLoader'ı yaz**

`src/KolayYazdir.Documents/DocumentLoader.cs`:

```csharp
using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents;

/// <summary>
/// Dosya yolunu açılmış bir belgeye çevirir. Uzantı eşlemesi ve Office
/// dönüşümü burada saklanır; uygulamanın geri kalanı dosya türü bilmez.
/// </summary>
public sealed class DocumentLoader(IOfficeConverter converter, ConversionCache cache)
{
    private static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"];

    private static readonly string[] OfficeExtensions =
        [".doc", ".docx", ".docm", ".rtf", ".odt", ".xls", ".xlsx", ".xlsm", ".ods", ".csv"];

    public static DocumentLoader Default => new(OfficeConverterChain.Default, ConversionCache.Default);

    public static bool IsSupported(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension == ".pdf" || ImageExtensions.Contains(extension) || OfficeExtensions.Contains(extension);
    }

    /// <summary>Dosya seçme penceresinin süzgeç metni.</summary>
    public static string FileDialogFilter =>
        "Yazdırılabilir dosyalar|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;" +
        "*.doc;*.docx;*.docm;*.rtf;*.odt;*.xls;*.xlsx;*.xlsm;*.ods;*.csv" +
        "|PDF|*.pdf" +
        "|Görseller|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp" +
        "|Word ve Excel|*.doc;*.docx;*.docm;*.rtf;*.odt;*.xls;*.xlsx;*.xlsm;*.ods;*.csv" +
        "|Tüm dosyalar|*.*";

    public async Task<SourceDocument> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new DocumentLoadException($"Dosya bulunamadı: {Path.GetFileName(path)}");

        var extension = Path.GetExtension(path).ToLowerInvariant();

        if (extension == ".pdf")
            return new SourceDocument(path, new PdfRasterizer(path));

        if (ImageExtensions.Contains(extension))
            return new SourceDocument(path, new ImageRasterizer(path));

        if (OfficeExtensions.Contains(extension))
            return new SourceDocument(path, new PdfRasterizer(await ConvertAsync(path, ct)));

        throw new DocumentLoadException($"Bu dosya türü yazdırılamıyor: {extension}");
    }

    /// <summary>Office dosyasını PDF'e çevirir; aynı dosya daha önce çevrildiyse onu kullanır.</summary>
    private async Task<string> ConvertAsync(string path, CancellationToken ct)
    {
        if (cache.Lookup(path) is { } cached) return cached;

        var workspace = Path.Combine(Path.GetTempPath(), "KolayYazdir", "calisma", Guid.NewGuid().ToString("N"));
        try
        {
            var produced = await converter.ToPdfAsync(path, workspace, ct);
            return cache.Store(path, produced);
        }
        catch (OfficeConversionException ex)
        {
            throw new DocumentLoadException(ex.Message, ex);
        }
        finally
        {
            try { Directory.Delete(workspace, recursive: true); } catch (IOException) { } catch (DirectoryNotFoundException) { }
        }
    }
}
```

- [ ] **Step 5: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/KolayYazdir.Documents tests/KolayYazdir.Documents.Tests
git commit -m "Dosya türlerini birleştiren DocumentLoader"
```

---

### Task 12: DocumentSet — dosyaları tek sayfa dizisine birleştirme

Yerleşim motoru "5. sayfa" der; birinin bu numarayı "ikinci dosyanın üçüncü sayfası"na çevirmesi gerekir. `DocumentSet` bu eşlemeyi tutar.

**Files:**
- Create: `src/KolayYazdir.Documents/DocumentSet.cs`
- Test: `tests/KolayYazdir.Documents.Tests/DocumentSetTests.cs`

**Interfaces:**
- Consumes: `SourceDocument` (Task 11), `SourcePageInfo`, `SizePt` (Task 5, 1)
- Produces: `sealed class DocumentSet : IDisposable { DocumentSet(IReadOnlyList<SourceDocument> documents); IReadOnlyList<SourcePageInfo> Pages { get; } RasterPage Render(int combinedIndex, double dpi); string FileNameOf(int combinedIndex); }`

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.Documents.Tests/DocumentSetTests.cs`:

```csharp
using System.Drawing.Imaging;
using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents.Tests;

public class DocumentSetTests : IDisposable
{
    private readonly string _cacheRoot = Directory.CreateTempSubdirectory("kolayyazdir-set").FullName;
    private readonly List<string> _temporaryFiles = [];
    private readonly List<SourceDocument> _documents = [];

    private async Task<SourceDocument> Load(string path)
    {
        _temporaryFiles.Add(path);
        var loader = new DocumentLoader(OfficeConverterChain.Default, new ConversionCache(_cacheRoot));
        var document = await loader.LoadAsync(path, CancellationToken.None);
        _documents.Add(document);
        return document;
    }

    [Fact]
    public async Task Pages_of_every_document_are_concatenated_in_order()
    {
        var twoPagePdf = await Load(PdfFixtures.Create((595.276, 841.890), (595.276, 841.890)));
        var image = await Load(ImageFixtures.Create(600, 400, 96, ImageFormat.Png));

        using var set = new DocumentSet([twoPagePdf, image]);

        Assert.Equal(3, set.Pages.Count);
    }

    [Fact]
    public async Task Combined_indexes_run_from_zero_without_gaps()
    {
        var pdf = await Load(PdfFixtures.Create((595.276, 841.890), (595.276, 841.890)));
        var image = await Load(ImageFixtures.Create(600, 400, 96, ImageFormat.Png));

        using var set = new DocumentSet([pdf, image]);

        Assert.Equal([0, 1, 2], set.Pages.Select(p => p.Index));
    }

    [Fact]
    public async Task Each_page_carries_the_size_of_its_own_document()
    {
        var a4 = await Load(PdfFixtures.Create((595.276, 841.890)));
        var image = await Load(ImageFixtures.Create(600, 400, 96, ImageFormat.Png));

        using var set = new DocumentSet([a4, image]);

        Assert.Equal(595.276, set.Pages[0].Size.Width, 0);
        Assert.Equal(450, set.Pages[1].Size.Width, 0);
    }

    [Fact]
    public async Task Render_reaches_the_right_document()
    {
        var pdf = await Load(PdfFixtures.Create((595.276, 841.890)));
        var image = await Load(ImageFixtures.Create(600, 400, 96, ImageFormat.Png));

        using var set = new DocumentSet([pdf, image]);

        var second = set.Render(1, dpi: 96);

        Assert.Equal(600, second.WidthPx);
        Assert.Equal(400, second.HeightPx);
    }

    [Fact]
    public async Task File_name_lookup_reports_the_owning_document()
    {
        var pdf = await Load(PdfFixtures.Create((595.276, 841.890), (595.276, 841.890)));
        var image = await Load(ImageFixtures.Create(600, 400, 96, ImageFormat.Png));

        using var set = new DocumentSet([pdf, image]);

        Assert.Equal(pdf.FileName, set.FileNameOf(1));
        Assert.Equal(image.FileName, set.FileNameOf(2));
    }

    [Fact]
    public void An_empty_set_has_no_pages()
    {
        using var set = new DocumentSet([]);

        Assert.Empty(set.Pages);
    }

    [Fact]
    public async Task Out_of_range_index_is_rejected()
    {
        var pdf = await Load(PdfFixtures.Create((595.276, 841.890)));
        using var set = new DocumentSet([pdf]);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.Render(5, 96));
    }

    public void Dispose()
    {
        foreach (var document in _documents) document.Dispose();
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
        try { Directory.Delete(_cacheRoot, recursive: true); } catch (IOException) { }
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests --filter DocumentSetTests`
Expected: FAIL — `DocumentSet` bulunamıyor

- [ ] **Step 3: Uygulamayı yaz**

`src/KolayYazdir.Documents/DocumentSet.cs`:

```csharp
using KolayYazdir.Core.Models;

namespace KolayYazdir.Documents;

/// <summary>
/// Seçilen dosyaların sayfalarını tek bir diziye dizer. Yerleşim motoru
/// sadece bu diziyi görür; hangi sayfanın hangi dosyadan geldiğini bilmez.
/// Sahiplik burada değildir — belgeler dışarıdan verilir, <see cref="Dispose"/>
/// hepsini kapatır.
/// </summary>
public sealed class DocumentSet : IDisposable
{
    private readonly IReadOnlyList<SourceDocument> _documents;

    /// <summary>Birleşik indeksten (belge, o belgedeki sayfa) eşlemesi.</summary>
    private readonly List<(int Document, int Page)> _map = [];

    public DocumentSet(IReadOnlyList<SourceDocument> documents)
    {
        _documents = documents;

        var pages = new List<SourcePageInfo>();
        for (var d = 0; d < documents.Count; d++)
        {
            for (var p = 0; p < documents[d].PageCount; p++)
            {
                pages.Add(new SourcePageInfo(pages.Count, documents[d].PageSize(p)));
                _map.Add((d, p));
            }
        }

        Pages = pages;
    }

    public IReadOnlyList<SourcePageInfo> Pages { get; }

    public RasterPage Render(int combinedIndex, double dpi)
    {
        var (document, page) = Locate(combinedIndex);
        return _documents[document].Render(page, dpi);
    }

    public string FileNameOf(int combinedIndex) => _documents[Locate(combinedIndex).Document].FileName;

    private (int Document, int Page) Locate(int combinedIndex)
    {
        if (combinedIndex < 0 || combinedIndex >= _map.Count)
            throw new ArgumentOutOfRangeException(nameof(combinedIndex), combinedIndex, "Böyle bir sayfa yok.");

        return _map[combinedIndex];
    }

    public void Dispose()
    {
        foreach (var document in _documents) document.Dispose();
    }
}
```

- [ ] **Step 4: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Documents.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/KolayYazdir.Documents/DocumentSet.cs tests/KolayYazdir.Documents.Tests/DocumentSetTests.cs
git commit -m "Dosyaları tek sayfa dizisinde birleştiren DocumentSet"
```

---

### Task 13: SheetRenderer

Bir `Sheet`'i çizer. Önizleme ve yazıcı bunu farklı DPI ile çağırır; ekranda gördüğün ile kağıda çıkan bu yüzden aynıdır.

**Files:**
- Create: `src/KolayYazdir.Printing/KolayYazdir.Printing.csproj`
- Create: `src/KolayYazdir.Printing/SheetRenderer.cs`
- Create: `src/KolayYazdir.Printing/RenderConstants.cs`
- Create: `tests/KolayYazdir.Printing.Tests/KolayYazdir.Printing.Tests.csproj`
- Test: `tests/KolayYazdir.Printing.Tests/SheetRendererTests.cs`

**Interfaces:**
- Consumes: `Sheet`, `PlacedPage`, `SizePt`, `ColorMode` (Core), `DocumentSet.Render` (Task 12)
- Produces:
  - `static class RenderConstants { const double PrintDpi = 300; const double PreviewDpi = 110; }`
  - `interface IPageImageSource { RasterPage Render(int sourceIndex, double dpi); }`
  - `sealed class SheetRenderer(IPageImageSource source) { void Draw(Sheet sheet, Graphics graphics, double dpi, ColorMode color); Bitmap RenderToBitmap(Sheet sheet, double dpi, ColorMode color); }`

`DocumentSet`'in `IPageImageSource`'u karşılaması için Task 12'deki sınıfa `: IPageImageSource` eklenir (imza zaten uyuyor).

- [ ] **Step 1: Projeleri kur**

```bash
cd "D:/Desktop/Software/Personal Projects/Printer Tool"
dotnet new classlib -o src/KolayYazdir.Printing -f net8.0-windows
dotnet new xunit -o tests/KolayYazdir.Printing.Tests -f net8.0-windows
rm src/KolayYazdir.Printing/Class1.cs tests/KolayYazdir.Printing.Tests/UnitTest1.cs
dotnet sln add src/KolayYazdir.Printing tests/KolayYazdir.Printing.Tests
dotnet add src/KolayYazdir.Printing reference src/KolayYazdir.Core src/KolayYazdir.Documents
dotnet add tests/KolayYazdir.Printing.Tests reference src/KolayYazdir.Printing
dotnet add src/KolayYazdir.Printing package System.Drawing.Common --version 8.0.10
dotnet add tests/KolayYazdir.Printing.Tests package Xunit.SkippableFact --version 1.4.13
```

`src/KolayYazdir.Printing/KolayYazdir.Printing.csproj` içine `<Nullable>enable</Nullable>` ve `<ImplicitUsings>enable</ImplicitUsings>` ekle.

`src/KolayYazdir.Documents/DocumentSet.cs` içinde sınıf bildirimini değiştir:

```csharp
public sealed class DocumentSet : IPageImageSource, IDisposable
```

ve `IPageImageSource` arayüzünü `src/KolayYazdir.Documents/IPageRasterizer.cs` dosyasının sonuna ekle:

```csharp
/// <summary>Birleşik sayfa indeksinden piksel üretebilen kaynak.</summary>
public interface IPageImageSource
{
    RasterPage Render(int sourceIndex, double dpi);
}
```

- [ ] **Step 2: Başarısız testi yaz**

`tests/KolayYazdir.Printing.Tests/SheetRendererTests.cs`:

```csharp
using System.Drawing;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Documents;

namespace KolayYazdir.Printing.Tests;

public class SheetRendererTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    /// <summary>Her sayfayı düz renkli bir kare olarak veren sahte kaynak.</summary>
    private sealed class SolidColourSource(Color colour, int sizePx = 64) : IPageImageSource
    {
        public RasterPage Render(int sourceIndex, double dpi)
        {
            var bytes = new byte[sizePx * sizePx * 4];
            for (var i = 0; i < bytes.Length; i += 4)
            {
                bytes[i] = colour.B;
                bytes[i + 1] = colour.G;
                bytes[i + 2] = colour.R;
                bytes[i + 3] = 255;
            }

            return new RasterPage(sizePx, sizePx, bytes);
        }
    }

    private static IReadOnlyList<Sheet> Sheets(int pageCount, PagesPerSheet nUp)
    {
        var pages = Enumerable.Range(0, pageCount)
            .Select(i => new SourcePageInfo(i, new SizePt(200, 200)))
            .ToList();

        return LayoutEngine.Build(pages, new PrintSettings { PagesPerSheet = nUp, FitToPage = true }, FullBleed);
    }

    /// <summary>Bir noktadaki pikselin "koyu" olup olmadığı.</summary>
    private static bool IsDark(Bitmap bitmap, double fractionX, double fractionY)
    {
        var pixel = bitmap.GetPixel(
            (int)(bitmap.Width * fractionX),
            (int)(bitmap.Height * fractionY));

        return pixel.R < 128 && pixel.G < 128 && pixel.B < 128;
    }

    [Fact]
    public void Rendered_bitmap_matches_the_paper_aspect_ratio()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));
        var sheet = Sheets(1, PagesPerSheet.One)[0];

        using var bitmap = renderer.RenderToBitmap(sheet, dpi: 72, ColorMode.Color);

        Assert.Equal(596, bitmap.Width, 2);
        Assert.Equal(842, bitmap.Height, 2);
    }

    [Fact]
    public void A_blank_sheet_renders_all_white()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));
        var blank = new Sheet(0, SheetSide.Back, A4, []);

        using var bitmap = renderer.RenderToBitmap(blank, dpi: 72, ColorMode.Color);

        Assert.False(IsDark(bitmap, 0.5, 0.5));
        Assert.False(IsDark(bitmap, 0.25, 0.25));
    }

    [Fact]
    public void One_up_content_lands_in_the_middle_of_the_page()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));

        using var bitmap = renderer.RenderToBitmap(Sheets(1, PagesPerSheet.One)[0], dpi: 72, ColorMode.Color);

        Assert.True(IsDark(bitmap, 0.5, 0.5), "sayfanın ortası dolu olmalı");
    }

    [Fact]
    public void Four_up_fills_all_four_quadrants()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));

        using var bitmap = renderer.RenderToBitmap(Sheets(4, PagesPerSheet.Four)[0], dpi: 72, ColorMode.Color);

        Assert.True(IsDark(bitmap, 0.25, 0.25), "sol üst hücre dolu olmalı");
        Assert.True(IsDark(bitmap, 0.75, 0.25), "sağ üst hücre dolu olmalı");
        Assert.True(IsDark(bitmap, 0.25, 0.75), "sol alt hücre dolu olmalı");
        Assert.True(IsDark(bitmap, 0.75, 0.75), "sağ alt hücre dolu olmalı");
    }

    [Fact]
    public void Cells_left_empty_by_a_partial_sheet_stay_white()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));
        var sheets = Sheets(5, PagesPerSheet.Four);

        using var bitmap = renderer.RenderToBitmap(sheets[1], dpi: 72, ColorMode.Color);

        Assert.True(IsDark(bitmap, 0.25, 0.25), "ikinci yaprakta ilk hücre dolu olmalı");
        Assert.False(IsDark(bitmap, 0.75, 0.75), "kalan hücreler boş kalmalı");
    }

    [Fact]
    public void Monochrome_turns_colour_into_grey()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Red));

        using var bitmap = renderer.RenderToBitmap(Sheets(1, PagesPerSheet.One)[0], dpi: 72, ColorMode.Monochrome);

        var pixel = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);

        Assert.Equal(pixel.R, pixel.G);
        Assert.Equal(pixel.G, pixel.B);
    }

    [Fact]
    public void Colour_mode_keeps_the_original_hue()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Red));

        using var bitmap = renderer.RenderToBitmap(Sheets(1, PagesPerSheet.One)[0], dpi: 72, ColorMode.Color);

        var pixel = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);

        Assert.True(pixel.R > 200 && pixel.G < 80 && pixel.B < 80, $"beklenen kırmızı, gelen {pixel}");
    }

    [Fact]
    public void Higher_dpi_produces_a_proportionally_larger_bitmap()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));
        var sheet = Sheets(1, PagesPerSheet.One)[0];

        using var low = renderer.RenderToBitmap(sheet, dpi: 72, ColorMode.Color);
        using var high = renderer.RenderToBitmap(sheet, dpi: 144, ColorMode.Color);

        Assert.Equal(low.Width * 2, high.Width, 2);
    }
}
```

- [ ] **Step 3: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Printing.Tests`
Expected: FAIL — `SheetRenderer` bulunamıyor

- [ ] **Step 4: Uygulamayı yaz**

`src/KolayYazdir.Printing/RenderConstants.cs`:

```csharp
namespace KolayYazdir.Printing;

public static class RenderConstants
{
    /// <summary>
    /// Baskı çözünürlüğü. A4 bir sayfa bu değerde yaklaşık 34 MB tutar;
    /// 600 DPI'a çıkmak sayfayı şeritler halinde render etmeyi gerektirirdi ve
    /// lazer çıktıda gözle ayırt edilir bir kazanç sağlamazdı.
    /// </summary>
    public const double PrintDpi = 300;

    /// <summary>Ekran önizlemesi için yeterli çözünürlük.</summary>
    public const double PreviewDpi = 110;
}
```

`src/KolayYazdir.Printing/SheetRenderer.cs`:

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Documents;

namespace KolayYazdir.Printing;

/// <summary>
/// Bir yaprağı çizer. Önizleme ve yazıcı aynı metodu farklı DPI ile çağırır;
/// ekranda görülen ile kağıda çıkan bu yüzden birbirinin aynısıdır.
/// </summary>
public sealed class SheetRenderer(IPageImageSource source)
{
    /// <summary>Renkli görüntüyü göze doğal gelen ağırlıklarla griye çevirir.</summary>
    private static readonly ColorMatrix GreyscaleMatrix = new(
    [
        [0.299f, 0.299f, 0.299f, 0, 0],
        [0.587f, 0.587f, 0.587f, 0, 0],
        [0.114f, 0.114f, 0.114f, 0, 0],
        [0, 0, 0, 1, 0],
        [0, 0, 0, 0, 1]
    ]);

    /// <summary>Yaprağı yeni bir bitmap'e çizer. Çağıran bitmap'i kapatmalıdır.</summary>
    public Bitmap RenderToBitmap(Sheet sheet, double dpi, ColorMode color)
    {
        var width = (int)Math.Round(sheet.Paper.Width / 72.0 * dpi);
        var height = (int)Math.Round(sheet.Paper.Height / 72.0 * dpi);

        var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppArgb);
        bitmap.SetResolution((float)dpi, (float)dpi);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            Draw(sheet, graphics, dpi, color);
        }

        return bitmap;
    }

    /// <summary>
    /// Yaprağı hazır bir yüzeye çizer. Koordinatlar punto cinsindendir;
    /// yüzeyin kendi ölçeği <see cref="Graphics.PageUnit"/> ile ayarlanır.
    /// </summary>
    public void Draw(Sheet sheet, Graphics graphics, double dpi, ColorMode color)
    {
        graphics.PageUnit = GraphicsUnit.Point;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        foreach (var placed in sheet.Pages)
        {
            if (placed.Destination.Width <= 0 || placed.Destination.Height <= 0) continue;

            // İçeriği hedef dikdörtgenin kısa kenarına göre yeterli çözünürlükte
            // isteyip yeniden örneklemeyi GDI+'a bırakıyoruz.
            var raster = source.Render(placed.SourceIndex, dpi);
            using var image = ToBitmap(raster);

            DrawPlaced(graphics, image, placed, color);
        }
    }

    private static void DrawPlaced(Graphics graphics, Bitmap image, PlacedPage placed, ColorMode color)
    {
        var state = graphics.Save();
        try
        {
            var destination = placed.Destination;
            graphics.TranslateTransform((float)destination.X, (float)destination.Y);

            // 90° saat yönünde döndürme: önce sağ üst köşeye taşı, sonra döndür.
            // Böylece dönmüş içerik tam olarak hedef dikdörtgeni doldurur.
            var width = (float)destination.Width;
            var height = (float)destination.Height;

            if (placed.RotationDegrees == 90)
            {
                graphics.TranslateTransform(width, 0);
                graphics.RotateTransform(90);
                (width, height) = (height, width);
            }

            var target = new RectangleF(0, 0, width, height);

            if (color == ColorMode.Monochrome)
            {
                using var attributes = new ImageAttributes();
                attributes.SetColorMatrix(GreyscaleMatrix);
                graphics.DrawImage(image, Rectangle.Round(target),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
            else
            {
                graphics.DrawImage(image, target);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>Dolgusuz BGRA dizisini GDI+ bitmap'ine sarar.</summary>
    private static Bitmap ToBitmap(RasterPage page)
    {
        var bitmap = new Bitmap(page.WidthPx, page.HeightPx, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, page.WidthPx, page.HeightPx),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            for (var row = 0; row < page.HeightPx; row++)
            {
                Marshal.Copy(page.Bgra, row * page.WidthPx * 4, data.Scan0 + row * data.Stride, page.WidthPx * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}
```

- [ ] **Step 5: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Printing.Tests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/KolayYazdir.Printing tests/KolayYazdir.Printing.Tests src/KolayYazdir.Documents KolayYazdir.sln
git commit -m "Önizleme ve baskının paylaştığı SheetRenderer"
```

---

### Task 14: Elle önlü arkalı sıralaması

Yazıcı otomatik dupleks desteklemiyorsa iki geçişte basarız. Sıralama saf mantıktır, `Core` içinde yaşar ve yazıcısız test edilir.

**Files:**
- Create: `src/KolayYazdir.Core/Layout/ManualDuplexPlan.cs`
- Test: `tests/KolayYazdir.Core.Tests/ManualDuplexPlanTests.cs`

**Interfaces:**
- Produces: `sealed record ManualDuplexPlan(IReadOnlyList<Sheet> FirstPass, IReadOnlyList<Sheet> SecondPass) { static ManualDuplexPlan Split(IReadOnlyList<Sheet> sheets); bool NeedsSecondPass { get; } }`

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.Core.Tests/ManualDuplexPlanTests.cs`:

```csharp
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class ManualDuplexPlanTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    private static IReadOnlyList<Sheet> DuplexSheets(int pageCount) =>
        LayoutEngine.Build(
            Enumerable.Range(0, pageCount).Select(i => new SourcePageInfo(i, A4)).ToList(),
            new PrintSettings { Duplex = DuplexMode.Duplex },
            FullBleed);

    [Fact]
    public void Fronts_go_in_the_first_pass()
    {
        var plan = ManualDuplexPlan.Split(DuplexSheets(6));

        Assert.All(plan.FirstPass, s => Assert.Equal(SheetSide.Front, s.Side));
        Assert.Equal(3, plan.FirstPass.Count);
    }

    [Fact]
    public void Fronts_keep_their_natural_order()
    {
        var plan = ManualDuplexPlan.Split(DuplexSheets(6));

        Assert.Equal([0, 1, 2], plan.FirstPass.Select(s => s.Index));
    }

    [Fact]
    public void Backs_are_printed_in_reverse_leaf_order()
    {
        // Yüzü aşağı çıkaran yazıcılarda ön yüzler tepsiye ters sırayla yığılır;
        // kağıt destesi çevrilip geri konduğunda son yaprak en üsttedir.
        var plan = ManualDuplexPlan.Split(DuplexSheets(6));

        Assert.Equal([2, 1, 0], plan.SecondPass.Select(s => s.Index));
    }

    [Fact]
    public void Backs_are_all_back_sides()
    {
        var plan = ManualDuplexPlan.Split(DuplexSheets(6));

        Assert.All(plan.SecondPass, s => Assert.Equal(SheetSide.Back, s.Side));
    }

    [Fact]
    public void A_blank_back_is_still_printed_to_keep_the_stack_aligned()
    {
        var plan = ManualDuplexPlan.Split(DuplexSheets(3));

        Assert.Equal(2, plan.FirstPass.Count);
        Assert.Equal(2, plan.SecondPass.Count);
        Assert.Contains(plan.SecondPass, s => s.IsBlank);
    }

    [Fact]
    public void A_simplex_job_needs_no_second_pass()
    {
        var simplex = LayoutEngine.Build(
            Enumerable.Range(0, 3).Select(i => new SourcePageInfo(i, A4)).ToList(),
            new PrintSettings(),
            FullBleed);

        var plan = ManualDuplexPlan.Split(simplex);

        Assert.False(plan.NeedsSecondPass);
        Assert.Equal(3, plan.FirstPass.Count);
        Assert.Empty(plan.SecondPass);
    }

    [Fact]
    public void A_duplex_job_needs_a_second_pass()
    {
        Assert.True(ManualDuplexPlan.Split(DuplexSheets(2)).NeedsSecondPass);
    }

    [Fact]
    public void An_empty_job_produces_empty_passes()
    {
        var plan = ManualDuplexPlan.Split([]);

        Assert.Empty(plan.FirstPass);
        Assert.Empty(plan.SecondPass);
        Assert.False(plan.NeedsSecondPass);
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests --filter ManualDuplexPlanTests`
Expected: FAIL — `ManualDuplexPlan` bulunamıyor

- [ ] **Step 3: Uygulamayı yaz**

`src/KolayYazdir.Core/Layout/ManualDuplexPlan.cs`:

```csharp
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Layout;

/// <summary>
/// Otomatik dupleks olmayan yazıcılar için iki geçişlik baskı sırası.
/// Önce tüm ön yüzler basılır, kullanıcı desteyi çevirip tepsiye koyar,
/// sonra arka yüzler ters sırayla basılır.
/// </summary>
/// <remarks>
/// Ters sıra, ön yüzleri yüzü aşağı çıkaran yazıcılara göredir: bu yazıcılarda
/// çıkan deste ilk yaprak en altta olacak şekilde birikir, olduğu gibi çevrilip
/// geri konduğunda son yaprak öne geçer. Yüzü yukarı çıkaran bir yazıcıda bu
/// sıra ters olur — dükkandaki yazıcıda yerinde doğrulanmalıdır.
/// </remarks>
public sealed record ManualDuplexPlan(IReadOnlyList<Sheet> FirstPass, IReadOnlyList<Sheet> SecondPass)
{
    public bool NeedsSecondPass => SecondPass.Count > 0;

    public static ManualDuplexPlan Split(IReadOnlyList<Sheet> sheets)
    {
        var fronts = sheets.Where(s => s.Side == SheetSide.Front).ToList();
        var backs = sheets.Where(s => s.Side == SheetSide.Back).Reverse().ToList();

        return new ManualDuplexPlan(fronts, backs);
    }
}
```

- [ ] **Step 4: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Core.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/KolayYazdir.Core/Layout/ManualDuplexPlan.cs tests/KolayYazdir.Core.Tests/ManualDuplexPlanTests.cs
git commit -m "Elle önlü arkalı iki geçişli baskı sırası"
```

---

### Task 15: Yazıcı yetenekleri

Sürücüden kağıt cinsi isimlerini ("Düz", "Kalın 1"), dupleks ve renk desteğini okur. Bu, uygulamanın Windows'un yazdırma yığınına dokunduğu ilk yerdir.

**Files:**
- Create: `src/KolayYazdir.Printing/Interop/NativeMethods.cs`
- Create: `src/KolayYazdir.Printing/PrinterCapabilities.cs`
- Test: `tests/KolayYazdir.Printing.Tests/PrinterCapabilitiesTests.cs`

**Interfaces:**
- Produces:
  - `readonly record struct MediaType(int Id, string Name)`
  - `sealed record PrinterCapabilities(string PrinterName, bool SupportsColor, bool SupportsAutomaticDuplex, bool SupportsMultipleCopies, IReadOnlyList<MediaType> MediaTypes, RectPt PrintableArea) { static PrinterCapabilities? Read(string printerName, PaperFormat, Orientation); static string? DefaultPrinterName { get; } }`

`SupportsMultipleCopies` tek bir yerde hesaplanır ve hem görünüm modeli hem
baskı çalıştırıcısı bu tek cevaba uyar. Kopyayı kimin çoğaltacağı kararının
iki yerde ayrı verilmesi, sürücünün kopyalamayı desteklemediği durumda tek
kopya basılmasına yol açardı.

Sürücü kağıt cinsi listesi vermezse `MediaTypes` iki girdiyle doldurulur: `(1, "Düz")` ve `(3, "Kalın")` — bunlar `DMMEDIA_STANDARD` ve `DMMEDIA_USER` üstü ilk sürücü tanımlı değerdir ve spec'teki yedek eşlemedir.

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.Printing.Tests/PrinterCapabilitiesTests.cs`:

```csharp
using System.Drawing.Printing;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Printing.Tests;

public class PrinterCapabilitiesTests
{
    private const string VirtualPrinter = "Microsoft Print to PDF";

    private static bool HasVirtualPrinter =>
        PrinterSettings.InstalledPrinters.Cast<string>().Contains(VirtualPrinter);

    [SkippableFact]
    public void A_known_printer_can_be_read()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var capabilities = PrinterCapabilities.Read(VirtualPrinter, PaperFormat.A4, Orientation.Portrait);

        Assert.NotNull(capabilities);
        Assert.Equal(VirtualPrinter, capabilities.PrinterName);
    }

    [Fact]
    public void An_unknown_printer_returns_null_instead_of_throwing()
    {
        Assert.Null(PrinterCapabilities.Read("Böyle Bir Yazıcı Yok 12345", PaperFormat.A4, Orientation.Portrait));
    }

    [SkippableFact]
    public void Media_types_are_never_empty()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var capabilities = PrinterCapabilities.Read(VirtualPrinter, PaperFormat.A4, Orientation.Portrait)!;

        // Sürücü liste vermezse yedek "Düz / Kalın" eşlemesi devreye girer.
        Assert.NotEmpty(capabilities.MediaTypes);
        Assert.All(capabilities.MediaTypes, m => Assert.False(string.IsNullOrWhiteSpace(m.Name)));
    }

    [SkippableFact]
    public void Printable_area_fits_inside_the_paper()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var capabilities = PrinterCapabilities.Read(VirtualPrinter, PaperFormat.A4, Orientation.Portrait)!;
        var paper = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);

        Assert.True(capabilities.PrintableArea.Width > 0);
        Assert.True(capabilities.PrintableArea.Width <= paper.Width + 1);
        Assert.True(capabilities.PrintableArea.Height <= paper.Height + 1);
    }

    [SkippableFact]
    public void Landscape_printable_area_is_wider_than_it_is_tall()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var capabilities = PrinterCapabilities.Read(VirtualPrinter, PaperFormat.A4, Orientation.Landscape)!;

        Assert.True(capabilities.PrintableArea.Width > capabilities.PrintableArea.Height);
    }

    [SkippableFact]
    public void A_default_printer_name_is_reported_when_one_exists()
    {
        Skip.If(PrinterSettings.InstalledPrinters.Count == 0, "Bu makinede hiç yazıcı yok.");

        Assert.False(string.IsNullOrWhiteSpace(PrinterCapabilities.DefaultPrinterName));
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Printing.Tests --filter PrinterCapabilitiesTests`
Expected: FAIL — `PrinterCapabilities` bulunamıyor

- [ ] **Step 3: P/Invoke bildirimlerini yaz**

`src/KolayYazdir.Printing/Interop/NativeMethods.cs`:

```csharp
using System.Runtime.InteropServices;

namespace KolayYazdir.Printing.Interop;

/// <summary>
/// Windows yazdırma yığınına doğrudan erişim. .NET'in
/// <c>System.Drawing.Printing</c> katmanı kağıt cinsini ve çevirme kenarını
/// açmadığı için bu kadarını elle yapıyoruz.
/// </summary>
internal static partial class NativeMethods
{
    // DeviceCapabilities sorgu numaraları (wingdi.h)
    internal const int DC_DUPLEX = 7;
    internal const int DC_COLORDEVICE = 32;
    internal const int DC_MEDIATYPENAMES = 34;
    internal const int DC_MEDIATYPES = 35;

    /// <summary>Kağıt cinsi isimleri sabit 64 karakterlik alanlarda döner.</summary>
    internal const int MediaTypeNameLength = 64;

    [LibraryImport("winspool.drv", EntryPoint = "DeviceCapabilitiesW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int DeviceCapabilities(
        string device, string? port, int capability, IntPtr output, IntPtr deviceMode);

    [LibraryImport("kernel32.dll")]
    internal static partial IntPtr GlobalLock(IntPtr handle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalUnlock(IntPtr handle);

    [LibraryImport("kernel32.dll")]
    internal static partial IntPtr GlobalFree(IntPtr handle);
}
```

`src/KolayYazdir.Printing/KolayYazdir.Printing.csproj` içine `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` ekle (`LibraryImport` üreteci için gerekir).

- [ ] **Step 4: PrinterCapabilities'i yaz**

`src/KolayYazdir.Printing/PrinterCapabilities.cs`:

```csharp
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Printing.Interop;

namespace KolayYazdir.Printing;

/// <summary>Sürücünün tanıdığı bir kağıt cinsi.</summary>
public readonly record struct MediaType(int Id, string Name);

/// <summary>Bir yazıcının bizi ilgilendiren yetenekleri.</summary>
public sealed record PrinterCapabilities(
    string PrinterName,
    bool SupportsColor,
    bool SupportsAutomaticDuplex,
    bool SupportsMultipleCopies,
    IReadOnlyList<MediaType> MediaTypes,
    RectPt PrintableArea)
{
    /// <summary>Sürücü kağıt cinsi listesi vermediğinde kullanılan yedek eşleme.</summary>
    private static readonly MediaType[] FallbackMediaTypes =
    [
        new(1, "Düz"),
        new(3, "Kalın")
    ];

    public static string? DefaultPrinterName
    {
        get
        {
            var name = new PrinterSettings().PrinterName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }

    /// <returns>Yazıcı yoksa veya okunamıyorsa null.</returns>
    public static PrinterCapabilities? Read(string printerName, PaperFormat paper, Orientation orientation)
    {
        var settings = new PrinterSettings { PrinterName = printerName };
        if (!settings.IsValid) return null;

        try
        {
            return new PrinterCapabilities(
                printerName,
                settings.SupportsColor,
                ReadDuplexSupport(printerName),
                settings.MaximumCopies > 1,
                ReadMediaTypes(printerName),
                ReadPrintableArea(settings, paper, orientation));
        }
        catch (Exception ex) when (ex is InvalidPrinterException or ExternalException)
        {
            return null;
        }
    }

    private static bool ReadDuplexSupport(string printerName) =>
        NativeMethods.DeviceCapabilities(printerName, null, NativeMethods.DC_DUPLEX, IntPtr.Zero, IntPtr.Zero) == 1;

    private static IReadOnlyList<MediaType> ReadMediaTypes(string printerName)
    {
        var count = NativeMethods.DeviceCapabilities(
            printerName, null, NativeMethods.DC_MEDIATYPENAMES, IntPtr.Zero, IntPtr.Zero);

        if (count <= 0) return FallbackMediaTypes;

        var namesBuffer = Marshal.AllocHGlobal(count * NativeMethods.MediaTypeNameLength * sizeof(char));
        var idsBuffer = Marshal.AllocHGlobal(count * sizeof(int));
        try
        {
            var namesWritten = NativeMethods.DeviceCapabilities(
                printerName, null, NativeMethods.DC_MEDIATYPENAMES, namesBuffer, IntPtr.Zero);
            var idsWritten = NativeMethods.DeviceCapabilities(
                printerName, null, NativeMethods.DC_MEDIATYPES, idsBuffer, IntPtr.Zero);

            if (namesWritten <= 0) return FallbackMediaTypes;

            var types = new List<MediaType>(namesWritten);
            for (var i = 0; i < namesWritten; i++)
            {
                var offset = namesBuffer + i * NativeMethods.MediaTypeNameLength * sizeof(char);
                var name = Marshal.PtrToStringUni(offset, NativeMethods.MediaTypeNameLength)?
                    .TrimEnd('\0')
                    .Trim();

                if (string.IsNullOrWhiteSpace(name)) continue;

                var id = i < idsWritten ? Marshal.ReadInt32(idsBuffer, i * sizeof(int)) : i + 1;
                types.Add(new MediaType(id, name));
            }

            return types.Count > 0 ? types : FallbackMediaTypes;
        }
        finally
        {
            Marshal.FreeHGlobal(namesBuffer);
            Marshal.FreeHGlobal(idsBuffer);
        }
    }

    /// <summary>
    /// Yazıcının basabildiği alanı punto olarak verir. .NET bu bilgiyi
    /// yüzde bir inç biriminde tutar.
    /// </summary>
    private static RectPt ReadPrintableArea(PrinterSettings settings, PaperFormat paper, Orientation orientation)
    {
        var page = settings.DefaultPageSettings;
        page.Landscape = orientation == Orientation.Landscape;

        var area = page.PrintableArea;
        var expected = Paper.SizeOf(paper, orientation);

        // PrintableArea her zaman dikey kağıt koordinatlarında gelir; yatayda
        // kendimiz çeviriyoruz.
        var (x, y, width, height) = orientation == Orientation.Landscape
            ? (area.Y, area.X, area.Height, area.Width)
            : (area.X, area.Y, area.Width, area.Height);

        var rect = new RectPt(x * 0.72, y * 0.72, width * 0.72, height * 0.72);

        // Sürücü saçmalarsa (sıfır veya kağıttan büyük alan) tüm kağıda düş.
        if (rect.Width <= 0 || rect.Height <= 0 || rect.Width > expected.Width + 1 || rect.Height > expected.Height + 1)
            return new RectPt(0, 0, expected.Width, expected.Height);

        return rect;
    }
}
```

- [ ] **Step 5: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Printing.Tests --filter PrinterCapabilitiesTests`
Expected: PASS

- [ ] **Step 6: Kağıt cinsi isimlerini gözle doğrula**

Sürücünün verdiği isimlerin Windows'ta görünenlerle aynı olduğunu görmemiz
gerekiyor. Aşağıdaki testi geçici olarak ekle, çıktısını oku, sonra sil:

```csharp
    [SkippableFact]
    public void Print_media_types_for_manual_inspection()
    {
        var name = PrinterCapabilities.DefaultPrinterName;
        Skip.If(name is null, "Varsayılan yazıcı yok.");

        var capabilities = PrinterCapabilities.Read(name!, PaperFormat.A4, Orientation.Portrait)!;
        foreach (var media in capabilities.MediaTypes)
        {
            Console.WriteLine($"{media.Id}: {media.Name}");
        }

        Assert.True(true);
    }
```

Run: `dotnet test tests/KolayYazdir.Printing.Tests --filter Print_media_types_for_manual_inspection --logger "console;verbosity=detailed"`

Çıktıda "Düz" ve "Kalın 1" görünüyorsa doğru yoldayız. Gözlemi
`docs/superpowers/specs/2026-08-21-kolay-yazdir-design.md` içindeki "Açık riskler"
bölümüne not düş, sonra bu geçici testi sil.

Sürücü hiç isim vermiyorsa yedek eşleme devreye girer ve listede "Düz" ile
"Kalın" görünür — bu da kabul edilebilir bir sonuçtur, ama not edilmeli.

- [ ] **Step 7: Commit**

```bash
git add src/KolayYazdir.Printing tests/KolayYazdir.Printing.Tests
git commit -m "Sürücüden kağıt cinsi ve dupleks yeteneği okuma"
```

---

### Task 16: DEVMODE kurulumu ve baskı işi

Ayarları sürücüye geçirip yaprakları bastırır.

**Files:**
- Create: `src/KolayYazdir.Printing/Interop/DevMode.cs`
- Create: `src/KolayYazdir.Printing/PrintJobRunner.cs`
- Test: `tests/KolayYazdir.Printing.Tests/PrintJobRunnerTests.cs`

**Interfaces:**
- Produces:
  - `static class DevModeConfigurator { void Apply(PrinterSettings settings, PrintSettings print, bool driverHandlesCopies); }`
  - `sealed class PrintJobRunner(SheetRenderer renderer) { void Run(IReadOnlyList<Sheet> sheets, PrintSettings settings, string printerName, bool driverHandlesCopies, string? outputFile = null); }`

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.Printing.Tests/PrintJobRunnerTests.cs`:

```csharp
using System.Drawing;
using System.Drawing.Printing;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Documents;

namespace KolayYazdir.Printing.Tests;

public class PrintJobRunnerTests : IDisposable
{
    private const string VirtualPrinter = "Microsoft Print to PDF";
    private readonly string _output = Directory.CreateTempSubdirectory("kolayyazdir-print").FullName;

    private static bool HasVirtualPrinter =>
        PrinterSettings.InstalledPrinters.Cast<string>().Contains(VirtualPrinter);

    private sealed class BlackSquareSource : IPageImageSource
    {
        public RasterPage Render(int sourceIndex, double dpi)
        {
            var bytes = new byte[64 * 64 * 4];
            for (var i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
            return new RasterPage(64, 64, bytes);
        }
    }

    private static IReadOnlyList<Sheet> Sheets(int pageCount, PrintSettings settings)
    {
        var paper = Paper.SizeOf(settings.Paper, settings.Orientation);
        var pages = Enumerable.Range(0, pageCount)
            .Select(i => new SourcePageInfo(i, paper))
            .ToList();

        return LayoutEngine.Build(pages, settings, new RectPt(0, 0, paper.Width, paper.Height));
    }

    [SkippableFact]
    public void Devmode_carries_the_paper_size_and_orientation()
    {
        var settings = new PrinterSettings();
        Skip.IfNot(settings.IsValid, "Varsayılan yazıcı yok.");

        DevModeConfigurator.Apply(
            settings,
            new PrintSettings { Paper = PaperFormat.A3, Orientation = Orientation.Landscape },
            driverHandlesCopies: true);

        Assert.True(settings.DefaultPageSettings.Landscape);
        Assert.Equal((int)PaperKind.A3, (int)settings.DefaultPageSettings.PaperSize.Kind);
    }

    [SkippableFact]
    public void Devmode_carries_the_copy_count_when_the_driver_handles_copies()
    {
        var settings = new PrinterSettings();
        Skip.IfNot(settings.IsValid, "Varsayılan yazıcı yok.");

        DevModeConfigurator.Apply(settings, new PrintSettings { Copies = 4 }, driverHandlesCopies: true);

        Assert.Equal(4, settings.Copies);
    }

    [SkippableFact]
    public void Devmode_leaves_copies_at_one_when_the_app_repeats_sheets()
    {
        var settings = new PrinterSettings();
        Skip.IfNot(settings.IsValid, "Varsayılan yazıcı yok.");

        DevModeConfigurator.Apply(settings, new PrintSettings { Copies = 4 }, driverHandlesCopies: false);

        Assert.Equal(1, settings.Copies);
    }

    [SkippableFact]
    public void A_job_reaches_the_virtual_printer_and_produces_a_file()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var target = Path.Combine(_output, "cikti.pdf");
        var settings = new PrintSettings();
        var runner = new PrintJobRunner(new SheetRenderer(new BlackSquareSource()));

        runner.Run(Sheets(3, settings), settings, VirtualPrinter, driverHandlesCopies: true, target);

        Assert.True(File.Exists(target), "sanal yazıcı dosya üretmedi");
        Assert.True(new FileInfo(target).Length > 0);
    }

    [SkippableFact]
    public void The_produced_file_has_one_page_per_sheet()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var target = Path.Combine(_output, "sayfa-sayisi.pdf");
        var settings = new PrintSettings { PagesPerSheet = PagesPerSheet.Four };
        var runner = new PrintJobRunner(new SheetRenderer(new BlackSquareSource()));

        var sheets = Sheets(8, settings);
        runner.Run(sheets, settings, VirtualPrinter, driverHandlesCopies: true, target);

        using var produced = new PdfRasterizer(target);
        Assert.Equal(sheets.Count, produced.PageCount);
    }

    [Fact]
    public void An_empty_job_is_a_no_op()
    {
        var runner = new PrintJobRunner(new SheetRenderer(new BlackSquareSource()));

        // Yazıcıya hiç gitmeden dönmeli; geçersiz yazıcı adı bunu kanıtlar.
        runner.Run([], new PrintSettings(), "Böyle Bir Yazıcı Yok 12345", driverHandlesCopies: true);
    }

    public void Dispose()
    {
        try { Directory.Delete(_output, recursive: true); } catch (IOException) { }
    }
}
```

Testin `PdfRasterizer`'a erişebilmesi için referans ekle:

```bash
dotnet add tests/KolayYazdir.Printing.Tests reference src/KolayYazdir.Documents
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.Printing.Tests --filter PrintJobRunnerTests`
Expected: FAIL — `DevModeConfigurator` bulunamıyor

- [ ] **Step 3: DEVMODE yapısını ve yapılandırıcıyı yaz**

`src/KolayYazdir.Printing/Interop/DevMode.cs`:

```csharp
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Printing.Interop;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DevMode
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
    public short dmSpecVersion;
    public short dmDriverVersion;
    public short dmSize;
    public short dmDriverExtra;
    public int dmFields;
    public short dmOrientation;
    public short dmPaperSize;
    public short dmPaperLength;
    public short dmPaperWidth;
    public short dmScale;
    public short dmCopies;
    public short dmDefaultSource;
    public short dmPrintQuality;
    public short dmColor;
    public short dmDuplex;
    public short dmYResolution;
    public short dmTTOption;
    public short dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
    public short dmLogPixels;
    public int dmBitsPerPel;
    public int dmPelsWidth;
    public int dmPelsHeight;
    public int dmDisplayFlags;
    public int dmDisplayFrequency;
    public int dmICMMethod;
    public int dmICMIntent;
    public int dmMediaType;
    public int dmDitherType;
    public int dmReserved1;
    public int dmReserved2;
    public int dmPanningWidth;
    public int dmPanningHeight;
}

/// <summary>DEVMODE alan bayrakları ve değerleri (wingdi.h).</summary>
internal static class DevModeFields
{
    public const int Orientation = 0x00000001;
    public const int PaperSize = 0x00000002;
    public const int Copies = 0x00000100;
    public const int Color = 0x00000800;
    public const int Duplex = 0x00001000;
    public const int Collate = 0x00008000;
    public const int MediaType = 0x02000000;

    public const short OrientPortrait = 1;
    public const short OrientLandscape = 2;

    public const short ColorMonochrome = 1;
    public const short ColorColour = 2;

    public const short DuplexSimplex = 1;

    /// <summary>Uzun kenardan çevir — dikey kağıtta kitap gibi açılır.</summary>
    public const short DuplexVertical = 2;

    /// <summary>Kısa kenardan çevir — yatay kağıtta bloknot gibi açılır.</summary>
    public const short DuplexHorizontal = 3;

    public const short CollateTrue = 1;

    public const short PaperA3 = 8;
    public const short PaperA4 = 9;
    public const short PaperA5 = 11;

    public static short PaperCode(PaperFormat format) => format switch
    {
        PaperFormat.A4 => PaperA4,
        PaperFormat.A5 => PaperA5,
        PaperFormat.A3 => PaperA3,
        _ => PaperA4
    };
}
```

`src/KolayYazdir.Printing/PrintJobRunner.cs` (yapılandırıcı ile birlikte):

```csharp
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using KolayYazdir.Core.Models;
using KolayYazdir.Printing.Interop;

namespace KolayYazdir.Printing;

/// <summary>
/// Kullanıcının seçimlerini sürücünün anladığı DEVMODE yapısına yazar.
/// .NET'in <c>PrinterSettings</c> sınıfı kağıt cinsini ve çevirme kenarını
/// açmadığı için yapıya doğrudan dokunuyoruz.
/// </summary>
public static class DevModeConfigurator
{
    public static void Apply(PrinterSettings settings, PrintSettings print, bool driverHandlesCopies)
    {
        var handle = settings.GetHdevmode(settings.DefaultPageSettings);
        var pointer = NativeMethods.GlobalLock(handle);

        try
        {
            var mode = Marshal.PtrToStructure<DevMode>(pointer);

            mode.dmOrientation = print.Orientation == Orientation.Landscape
                ? DevModeFields.OrientLandscape
                : DevModeFields.OrientPortrait;
            mode.dmPaperSize = DevModeFields.PaperCode(print.Paper);
            mode.dmColor = print.Color == ColorMode.Color
                ? DevModeFields.ColorColour
                : DevModeFields.ColorMonochrome;
            mode.dmDuplex = print.Duplex == DuplexMode.Simplex
                ? DevModeFields.DuplexSimplex
                : print.Binding == DuplexBinding.LongEdge
                    ? DevModeFields.DuplexVertical
                    : DevModeFields.DuplexHorizontal;
            mode.dmCopies = (short)(driverHandlesCopies ? Math.Max(1, print.Copies) : 1);
            mode.dmCollate = DevModeFields.CollateTrue;

            mode.dmFields |= DevModeFields.Orientation
                | DevModeFields.PaperSize
                | DevModeFields.Color
                | DevModeFields.Duplex
                | DevModeFields.Copies
                | DevModeFields.Collate;

            if (print.MediaTypeId is { } mediaType)
            {
                mode.dmMediaType = mediaType;
                mode.dmFields |= DevModeFields.MediaType;
            }

            Marshal.StructureToPtr(mode, pointer, fDeleteOld: false);
        }
        finally
        {
            NativeMethods.GlobalUnlock(handle);
        }

        settings.SetHdevmode(handle);
        settings.DefaultPageSettings.SetHdevmode(handle);
        NativeMethods.GlobalFree(handle);
    }
}

/// <summary>Hazır yaprak listesini yazıcıya gönderir.</summary>
public sealed class PrintJobRunner(SheetRenderer renderer)
{
    /// <param name="driverHandlesCopies">
    /// Sürücü kopyalamayı üstleniyorsa true; bu durumda kopya sayısı DEVMODE'a
    /// yazılır. False ise kopya sayısı 1'e sabitlenir ve çağıran tarafın
    /// yaprakları <c>LayoutEngine.Repeat</c> ile çoğaltmış olması beklenir.
    /// Karar <see cref="PrinterCapabilities.SupportsMultipleCopies"/>'den gelir;
    /// burada yeniden hesaplanmaz ki iki yer farklı sonuca varmasın.
    /// </param>
    /// <param name="outputFile">Doluysa çıktı dosyaya yazılır (sanal yazıcı testleri için).</param>
    public void Run(
        IReadOnlyList<Sheet> sheets,
        PrintSettings settings,
        string printerName,
        bool driverHandlesCopies,
        string? outputFile = null)
    {
        if (sheets.Count == 0) return;

        using var document = new PrintDocument();
        document.PrinterSettings.PrinterName = printerName;

        if (!document.PrinterSettings.IsValid)
            throw new InvalidPrinterException(document.PrinterSettings);

        DevModeConfigurator.Apply(document.PrinterSettings, settings, driverHandlesCopies);

        if (outputFile is not null)
        {
            document.PrinterSettings.PrintToFile = true;
            document.PrinterSettings.PrintFileName = outputFile;
        }

        document.DocumentName = "Kolay Yazdır";
        document.OriginAtMargins = false;

        var next = 0;
        document.PrintPage += (_, e) =>
        {
            var sheet = sheets[next++];

            if (!sheet.IsBlank && e.Graphics is { } graphics)
            {
                renderer.Draw(sheet, graphics, RenderConstants.PrintDpi, settings.Color);
            }

            e.HasMorePages = next < sheets.Count;
        };

        document.Print();
    }
}
```

- [ ] **Step 4: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.Printing.Tests`
Expected: PASS — "Microsoft Print to PDF" kuruluysa hepsi geçer, değilse ilgili testler `Skipped`

- [ ] **Step 5: Gerçek yazıcıda gözle doğrula**

Dükkandaki yazıcıya bağlı bilgisayarda:
1. A4 dikey, siyah beyaz, tek yön, 1'li — bir sayfa bas, kenar boşluklarını ölç.
2. Kağıt cinsini "Kalın 1" seç, kalın kağıt koy — yazıcının kağıdı doğru kavradığını gör.
3. Önlü arkalı seç, 4 sayfalık bir PDF bas — sayfa sırasının ve çevirme kenarının doğru olduğunu gör.

Gözlemleri `docs/superpowers/specs/2026-08-21-kolay-yazdir-design.md` içindeki "Açık riskler" bölümüne not düş.

- [ ] **Step 6: Commit**

```bash
git add src/KolayYazdir.Printing tests/KolayYazdir.Printing.Tests
git commit -m "DEVMODE kurulumu ve baskı işi çalıştırıcısı"
```

---

### Task 17: WPF kabuğu, karanlık tema ve segment düğmesi

Görsel iskelet. Bu görevin sonunda pencere açılıyor, tema doğru görünüyor ama hiçbir şey çalışmıyor.

**Files:**
- Create: `src/KolayYazdir.App/KolayYazdir.App.csproj`, `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`
- Create: `src/KolayYazdir.App/Theme/Dark.xaml`
- Create: `src/KolayYazdir.App/Controls/SegmentedControl.cs`
- Test: manuel (aşağıda)

**Interfaces:**
- Produces: `sealed class SegmentedControl : ItemsControl { object? SelectedValue { get; set; } string DisplayMemberPath { get; set; } }` — `SelectedValue` iki yönlü bağlanabilir.

- [ ] **Step 1: Projeyi kur**

```bash
cd "D:/Desktop/Software/Personal Projects/Printer Tool"
dotnet new wpf -o src/KolayYazdir.App -f net8.0-windows
dotnet sln add src/KolayYazdir.App
dotnet add src/KolayYazdir.App reference src/KolayYazdir.Core src/KolayYazdir.Documents src/KolayYazdir.Printing
dotnet add src/KolayYazdir.App package CommunityToolkit.Mvvm --version 8.4.0
```

`src/KolayYazdir.App/KolayYazdir.App.csproj` `PropertyGroup`'una ekle:

```xml
<Nullable>enable</Nullable>
<AssemblyName>KolayYazdir</AssemblyName>
<ApplicationTitle>Kolay Yazdır</ApplicationTitle>
<Version>1.0.0</Version>
```

- [ ] **Step 2: Temayı yaz**

`src/KolayYazdir.App/Theme/Dark.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <Color x:Key="BackgroundColor">#0A0A0A</Color>
  <Color x:Key="PanelColor">#141414</Color>
  <Color x:Key="BorderColor">#2E2E2E</Color>
  <Color x:Key="PrimaryTextColor">#FFFFFF</Color>
  <Color x:Key="SecondaryTextColor">#A8A8A8</Color>
  <Color x:Key="AccentColor">#FFD84D</Color>
  <Color x:Key="DangerColor">#FF6B6B</Color>

  <SolidColorBrush x:Key="Background" Color="{StaticResource BackgroundColor}"/>
  <SolidColorBrush x:Key="Panel" Color="{StaticResource PanelColor}"/>
  <SolidColorBrush x:Key="Border" Color="{StaticResource BorderColor}"/>
  <SolidColorBrush x:Key="PrimaryText" Color="{StaticResource PrimaryTextColor}"/>
  <SolidColorBrush x:Key="SecondaryText" Color="{StaticResource SecondaryTextColor}"/>
  <SolidColorBrush x:Key="Accent" Color="{StaticResource AccentColor}"/>
  <SolidColorBrush x:Key="Danger" Color="{StaticResource DangerColor}"/>

  <Style x:Key="SectionLabel" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource SecondaryText}"/>
    <Setter Property="FontSize" Value="11"/>
    <Setter Property="Margin" Value="0,0,0,5"/>
  </Style>

  <Style x:Key="AccentButton" TargetType="Button">
    <Setter Property="Background" Value="{StaticResource Accent}"/>
    <Setter Property="Foreground" Value="#141414"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Padding" Value="12,11"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Root" Background="{TemplateBinding Background}" CornerRadius="6">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                              Margin="{TemplateBinding Padding}"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Root" Property="Opacity" Value="0.88"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="TextBox">
    <Setter Property="Background" Value="#1E1E1E"/>
    <Setter Property="Foreground" Value="{StaticResource PrimaryText}"/>
    <Setter Property="CaretBrush" Value="{StaticResource PrimaryText}"/>
    <Setter Property="BorderBrush" Value="#3A3A3A"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="8,7"/>
    <Setter Property="FontSize" Value="12"/>
  </Style>

  <Style TargetType="CheckBox">
    <Setter Property="Foreground" Value="{StaticResource PrimaryText}"/>
    <Setter Property="FontSize" Value="12"/>
  </Style>

</ResourceDictionary>
```

`src/KolayYazdir.App/App.xaml` içindeki `Application.Resources`'a ekle:

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source="Theme/Dark.xaml"/>
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

- [ ] **Step 3: Segment düğmesini yaz**

`src/KolayYazdir.App/Controls/SegmentedControl.cs`:

```csharp
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace KolayYazdir.App.Controls;

/// <summary>
/// Yan yana duran, tek tıkla seçilen düğme grubu. Açılır kutu yerine bunu
/// kullanıyoruz: seçili olan tek bakışta görünür ve seçim tek tıkla değişir.
/// </summary>
public sealed class SegmentedControl : ItemsControl
{
    private static readonly Brush SelectedBackground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly Brush SelectedForeground = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));
    private static readonly Brush IdleBackground = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
    private static readonly Brush IdleForeground = new SolidColorBrush(Color.FromRgb(0xC4, 0xC4, 0xC4));
    private static readonly Brush IdleBorder = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));

    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(
        nameof(SelectedValue), typeof(object), typeof(SegmentedControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectionChanged));

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public SegmentedControl()
    {
        Focusable = false;

        var panel = new FrameworkElementFactory(typeof(UniformGrid));
        panel.SetValue(UniformGrid.RowsProperty, 1);
        ItemsPanel = new ItemsPanelTemplate(panel);
    }

    protected override DependencyObject GetContainerForItemOverride() => new SegmentButton(this);

    protected override bool IsItemItsOwnContainerOverride(object item) => item is SegmentButton;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is SegmentButton button) button.Bind(item);
        RefreshAppearance();
    }

    private static void OnSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((SegmentedControl)sender).RefreshAppearance();

    private void RefreshAppearance()
    {
        foreach (var item in Items)
        {
            if (ItemContainerGenerator.ContainerFromItem(item) is SegmentButton button)
            {
                button.SetSelected(Equals(item, SelectedValue));
            }
        }
    }

    private sealed class SegmentButton(SegmentedControl owner) : ButtonBase
    {
        private object? _value;

        public void Bind(object value)
        {
            _value = value;
            Content = value;
            Margin = new Thickness(2, 0, 2, 0);
            FontSize = 12;
            Padding = new Thickness(0, 8, 0, 8);
            HorizontalContentAlignment = HorizontalAlignment.Center;
            BorderThickness = new Thickness(1);

            Template = BuildTemplate();
            Click += (_, _) => owner.SelectedValue = _value;
        }

        public void SetSelected(bool selected)
        {
            Background = selected ? SelectedBackground : IdleBackground;
            Foreground = selected ? SelectedForeground : IdleForeground;
            BorderBrush = selected ? SelectedBackground : IdleBorder;
            FontWeight = selected ? FontWeights.Medium : FontWeights.Normal;
        }

        private static ControlTemplate BuildTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(BorderBrush)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(BorderThickness)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);

            return new ControlTemplate(typeof(SegmentButton)) { VisualTree = border };
        }
    }
}
```

- [ ] **Step 4: Pencereyi yaz**

`src/KolayYazdir.App/MainWindow.xaml` — ayar sütunu ve önizleme yerleşimi. Bağlamalar Task 18'de eklenecek; şimdilik statik.

```xml
<Window x:Class="KolayYazdir.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:KolayYazdir.App.Controls"
        Title="Kolay Yazdır" Height="760" Width="1080" MinHeight="620" MinWidth="900"
        Background="{StaticResource Background}"
        FontFamily="Segoe UI" UseLayoutRounding="True">

  <DockPanel>
    <Border DockPanel.Dock="Top" Background="{StaticResource Panel}"
            BorderBrush="{StaticResource Border}" BorderThickness="0,0,0,1" Padding="14,10">
      <DockPanel>
        <TextBlock Text="Yazdır" Foreground="{StaticResource PrimaryText}"
                   FontSize="14" FontWeight="Medium"/>
        <TextBlock x:Name="PrinterStatus" DockPanel.Dock="Right" HorizontalAlignment="Right"
                   Text="yazıcı aranıyor…" Foreground="{StaticResource SecondaryText}" FontSize="12"/>
      </DockPanel>
    </Border>

    <Border DockPanel.Dock="Left" Width="266" Background="{StaticResource Background}"
            BorderBrush="{StaticResource Border}" BorderThickness="0,0,1,0">
      <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="12">
        <StackPanel x:Name="SettingsColumn">

          <Button Style="{StaticResource AccentButton}" Content="Dosya seç"/>

          <Border Margin="0,12,0,0" BorderBrush="#3A3A3A" BorderThickness="1"
                  CornerRadius="6" MinHeight="70" Padding="8">
            <ListBox x:Name="FileList" Background="Transparent" BorderThickness="0"/>
          </Border>

          <TextBlock Style="{StaticResource SectionLabel}" Margin="0,14,0,5" Text="Kağıt boyutu"/>
          <controls:SegmentedControl x:Name="PaperSelector"/>

          <TextBlock Style="{StaticResource SectionLabel}" Margin="0,12,0,5" Text="Yön"/>
          <controls:SegmentedControl x:Name="OrientationSelector"/>

          <TextBlock Style="{StaticResource SectionLabel}" Margin="0,12,0,5" Text="Renk"/>
          <controls:SegmentedControl x:Name="ColorSelector"/>

          <TextBlock Style="{StaticResource SectionLabel}" Margin="0,12,0,5" Text="Yüz"/>
          <controls:SegmentedControl x:Name="DuplexSelector"/>
          <TextBlock x:Name="BindingHint" Foreground="#6E6E6E" FontSize="10" Margin="0,4,0,0"/>

          <TextBlock Style="{StaticResource SectionLabel}" Margin="0,12,0,5" Text="Kağıt cinsi"/>
          <ComboBox x:Name="MediaTypeSelector" FontSize="12"/>

          <TextBlock Style="{StaticResource SectionLabel}" Margin="0,12,0,5" Text="Sayfaya yerleşim"/>
          <controls:SegmentedControl x:Name="NUpSelector"/>

          <CheckBox x:Name="FitToPage" Margin="0,14,0,0" Content="Sayfaya sığdır"/>
          <CheckBox x:Name="AutoRotate" Margin="0,8,0,0" Content="İçeriği hücreye göre döndür"/>

          <Grid Margin="0,14,0,0">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="8"/>
              <ColumnDefinition Width="86"/>
            </Grid.ColumnDefinitions>
            <StackPanel Grid.Column="0">
              <TextBlock Style="{StaticResource SectionLabel}" Text="Sayfa aralığı"/>
              <TextBox x:Name="PageRange"/>
            </StackPanel>
            <StackPanel Grid.Column="2">
              <TextBlock Style="{StaticResource SectionLabel}" Text="Kopya"/>
              <TextBox x:Name="Copies" Text="1"/>
            </StackPanel>
          </Grid>

        </StackPanel>
      </ScrollViewer>
    </Border>

    <Grid Background="#0F0F0F">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
      </Grid.RowDefinitions>

      <DockPanel Grid.Row="0" Margin="12,12,12,8">
        <TextBlock Text="Önizleme" Foreground="{StaticResource SecondaryText}" FontSize="11"/>
        <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" HorizontalAlignment="Right">
          <Button x:Name="PreviousSheet" Content="‹" Width="26" Background="Transparent"
                  Foreground="{StaticResource PrimaryText}" BorderThickness="0" FontSize="16"/>
          <TextBlock x:Name="SheetLabel" Margin="8,0" VerticalAlignment="Center"
                     Foreground="{StaticResource PrimaryText}" FontSize="11"/>
          <Button x:Name="NextSheet" Content="›" Width="26" Background="Transparent"
                  Foreground="{StaticResource PrimaryText}" BorderThickness="0" FontSize="16"/>
        </StackPanel>
      </DockPanel>

      <Border Grid.Row="1" Margin="12,0" Background="#1A1A1A"
              BorderBrush="{StaticResource Border}" BorderThickness="1" CornerRadius="6">
        <Grid>
          <Image x:Name="PreviewImage" Margin="20" RenderOptions.BitmapScalingMode="HighQuality"/>
          <StackPanel x:Name="EmptyState" VerticalAlignment="Center" HorizontalAlignment="Center">
            <TextBlock Text="Yazdırmak için dosya seç" Foreground="{StaticResource SecondaryText}"
                       FontSize="14" HorizontalAlignment="Center" Margin="0,0,0,12"/>
            <Button Style="{StaticResource AccentButton}" Content="Dosya seç" Padding="20,10"/>
          </StackPanel>
        </Grid>
      </Border>

      <DockPanel Grid.Row="2" Margin="12">
        <TextBlock x:Name="JobSummary" VerticalAlignment="Center"
                   Foreground="{StaticResource SecondaryText}" FontSize="11"/>
        <Button x:Name="PrintButton" DockPanel.Dock="Right" HorizontalAlignment="Right"
                Style="{StaticResource AccentButton}" Content="Yazdır" Padding="30,12" FontSize="14"/>
      </DockPanel>
    </Grid>
  </DockPanel>
</Window>
```

- [ ] **Step 5: Pencereyi aç ve gözle doğrula**

Run: `dotnet run --project src/KolayYazdir.App`

Kontrol listesi:
- Pencere karanlık açılıyor, hiçbir yerde beyaz panel yok
- "Yazdır" ve "Dosya seç" düğmeleri sarı, üzerlerindeki yazı siyah
- Bölüm başlıkları soluk gri, ana yazılar beyaz
- Pencere küçültüldüğünde ayar sütunu kaydırılabiliyor, önizleme alanı daralıyor

- [ ] **Step 6: Commit**

```bash
git add src/KolayYazdir.App KolayYazdir.sln
git commit -m "WPF kabuğu, karanlık tema ve segment düğmesi"
```

---

### Task 18: Ana görünüm modeli ve canlı önizleme

Ayarlar değiştikçe yerleşimi yeniden hesaplayıp önizlemeyi tazeleyen katman. Mantık WPF'ten bağımsız yazılır, böylece test edilebilir.

**Files:**
- Create: `src/KolayYazdir.App/ViewModels/MainViewModel.cs`
- Create: `src/KolayYazdir.App/ViewModels/PreviewState.cs`
- Modify: `src/KolayYazdir.App/MainWindow.xaml` (bağlamalar), `MainWindow.xaml.cs`
- Create: `tests/KolayYazdir.App.Tests/KolayYazdir.App.Tests.csproj`
- Test: `tests/KolayYazdir.App.Tests/PreviewStateTests.cs`

**Interfaces:**
- Produces:
  - `sealed class PreviewState { void Load(IReadOnlyList<Sheet> sheets); int SheetCount; int CurrentIndex; Sheet? Current; string Label; void Next(); void Previous(); }`
  - `sealed partial class MainViewModel : ObservableObject` — `PrintSettings CurrentSettings`, `IAsyncRelayCommand AddFilesCommand`, `IRelayCommand PrintCommand`

`PreviewState` saf mantıktır ve ayrı test edilir; `MainViewModel` WPF'e bağlıdır ve elle doğrulanır.

- [ ] **Step 1: PreviewState testini yaz**

```bash
dotnet new xunit -o tests/KolayYazdir.App.Tests -f net8.0-windows
rm tests/KolayYazdir.App.Tests/UnitTest1.cs
dotnet sln add tests/KolayYazdir.App.Tests
dotnet add tests/KolayYazdir.App.Tests reference src/KolayYazdir.App
```

`tests/KolayYazdir.App.Tests/PreviewStateTests.cs`:

```csharp
using KolayYazdir.App.ViewModels;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.App.Tests;

public class PreviewStateTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    private static IReadOnlyList<Sheet> Sheets(int pageCount, DuplexMode duplex = DuplexMode.Simplex) =>
        LayoutEngine.Build(
            Enumerable.Range(0, pageCount).Select(i => new SourcePageInfo(i, A4)).ToList(),
            new PrintSettings { Duplex = duplex },
            FullBleed);

    [Fact]
    public void A_fresh_state_shows_nothing()
    {
        var state = new PreviewState();

        Assert.Equal(0, state.SheetCount);
        Assert.Null(state.Current);
    }

    [Fact]
    public void Loading_sheets_starts_on_the_first_one()
    {
        var state = new PreviewState();

        state.Load(Sheets(4));

        Assert.Equal(4, state.SheetCount);
        Assert.Equal(0, state.CurrentIndex);
    }

    [Fact]
    public void Next_moves_forward()
    {
        var state = new PreviewState();
        state.Load(Sheets(4));

        state.Next();

        Assert.Equal(1, state.CurrentIndex);
    }

    [Fact]
    public void Next_stops_at_the_last_sheet()
    {
        var state = new PreviewState();
        state.Load(Sheets(2));

        state.Next();
        state.Next();
        state.Next();

        Assert.Equal(1, state.CurrentIndex);
    }

    [Fact]
    public void Previous_stops_at_the_first_sheet()
    {
        var state = new PreviewState();
        state.Load(Sheets(2));

        state.Previous();

        Assert.Equal(0, state.CurrentIndex);
    }

    [Fact]
    public void Simplex_label_counts_sheets()
    {
        var state = new PreviewState();
        state.Load(Sheets(3));
        state.Next();

        Assert.Equal("Yaprak 2 / 3", state.Label);
    }

    [Fact]
    public void Duplex_label_names_the_side()
    {
        var state = new PreviewState();
        state.Load(Sheets(3, DuplexMode.Duplex));

        Assert.Equal("Yaprak 1 / 4 · ön", state.Label);

        state.Next();

        Assert.Equal("Yaprak 2 / 4 · arka", state.Label);
    }

    [Fact]
    public void An_empty_load_clears_the_label()
    {
        var state = new PreviewState();
        state.Load(Sheets(3));

        state.Load([]);

        Assert.Equal(string.Empty, state.Label);
        Assert.Null(state.Current);
    }

    [Fact]
    public void Reloading_a_shorter_job_clamps_the_current_index()
    {
        var state = new PreviewState();
        state.Load(Sheets(5));
        state.Next();
        state.Next();
        state.Next();

        state.Load(Sheets(2));

        Assert.Equal(1, state.CurrentIndex);
        Assert.NotNull(state.Current);
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.App.Tests`
Expected: FAIL — `PreviewState` bulunamıyor

- [ ] **Step 3: PreviewState'i yaz**

`src/KolayYazdir.App/ViewModels/PreviewState.cs`:

```csharp
using KolayYazdir.Core.Models;

namespace KolayYazdir.App.ViewModels;

/// <summary>
/// Önizlemede hangi yaprağın gösterildiğini tutar. WPF'e bağlı olmadığı için
/// gezinme mantığı doğrudan test edilebilir.
/// </summary>
public sealed class PreviewState
{
    private IReadOnlyList<Sheet> _sheets = [];

    public int SheetCount => _sheets.Count;

    public int CurrentIndex { get; private set; }

    public Sheet? Current => CurrentIndex < _sheets.Count ? _sheets[CurrentIndex] : null;

    /// <summary>"Yaprak 2 / 4 · arka" biçiminde gösterge metni.</summary>
    public string Label
    {
        get
        {
            if (Current is not { } sheet) return string.Empty;

            var position = $"Yaprak {CurrentIndex + 1} / {_sheets.Count}";

            // Tek yönlü baskıda "ön" demek gereksiz gürültüdür.
            var hasBacks = _sheets.Any(s => s.Side == SheetSide.Back);
            if (!hasBacks) return position;

            return $"{position} · {(sheet.Side == SheetSide.Front ? "ön" : "arka")}";
        }
    }

    /// <summary>
    /// Yeni yaprak listesini yükler. Ayar değişince liste kısalabilir; bu
    /// durumda görünen yaprak son yaprağa kırpılır, önizleme boşa düşmez.
    /// </summary>
    public void Load(IReadOnlyList<Sheet> sheets)
    {
        _sheets = sheets;
        CurrentIndex = sheets.Count == 0 ? 0 : Math.Min(CurrentIndex, sheets.Count - 1);
    }

    public void Next() => CurrentIndex = Math.Min(CurrentIndex + 1, Math.Max(0, _sheets.Count - 1));

    public void Previous() => CurrentIndex = Math.Max(0, CurrentIndex - 1);
}
```

- [ ] **Step 4: Testlerin geçtiğini doğrula**

Run: `dotnet test tests/KolayYazdir.App.Tests`
Expected: PASS

- [ ] **Step 5: MainViewModel'i yaz**

`src/KolayYazdir.App/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Documents;
using KolayYazdir.Printing;

namespace KolayYazdir.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DocumentLoader _loader = DocumentLoader.Default;
    private readonly PreviewState _preview = new();

    private DocumentSet? _documents;
    private IReadOnlyList<Sheet> _sheets = [];
    private CancellationTokenSource? _previewWork;

    [ObservableProperty] private PaperFormat _paperSize = PaperFormat.A4;
    [ObservableProperty] private Orientation _orientation = Orientation.Portrait;
    [ObservableProperty] private ColorMode _color = ColorMode.Monochrome;
    [ObservableProperty] private DuplexMode _duplex = DuplexMode.Simplex;
    [ObservableProperty] private PagesPerSheet _pagesPerSheet = PagesPerSheet.One;
    [ObservableProperty] private bool _fitToPage;
    [ObservableProperty] private bool _autoRotate = true;
    [ObservableProperty] private string _pageRange = string.Empty;
    [ObservableProperty] private int _copies = 1;
    [ObservableProperty] private MediaType? _mediaType;

    [ObservableProperty] private BitmapSource? _previewImage;
    [ObservableProperty] private string _sheetLabel = string.Empty;
    [ObservableProperty] private string _jobSummary = string.Empty;
    [ObservableProperty] private string _printerStatus = "yazıcı aranıyor…";
    [ObservableProperty] private bool _printerIsHealthy = true;
    [ObservableProperty] private string _bindingHint = string.Empty;

    public ObservableCollection<FileEntry> Files { get; } = [];

    public ObservableCollection<MediaType> MediaTypes { get; } = [];

    public PrinterCapabilities? Capabilities { get; private set; }

    public PrintSettings CurrentSettings => new()
    {
        Paper = PaperSize,
        Orientation = Orientation,
        Color = Color,
        Duplex = Duplex,
        PagesPerSheet = PagesPerSheet,
        FitToPage = FitToPage,
        AutoRotate = AutoRotate,
        PageRange = PageRange,
        Copies = Copies,
        MediaTypeId = MediaType?.Id
    };

    /// <summary>Herhangi bir ayar değiştiğinde yerleşimi ve önizlemeyi tazeler.</summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(PreviewImage) or nameof(SheetLabel) or nameof(JobSummary)
            or nameof(PrinterStatus) or nameof(PrinterIsHealthy) or nameof(BindingHint)) return;

        if (e.PropertyName is nameof(PaperSize) or nameof(Orientation)) RefreshCapabilities();
        UpdateBindingHint();
        Rebuild();
    }

    private void UpdateBindingHint() =>
        BindingHint = Duplex == DuplexMode.Simplex
            ? string.Empty
            : CurrentSettings.Binding == DuplexBinding.LongEdge
                ? "uzun kenardan çevir (dikey)"
                : "kısa kenardan çevir (yatay)";

    public void RefreshCapabilities()
    {
        var name = PrinterCapabilities.DefaultPrinterName;
        if (name is null)
        {
            Capabilities = null;
            PrinterIsHealthy = false;
            PrinterStatus = "yazıcı bulunamadı";
            return;
        }

        Capabilities = PrinterCapabilities.Read(name, PaperSize, Orientation);
        PrinterIsHealthy = Capabilities is not null;
        PrinterStatus = Capabilities is null ? $"{name} · ulaşılamıyor" : $"{name} · hazır";

        if (Capabilities is null) return;

        if (MediaTypes.Count == 0)
        {
            foreach (var media in Capabilities.MediaTypes) MediaTypes.Add(media);
        }

        // MediaType bir struct olduğu için FirstOrDefault boş listede (0, null)
        // döner; seçimi ancak liste doluyken yapıyoruz.
        if (MediaType is null && MediaTypes.Count > 0)
        {
            MediaType = MediaTypes[0];
        }
    }

    /// <summary>Yaprakları yeniden hesaplar ve görünen yaprağı çizer.</summary>
    public void Rebuild()
    {
        if (_documents is null || _documents.Pages.Count == 0)
        {
            _sheets = [];
            _preview.Load([]);
            PreviewImage = null;
            SheetLabel = string.Empty;
            JobSummary = string.Empty;
            return;
        }

        var printable = Capabilities?.PrintableArea
            ?? FullBleed(Paper.SizeOf(PaperSize, Orientation));

        _sheets = LayoutEngine.Build(_documents.Pages, CurrentSettings, printable);
        _preview.Load(_sheets);

        SheetLabel = _preview.Label;
        JobSummary = $"{LeafCount(_sheets)} yaprak · {_documents.Pages.Count} sayfa";
        DrawCurrentSheet();
    }

    private static RectPt FullBleed(SizePt paper) => new(0, 0, paper.Width, paper.Height);

    private static int LeafCount(IReadOnlyList<Sheet> sheets) =>
        sheets.Count == 0 ? 0 : sheets[^1].Index + 1;

    /// <summary>
    /// Görünen yaprağı arka planda çizer. 35'li yerleşimde tek yaprak otuz beş
    /// sayfa render etmek demektir; bunu arayüz iş parçacığında yapmak pencereyi
    /// dondururdu. Bir önceki çizim hâlâ sürüyorsa iptal edilir, böylece hızlı
    /// ayar değişikliklerinde sadece son istek tamamlanır.
    /// </summary>
    private async void DrawCurrentSheet()
    {
        if (_documents is null || _preview.Current is not { } sheet)
        {
            PreviewImage = null;
            return;
        }

        _previewWork?.Cancel();
        _previewWork?.Dispose();
        _previewWork = new CancellationTokenSource();
        var token = _previewWork.Token;

        var documents = _documents;
        var color = Color;

        try
        {
            var image = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var renderer = new SheetRenderer(documents);
                using var bitmap = renderer.RenderToBitmap(sheet, RenderConstants.PreviewDpi, color);

                token.ThrowIfCancellationRequested();

                // Dondurulmuş kaynak arayüz iş parçacığına güvenle geçer.
                return BitmapConverter.ToBitmapSource(bitmap);
            }, token);

            if (!token.IsCancellationRequested) PreviewImage = image;
        }
        catch (OperationCanceledException)
        {
            // Daha yeni bir çizim isteği geldi; bu sonucu atıyoruz.
        }
        catch (Exception)
        {
            // Bozuk bir sayfa önizlemeyi çökertmemeli; alan boş kalır.
            if (!token.IsCancellationRequested) PreviewImage = null;
        }
    }

    [RelayCommand]
    private void NextSheet()
    {
        _preview.Next();
        SheetLabel = _preview.Label;
        DrawCurrentSheet();
    }

    [RelayCommand]
    private void PreviousSheet()
    {
        _preview.Previous();
        SheetLabel = _preview.Label;
        DrawCurrentSheet();
    }

    /// <summary>Yüklü belgeleri değiştirir; dosya listesi görünümü çağırır.</summary>
    public async Task ReloadDocumentsAsync(CancellationToken ct)
    {
        _documents?.Dispose();

        var loaded = new List<SourceDocument>();
        foreach (var entry in Files)
        {
            entry.Error = null;
            entry.IsLoading = true;
            try
            {
                // PDF ve görseller anında açılır; Word/Excel dosyaları dış bir
                // sürece gidip gelir, o sırada satırda "çevriliyor…" görünür.
                var document = await _loader.LoadAsync(entry.Path, ct);
                entry.PageCount = document.PageCount;
                loaded.Add(document);
            }
            catch (DocumentLoadException ex)
            {
                entry.Error = ex.Message;
            }
            finally
            {
                entry.IsLoading = false;
            }
        }

        _documents = new DocumentSet(loaded);
        Rebuild();
    }

    /// <summary>Sürücü kopyalamayı üstlenmiyorsa yaprakları burada çoğaltırız.</summary>
    public bool DriverHandlesCopies => Capabilities?.SupportsMultipleCopies ?? false;

    public IReadOnlyList<Sheet> SheetsForPrinting() =>
        DriverHandlesCopies ? _sheets : LayoutEngine.Repeat(_sheets, Copies);

    public void Dispose()
    {
        _previewWork?.Cancel();
        _previewWork?.Dispose();
        _documents?.Dispose();
    }
}
```

`src/KolayYazdir.App/ViewModels/BitmapConverter.cs`:

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KolayYazdir.App.ViewModels;

/// <summary>GDI+ bitmap'ini WPF'in gösterebileceği biçime çevirir.</summary>
public static class BitmapConverter
{
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var source = BitmapSource.Create(
                bitmap.Width, bitmap.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgra32, null,
                data.Scan0, data.Stride * bitmap.Height, data.Stride);

            // Kaynağı dondurmak, arka planda üretilip arayüz iş parçacığında
            // gösterilmesini güvenli kılar.
            source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
```

- [ ] **Step 6: Pencereyi görünüm modeline bağla**

`src/KolayYazdir.App/MainWindow.xaml.cs` içinde `DataContext`'i kur ve segment düğmelerini doldur:

```csharp
using System.Windows;
using KolayYazdir.App.ViewModels;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        PaperSelector.ItemsSource = new[] { PaperFormat.A4, PaperFormat.A5, PaperFormat.A3 };
        OrientationSelector.ItemsSource = new[] { Orientation.Portrait, Orientation.Landscape };
        ColorSelector.ItemsSource = new[] { ColorMode.Color, ColorMode.Monochrome };
        DuplexSelector.ItemsSource = new[] { DuplexMode.Simplex, DuplexMode.Duplex };
        NUpSelector.ItemsSource = new[]
        {
            PagesPerSheet.One, PagesPerSheet.Two, PagesPerSheet.Four,
            PagesPerSheet.Nine, PagesPerSheet.Sixteen, PagesPerSheet.ThirtyFive
        };

        _viewModel.RefreshCapabilities();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
```

`MainWindow.xaml` içindeki denetimlere bağlamaları ekle:

```xml
<controls:SegmentedControl x:Name="PaperSelector" SelectedValue="{Binding PaperSize, Mode=TwoWay}"/>
<controls:SegmentedControl x:Name="OrientationSelector" SelectedValue="{Binding Orientation, Mode=TwoWay}"/>
<controls:SegmentedControl x:Name="ColorSelector" SelectedValue="{Binding Color, Mode=TwoWay}"/>
<controls:SegmentedControl x:Name="DuplexSelector" SelectedValue="{Binding Duplex, Mode=TwoWay}"/>
<controls:SegmentedControl x:Name="NUpSelector" SelectedValue="{Binding PagesPerSheet, Mode=TwoWay}"/>
```

ve şu bağlamaları ilgili denetimlere ekle:

```xml
<TextBlock x:Name="PrinterStatus" Text="{Binding PrinterStatus}"/>
<TextBlock x:Name="BindingHint" Text="{Binding BindingHint}"/>
<ComboBox x:Name="MediaTypeSelector" ItemsSource="{Binding MediaTypes}"
          SelectedItem="{Binding MediaType, Mode=TwoWay}" DisplayMemberPath="Name"/>
<CheckBox x:Name="FitToPage" IsChecked="{Binding FitToPage, Mode=TwoWay}"/>
<CheckBox x:Name="AutoRotate" IsChecked="{Binding AutoRotate, Mode=TwoWay}"/>
<TextBox x:Name="PageRange" Text="{Binding PageRange, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
<TextBox x:Name="Copies" Text="{Binding Copies, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
<Image x:Name="PreviewImage" Source="{Binding PreviewImage}"/>
<TextBlock x:Name="SheetLabel" Text="{Binding SheetLabel}"/>
<TextBlock x:Name="JobSummary" Text="{Binding JobSummary}"/>
<Button x:Name="PreviousSheet" Command="{Binding PreviousSheetCommand}"/>
<Button x:Name="NextSheet" Command="{Binding NextSheetCommand}"/>
```

Boş durumun görünürlüğü için `EmptyState`'e ekle:

```xml
<StackPanel x:Name="EmptyState"
            Visibility="{Binding PreviewImage, Converter={StaticResource NullToVisible}}">
```

ve `Theme/Dark.xaml`'a dönüştürücüyü ekle:

```xml
<local:NullToVisibilityConverter x:Key="NullToVisible"
    xmlns:local="clr-namespace:KolayYazdir.App.ViewModels"/>
```

`src/KolayYazdir.App/ViewModels/NullToVisibilityConverter.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KolayYazdir.App.ViewModels;

/// <summary>Değer null ise görünür, doluysa gizli.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 7: Elle doğrula**

Run: `dotnet run --project src/KolayYazdir.App`

- Kağıt boyutu düğmeleri tıklanınca seçim beyaza dönüyor
- "Önlü arkalı" seçilince altında "uzun kenardan çevir (dikey)" yazısı beliriyor
- Yön "Yatay" yapılınca yazı "kısa kenardan çevir (yatay)" oluyor
- Kağıt cinsi açılır kutusunda yazıcının kendi isimleri ("Düz", "Kalın 1") görünüyor
- Başlıkta yazıcı adı ve "hazır" yazıyor

- [ ] **Step 8: Commit**

```bash
git add src/KolayYazdir.App tests/KolayYazdir.App.Tests KolayYazdir.sln
git commit -m "Ana görünüm modeli ve canlı önizleme"
```

---

### Task 19: Dosya listesi, sürükle bırak ve hata gösterimi

**Files:**
- Create: `src/KolayYazdir.App/ViewModels/FileEntry.cs`
- Modify: `src/KolayYazdir.App/MainWindow.xaml`, `MainWindow.xaml.cs`
- Modify: `src/KolayYazdir.App/ViewModels/MainViewModel.cs`
- Test: `tests/KolayYazdir.App.Tests/FileEntryTests.cs`

**Interfaces:**
- Produces: `sealed partial class FileEntry : ObservableObject { string Path; string FileName; int PageCount; string? Error; bool HasError; string PageLabel; }`
- Produces: `MainViewModel.AddFilesCommand`, `MainViewModel.RemoveFile(FileEntry)`, `MainViewModel.MoveFile(int from, int to)`

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.App.Tests/FileEntryTests.cs`:

```csharp
using KolayYazdir.App.ViewModels;

namespace KolayYazdir.App.Tests;

public class FileEntryTests
{
    [Fact]
    public void The_display_name_is_the_file_name_only()
    {
        var entry = new FileEntry(@"C:\Users\arda\Downloads\kitapcik.pdf");

        Assert.Equal("kitapcik.pdf", entry.FileName);
    }

    [Fact]
    public void A_fresh_entry_has_no_error()
    {
        Assert.False(new FileEntry(@"C:\a.pdf").HasError);
    }

    [Fact]
    public void Setting_an_error_raises_the_error_flag()
    {
        var entry = new FileEntry(@"C:\a.pdf") { Error = "Dosya bozuk" };

        Assert.True(entry.HasError);
    }

    [Fact]
    public void Page_label_counts_pages()
    {
        var entry = new FileEntry(@"C:\a.pdf") { PageCount = 12 };

        Assert.Equal("12 sf", entry.PageLabel);
    }

    [Fact]
    public void Page_label_shows_the_error_instead_when_loading_failed()
    {
        var entry = new FileEntry(@"C:\a.pdf") { PageCount = 0, Error = "Dosya bozuk" };

        Assert.Equal("Dosya bozuk", entry.PageLabel);
    }

    [Fact]
    public void Page_label_is_blank_before_loading()
    {
        Assert.Equal(string.Empty, new FileEntry(@"C:\a.pdf").PageLabel);
    }

    [Fact]
    public void Page_label_reports_progress_while_loading()
    {
        var entry = new FileEntry(@"C:\a.docx") { IsLoading = true };

        Assert.Equal("çevriliyor…", entry.PageLabel);
    }

    [Fact]
    public void Progress_outranks_a_stale_page_count()
    {
        var entry = new FileEntry(@"C:\a.docx") { PageCount = 3, IsLoading = true };

        Assert.Equal("çevriliyor…", entry.PageLabel);
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.App.Tests --filter FileEntryTests`
Expected: FAIL — `FileEntry` bulunamıyor

- [ ] **Step 3: FileEntry'yi yaz**

`src/KolayYazdir.App/ViewModels/FileEntry.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace KolayYazdir.App.ViewModels;

/// <summary>Dosya listesindeki bir satır.</summary>
public sealed partial class FileEntry(string path) : ObservableObject
{
    public string Path { get; } = path;

    public string FileName { get; } = System.IO.Path.GetFileName(path);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    private int _pageCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    private string? _error;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    private bool _isLoading;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>
    /// Satırın sağındaki küçük yazı. Word/Excel dönüşümü uzun sürebildiği için
    /// beklerken kullanıcıya ne olduğunu söylüyoruz.
    /// </summary>
    public string PageLabel => IsLoading
        ? "çevriliyor…"
        : HasError ? Error! : PageCount > 0 ? $"{PageCount} sf" : string.Empty;
}
```

- [ ] **Step 4: Görünüm modeline dosya komutlarını ekle**

`MainViewModel` içine ekle:

```csharp
    /// <summary>Dosya seçme penceresinin açılacağı klasör; ayarlardan gelir.</summary>
    public string DefaultFolder { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";

    /// <summary>Görünüm dosya seçtirir, bu metot yükler.</summary>
    public async Task AddFilesAsync(IEnumerable<string> paths, CancellationToken ct)
    {
        var added = false;
        foreach (var path in paths.Where(DocumentLoader.IsSupported))
        {
            if (Files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))) continue;

            Files.Add(new FileEntry(path));
            added = true;
        }

        if (added) await ReloadDocumentsAsync(ct);
    }

    public async Task RemoveFileAsync(FileEntry entry, CancellationToken ct)
    {
        if (!Files.Remove(entry)) return;
        await ReloadDocumentsAsync(ct);
    }

    /// <summary>Listedeki bir dosyayı başka bir sıraya taşır.</summary>
    public async Task MoveFileAsync(int from, int to, CancellationToken ct)
    {
        if (from == to || from < 0 || from >= Files.Count) return;

        to = Math.Clamp(to, 0, Files.Count - 1);
        Files.Move(from, to);
        await ReloadDocumentsAsync(ct);
    }
```

- [ ] **Step 5: Dosya listesi görünümünü yaz**

`MainWindow.xaml` içindeki `FileList`'i şununla değiştir:

```xml
<ListBox x:Name="FileList" Background="Transparent" BorderThickness="0"
         ItemsSource="{Binding Files}" AllowDrop="True"
         ScrollViewer.HorizontalScrollBarVisibility="Disabled">
  <ListBox.ItemContainerStyle>
    <Style TargetType="ListBoxItem">
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="Padding" Value="2"/>
      <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
    </Style>
  </ListBox.ItemContainerStyle>
  <ListBox.ItemTemplate>
    <DataTemplate>
      <DockPanel>
        <Button DockPanel.Dock="Right" Content="✕" Width="20" BorderThickness="0"
                Background="Transparent" Foreground="{StaticResource SecondaryText}"
                Click="RemoveFile_Click" Tag="{Binding}"/>
        <TextBlock DockPanel.Dock="Right" Text="{Binding PageLabel}" FontSize="11"
                   Margin="6,0" VerticalAlignment="Center">
          <TextBlock.Style>
            <Style TargetType="TextBlock">
              <Setter Property="Foreground" Value="{StaticResource SecondaryText}"/>
              <Style.Triggers>
                <DataTrigger Binding="{Binding HasError}" Value="True">
                  <Setter Property="Foreground" Value="{StaticResource Danger}"/>
                </DataTrigger>
              </Style.Triggers>
            </Style>
          </TextBlock.Style>
        </TextBlock>
        <TextBlock Text="{Binding FileName}" FontSize="12" TextTrimming="CharacterEllipsis"
                   VerticalAlignment="Center">
          <TextBlock.Style>
            <Style TargetType="TextBlock">
              <Setter Property="Foreground" Value="#E8E8E8"/>
              <Style.Triggers>
                <DataTrigger Binding="{Binding HasError}" Value="True">
                  <Setter Property="Foreground" Value="{StaticResource Danger}"/>
                </DataTrigger>
              </Style.Triggers>
            </Style>
          </TextBlock.Style>
        </TextBlock>
      </DockPanel>
    </DataTemplate>
  </ListBox.ItemTemplate>
</ListBox>
```

`MainWindow.xaml.cs` içine dosya seçme, sürükle bırak ve silme kodunu ekle:

```csharp
    private async void PickFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = DocumentLoader.FileDialogFilter,
            InitialDirectory = Directory.Exists(_viewModel.DefaultFolder) ? _viewModel.DefaultFolder : null
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.AddFilesAsync(dialog.FileNames, CancellationToken.None);
        }
    }

    private async void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: FileEntry entry })
        {
            await _viewModel.RemoveFileAsync(entry, CancellationToken.None);
        }
    }

    private void FileList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void FileList_Drop(object sender, DragEventArgs e)
    {
        // Listenin içinden gelen sürükleme sıralamadır, dışarıdan gelen ekleme.
        if (e.Data.GetDataPresent(typeof(FileEntry)))
        {
            await HandleReorderDrop(e);
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await _viewModel.AddFilesAsync(paths, CancellationToken.None);
        }
    }
```

Sıralama için sürükleme başlatma ve bırakma kodunu ekle:

```csharp
    private Point _dragOrigin;

    private void FileList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _dragOrigin = e.GetPosition(null);

    private void FileList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        // Küçük titremeleri sürükleme sanmayalım; sistem eşiğini bekliyoruz.
        var moved = e.GetPosition(null) - _dragOrigin;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (FindEntryUnder(e.OriginalSource) is not { } entry) return;

        DragDrop.DoDragDrop(FileList, new DataObject(typeof(FileEntry), entry), DragDropEffects.Move);
    }

    private async Task HandleReorderDrop(DragEventArgs e)
    {
        if (e.Data.GetData(typeof(FileEntry)) is not FileEntry dragged) return;

        var from = _viewModel.Files.IndexOf(dragged);
        var target = FindEntryUnder(e.OriginalSource);

        // Boşluğa bırakmak "en sona taşı" demektir.
        var to = target is null ? _viewModel.Files.Count - 1 : _viewModel.Files.IndexOf(target);

        await _viewModel.MoveFileAsync(from, to, CancellationToken.None);
    }

    /// <summary>Fareyi altındaki liste satırının verisine çevirir.</summary>
    private static FileEntry? FindEntryUnder(object? source)
    {
        var element = source as DependencyObject;
        while (element is not null and not ListBoxItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        return (element as ListBoxItem)?.DataContext as FileEntry;
    }
```

`FileList`'e olayları bağla:

```xml
DragOver="FileList_DragOver" Drop="FileList_Drop"
PreviewMouseLeftButtonDown="FileList_PreviewMouseLeftButtonDown"
PreviewMouseMove="FileList_PreviewMouseMove"
```

Her iki "Dosya seç" düğmesine `Click="PickFiles_Click"` ekle.
Pencerenin kendisine de `AllowDrop="True" DragOver="FileList_DragOver" Drop="FileList_Drop"` ekle — kullanıcı pencerenin herhangi bir yerine bırakabilsin.

Dosyanın başına gereken `using` satırları:

```csharp
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KolayYazdir.App.ViewModels;
using KolayYazdir.Documents;
```

- [ ] **Step 6: Testlerin geçtiğini doğrula ve elle dene**

Run: `dotnet test tests/KolayYazdir.App.Tests`
Expected: PASS

Run: `dotnet run --project src/KolayYazdir.App`

- İki PDF ve bir fotoğrafı pencereye sürükle bırak; listede sayfa sayılarıyla görünsünler
- Önizleme dolsun, alttaki özet "N yaprak · M sayfa" desin
- Bozuk bir dosyayı (uzantısı .pdf ama içi metin) ekle; satır kırmızıya dönsün, diğerleri basılabilir kalsın
- ✕ ile bir dosyayı çıkar; önizleme güncellensin
- Listede bir dosyayı tutup başka bir satırın üzerine sürükle; sıra değişsin ve önizleme yeni sıraya göre yenilensin
- Bir Word dosyası ekle; dönüşüm sürerken satırda "çevriliyor…" görünsün, bu sırada pencere donmasın

- [ ] **Step 7: Commit**

```bash
git add src/KolayYazdir.App tests/KolayYazdir.App.Tests
git commit -m "Dosya listesi, sürükle bırak ve hata gösterimi"
```

---

### Task 20: Yazdırma akışı ve ayarların hatırlanması

**Files:**
- Create: `src/KolayYazdir.App/Services/SettingsStore.cs`
- Create: `src/KolayYazdir.App/Services/StoredSettings.cs`
- Modify: `src/KolayYazdir.App/ViewModels/MainViewModel.cs`, `MainWindow.xaml.cs`
- Test: `tests/KolayYazdir.App.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `sealed record StoredSettings { string? DefaultFolder; PaperFormat Paper; Orientation Orientation; ColorMode Color; DuplexMode Duplex; PagesPerSheet PagesPerSheet; bool FitToPage; bool AutoRotate; int Copies; int? MediaTypeId; }`
  - `sealed class SettingsStore(string filePath) { static SettingsStore Default; StoredSettings Load(); void Save(StoredSettings settings); }`
  - `MainViewModel.PrintCommand`

- [ ] **Step 1: Başarısız testi yaz**

`tests/KolayYazdir.App.Tests/SettingsStoreTests.cs`:

```csharp
using KolayYazdir.App.Services;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.App.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kolayyazdir-settings").FullName;

    private SettingsStore Store() => new(Path.Combine(_root, "ayarlar.json"));

    [Fact]
    public void A_missing_file_yields_the_defaults()
    {
        var settings = Store().Load();

        Assert.Equal(PaperFormat.A4, settings.Paper);
        Assert.Equal(ColorMode.Monochrome, settings.Color);
        Assert.False(settings.FitToPage);
        Assert.True(settings.AutoRotate);
        Assert.Equal(1, settings.Copies);
    }

    [Fact]
    public void Saved_settings_come_back()
    {
        var store = Store();
        store.Save(new StoredSettings
        {
            Paper = PaperFormat.A3,
            Orientation = Orientation.Landscape,
            PagesPerSheet = PagesPerSheet.Nine,
            FitToPage = true,
            Copies = 5,
            MediaTypeId = 3,
            DefaultFolder = @"D:\Islerim"
        });

        var loaded = Store().Load();

        Assert.Equal(PaperFormat.A3, loaded.Paper);
        Assert.Equal(Orientation.Landscape, loaded.Orientation);
        Assert.Equal(PagesPerSheet.Nine, loaded.PagesPerSheet);
        Assert.True(loaded.FitToPage);
        Assert.Equal(5, loaded.Copies);
        Assert.Equal(3, loaded.MediaTypeId);
        Assert.Equal(@"D:\Islerim", loaded.DefaultFolder);
    }

    [Fact]
    public void A_corrupt_file_falls_back_to_the_defaults()
    {
        File.WriteAllText(Path.Combine(_root, "ayarlar.json"), "{ bu json değil");

        Assert.Equal(PaperFormat.A4, Store().Load().Paper);
    }

    [Fact]
    public void Saving_creates_the_folder_when_it_is_missing()
    {
        var nested = new SettingsStore(Path.Combine(_root, "yeni", "klasor", "ayarlar.json"));

        nested.Save(new StoredSettings { Copies = 2 });

        Assert.Equal(2, nested.Load().Copies);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `dotnet test tests/KolayYazdir.App.Tests --filter SettingsStoreTests`
Expected: FAIL — `SettingsStore` bulunamıyor

- [ ] **Step 3: Ayar deposunu yaz**

`src/KolayYazdir.App/Services/StoredSettings.cs`:

```csharp
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.App.Services;

/// <summary>Uygulama kapanırken saklanan, açılırken geri yüklenen ayarlar.</summary>
public sealed record StoredSettings
{
    public string? DefaultFolder { get; init; }
    public PaperFormat Paper { get; init; } = PaperFormat.A4;
    public Orientation Orientation { get; init; } = Orientation.Portrait;
    public ColorMode Color { get; init; } = ColorMode.Monochrome;
    public DuplexMode Duplex { get; init; } = DuplexMode.Simplex;
    public PagesPerSheet PagesPerSheet { get; init; } = PagesPerSheet.One;
    public bool FitToPage { get; init; }
    public bool AutoRotate { get; init; } = true;
    public int Copies { get; init; } = 1;
    public int? MediaTypeId { get; init; }
}
```

`src/KolayYazdir.App/Services/SettingsStore.cs`:

```csharp
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KolayYazdir.App.Services;

/// <summary>
/// Ayarları kullanıcının profilinde bir JSON dosyasında tutar. Dosya
/// bozulursa varsayılanlara dönülür — kullanıcıya hata gösterip yolunu
/// kesmenin bir faydası yok.
/// </summary>
public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static SettingsStore Default => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KolayYazdir",
        "ayarlar.json"));

    public StoredSettings Load()
    {
        try
        {
            if (!File.Exists(filePath)) return new StoredSettings();

            return JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(filePath), Options)
                ?? new StoredSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new StoredSettings();
        }
    }

    public void Save(StoredSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ayar saklanamadıysa uygulama yine çalışır; sessizce geçiyoruz.
        }
    }
}
```

- [ ] **Step 4: Yazdırma komutunu ve ayar yüklemeyi bağla**

`MainViewModel` içine ekle:

```csharp
    private readonly SettingsStore _settingsStore = SettingsStore.Default;

    /// <summary>Uygulama açılışında son kullanılan ayarları geri yükler.</summary>
    public void RestoreSettings()
    {
        var stored = _settingsStore.Load();

        if (!string.IsNullOrWhiteSpace(stored.DefaultFolder)) DefaultFolder = stored.DefaultFolder;
        PaperSize = stored.Paper;
        Orientation = stored.Orientation;
        Color = stored.Color;
        Duplex = stored.Duplex;
        PagesPerSheet = stored.PagesPerSheet;
        FitToPage = stored.FitToPage;
        AutoRotate = stored.AutoRotate;
        Copies = stored.Copies;

        RefreshCapabilities();

        if (stored.MediaTypeId is { } id && MediaTypes.Any(m => m.Id == id))
        {
            MediaType = MediaTypes.First(m => m.Id == id);
        }
    }

    public void PersistSettings() => _settingsStore.Save(new StoredSettings
    {
        DefaultFolder = DefaultFolder,
        Paper = PaperSize,
        Orientation = Orientation,
        Color = Color,
        Duplex = Duplex,
        PagesPerSheet = PagesPerSheet,
        FitToPage = FitToPage,
        AutoRotate = AutoRotate,
        Copies = Copies,
        MediaTypeId = MediaType?.Id
    });

    /// <summary>
    /// Yazdırır. Dosya seçilmemişse hata vermez — çağıran görünüm dosya
    /// seçme penceresini açar, kullanıcı doğru yere yönlendirilmiş olur.
    /// </summary>
    public PrintOutcome Print()
    {
        if (_documents is null || _sheets.Count == 0) return PrintOutcome.NothingToPrint;

        var printerName = PrinterCapabilities.DefaultPrinterName;
        if (printerName is null) return PrintOutcome.NoPrinter;

        var renderer = new SheetRenderer(_documents);
        var runner = new PrintJobRunner(renderer);
        var settings = CurrentSettings;

        // Otomatik dupleks yoksa iki geçişte basarız; ikinci geçiş için
        // kullanıcıdan kağıtları çevirmesi istenir.
        var needsManualDuplex = settings.Duplex == DuplexMode.Duplex
            && Capabilities is { SupportsAutomaticDuplex: false };

        if (!needsManualDuplex)
        {
            runner.Run(SheetsForPrinting(), settings, printerName, DriverHandlesCopies);
            return PrintOutcome.Done;
        }

        // İki geçişli baskıda kopyayı sürücüye bırakamayız: sürücü her geçişi
        // ayrı ayrı çoğaltır ve deste sırası bozulur. Yaprakları kendimiz
        // çoğaltıp sürücüye tek kopya söylüyoruz.
        var plan = ManualDuplexPlan.Split(LayoutEngine.Repeat(_sheets, Copies));
        var simplex = settings with { Duplex = DuplexMode.Simplex, Copies = 1 };

        runner.Run(plan.FirstPass, simplex, printerName, driverHandlesCopies: false);
        PendingSecondPass = () =>
            runner.Run(plan.SecondPass, simplex, printerName, driverHandlesCopies: false);

        return PrintOutcome.NeedsPaperFlip;
    }

    /// <summary>Elle önlü arkalıda ikinci geçişi çalıştıran eylem.</summary>
    public Action? PendingSecondPass { get; private set; }
```

`src/KolayYazdir.App/ViewModels/PrintOutcome.cs`:

```csharp
namespace KolayYazdir.App.ViewModels;

public enum PrintOutcome
{
    Done,
    NothingToPrint,
    NoPrinter,
    NeedsPaperFlip
}
```

`MainWindow.xaml.cs` içinde yazdırma düğmesini bağla:

```csharp
    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        switch (_viewModel.Print())
        {
            case PrintOutcome.NothingToPrint:
                PickFiles_Click(sender, e);
                break;

            case PrintOutcome.NoPrinter:
                MessageBox.Show(this, "Yazıcı bulunamadı. Yazıcının açık ve bağlı olduğundan emin ol.",
                    "Kolay Yazdır", MessageBoxButton.OK, MessageBoxImage.Warning);
                break;

            case PrintOutcome.NeedsPaperFlip:
                var answer = MessageBox.Show(this,
                    "Ön yüzler basıldı.\n\nKağıtları çıkarıp ters çevir ve aynı sırayla tepsiye koy, " +
                    "sonra Tamam'a bas.",
                    "Önlü arkalı", MessageBoxButton.OKCancel, MessageBoxImage.Information);

                if (answer == MessageBoxResult.OK) _viewModel.PendingSecondPass?.Invoke();
                break;
        }

        await Task.CompletedTask;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.PersistSettings();
        base.OnClosing(e);
    }
```

`MainWindow` kurucusunda `_viewModel.RefreshCapabilities()` çağrısını `_viewModel.RestoreSettings()` ile değiştir (o da yetenekleri tazeliyor).

`PrintButton`'a `Click="Print_Click"` ekle.

- [ ] **Step 5: Varsayılan klasör ve yazıcı uyarısını bağla**

Spec varsayılan klasörün değiştirilebilir olmasını istiyor. Dosya seçme
penceresinin en son gezilen klasörü hatırlaması en sade çözüm: kullanıcı
başka bir klasörden dosya seçtiğinde orası yeni varsayılan olur.

`MainViewModel.AddFilesAsync` içine, dosyalar eklendikten sonra ekle:

```csharp
        // Kullanıcı başka bir klasöre gittiyse bir dahaki sefere oradan başla.
        if (added && Path.GetDirectoryName(paths.First()) is { Length: > 0 } folder)
        {
            DefaultFolder = folder;
        }
```

`paths` birden çok kez gezildiği için imzayı listeye çevir:

```csharp
    public async Task AddFilesAsync(IEnumerable<string> paths, CancellationToken ct)
    {
        var incoming = paths.Where(DocumentLoader.IsSupported).ToList();
```

ve döngüyü `incoming` üzerinde çalıştır, klasör kontrolünde `incoming` kullan.

Yazıcı uyarısı için `PrinterStatus` metnine renk tetikleyicisi ekle:

```xml
<TextBlock x:Name="PrinterStatus" DockPanel.Dock="Right" HorizontalAlignment="Right"
           Text="{Binding PrinterStatus}" FontSize="12">
  <TextBlock.Style>
    <Style TargetType="TextBlock">
      <Setter Property="Foreground" Value="{StaticResource SecondaryText}"/>
      <Style.Triggers>
        <DataTrigger Binding="{Binding PrinterIsHealthy}" Value="False">
          <Setter Property="Foreground" Value="{StaticResource Danger}"/>
        </DataTrigger>
      </Style.Triggers>
    </Style>
  </TextBlock.Style>
</TextBlock>
```

- [ ] **Step 6: Testlerin geçtiğini doğrula ve uçtan uca dene**

Run: `dotnet test`
Expected: PASS — tüm projeler

Run: `dotnet run --project src/KolayYazdir.App`

- Bir PDF seç, "Microsoft Print to PDF" varsayılan yazıcıyken bas; dosya üretildiğini gör
- Uygulamayı kapat, yeniden aç; kağıt boyutu, yerleşim ve kopya seçimlerinin hatırlandığını gör

- [ ] **Step 7: Commit**

```bash
git add src/KolayYazdir.App tests/KolayYazdir.App.Tests
git commit -m "Yazdırma akışı ve ayarların hatırlanması"
```

---

### Task 21: Otomatik güncelleme ve GitHub sürüm akışı

Son parça: uygulamanın kendini güncellemesi ve sürüm paketinin GitHub'da üretilmesi.

**Files:**
- Create: `src/KolayYazdir.App/Services/UpdateService.cs`
- Modify: `src/KolayYazdir.App/App.xaml.cs`
- Create: `.github/workflows/release.yml`
- Create: `README.md`

**Interfaces:**
- Produces: `sealed class UpdateService(string repositoryUrl) { Task CheckInBackgroundAsync(); }`

- [ ] **Step 1: Velopack'i ekle**

```bash
dotnet add src/KolayYazdir.App package Velopack --version 1.2.0
dotnet tool install -g vpk
```

- [ ] **Step 2: Güncelleme servisini yaz**

`src/KolayYazdir.App/Services/UpdateService.cs`:

```csharp
using Velopack;
using Velopack.Sources;

namespace KolayYazdir.App.Services;

/// <summary>
/// Açılışta arka planda yeni sürüme bakar, varsa indirir ve uygulamadan
/// çıkıldığında uygular. Hiçbir aşamada kullanıcıya sorulmaz ve hiçbir hata
/// kullanıcıya gösterilmez — kırtasiyede çalışan biri güncelleme penceresiyle
/// uğraşmamalı.
/// </summary>
public sealed class UpdateService(string repositoryUrl)
{
    /// <summary>
    /// Depo adresi. Step 5'te GitHub deposu oluşturulunca gerçek adresle
    /// değiştirilir; o adıma kadar güncelleme kontrolü sessizce başarısız olur
    /// ve uygulamanın çalışmasını etkilemez.
    /// </summary>
    public const string RepositoryUrl = "https://github.com/KULLANICI/kolay-yazdir";

    public async Task CheckInBackgroundAsync()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(repositoryUrl, accessToken: null, prerelease: false));

            // Geliştirme sırasında uygulama kurulu olmadığı için bu false döner.
            if (!manager.IsInstalled) return;

            var update = await manager.CheckForUpdatesAsync();
            if (update is null) return;

            await manager.DownloadUpdatesAsync(update);
            manager.WaitExitThenApplyUpdates(update);
        }
        catch (Exception)
        {
            // Ağ yoksa, GitHub ulaşılamıyorsa veya paket bozuksa sessizce geç.
            // Güncelleme, yazdırma işinin önüne asla geçmemeli.
        }
    }
}
```

`src/KolayYazdir.App/App.xaml.cs`:

```csharp
using System.Windows;
using KolayYazdir.App.Services;
using Velopack;

namespace KolayYazdir.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Velopack kurulum, güncelleme ve kaldırma kancalarını devralır.
        // Uygulamanın ilk işi olmak zorunda: kurulum sırasında çalıştırıldığında
        // pencere hiç açılmadan işini yapıp süreçten çıkar.
        VelopackApp.Build().Run();

        base.OnStartup(e);

        // Güncelleme kontrolü beklenmez; yazdırma işinin önüne geçmemeli.
        _ = new UpdateService(UpdateService.RepositoryUrl).CheckInBackgroundAsync();
    }
}
```

> WPF'in ürettiği `Main` metodu korunur — kendi `Main`'imizi yazmak onunla
> çakışırdı. `OnStartup`, `StartupUri` penceresi oluşturulmadan önce çalışır,
> bu yüzden Velopack yeterince erken devreye girer.

- [ ] **Step 3: Sürüm akışını yaz**

`.github/workflows/release.yml`:

```yaml
name: Sürüm

on:
  push:
    tags:
      - 'v*'

jobs:
  release:
    runs-on: windows-latest

    permissions:
      contents: write

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Testleri çalıştır
        run: dotnet test --configuration Release

      - name: Sürüm numarasını etiketten al
        id: version
        shell: bash
        run: echo "value=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"

      - name: Yayınla
        run: >
          dotnet publish src/KolayYazdir.App
          --configuration Release
          --runtime win-x64
          --self-contained true
          --output publish

      - name: Velopack paketini üret
        run: |
          dotnet tool install -g vpk
          vpk pack `
            --packId KolayYazdir `
            --packVersion ${{ steps.version.outputs.value }} `
            --packDir publish `
            --mainExe KolayYazdir.exe `
            --packTitle "Kolay Yazdır"

      - name: GitHub sürümüne yükle
        uses: softprops/action-gh-release@v2
        with:
          files: Releases/*
          generate_release_notes: true
```

- [ ] **Step 4: README yaz**

`README.md`:

```markdown
# Kolay Yazdır

Kırtasiyede günlük çıktı işini tek pencerede bitiren Windows uygulaması.
Windows'un yazdırma ayarlarındaki dağınıklığı gizler, sadece gerçekten
kullanılan seçenekleri bırakır.

## Ne yapar

- Görsel, PDF, Word ve Excel dosyalarını birlikte yazdırır
- A4 / A5 / A3, dikey / yatay, renkli / siyah beyaz
- Önlü arkalı — çevirme kenarını yönden kendisi seçer
- Kağıt cinsini yazıcının kendi listesinden seçtirir ("Düz", "Kalın 1")
- Bir sayfaya 1 / 2 / 4 / 9 / 16 / 35 sayfa yerleştirir
- Sayfaya sığdır seçeneği (varsayılan kapalı, gerçek boyut korunur)
- Sayfa aralığı ve kopya sayısı
- Canlı önizleme — ekranda gördüğün kağıda çıkanın aynısı

## Kurulum

[Sürümler](../../releases) sayfasından son `KolayYazdir-win-Setup.exe`
dosyasını indir ve çalıştır. Yönetici şifresi istemez.

Kurulumdan sonra uygulama kendini günceller; bir daha indirmen gerekmez.

## Gereksinimler

- Windows 10 veya üstü
- Word ve Excel yazdırmak için LibreOffice veya Microsoft Office

## Geliştirme

```bash
dotnet test
dotnet run --project src/KolayYazdir.App
```

Tasarım kararları için `docs/superpowers/specs/` altına bak.
```

- [ ] **Step 5: Depoyu GitHub'a bağla**

> Bu adım kullanıcının onayını gerektirir. Depo adresini ve görünürlüğünü
> (özel mi açık mı) sor, sonra oluştur:

```bash
gh repo create kolay-yazdir --private --source . --remote origin
git push -u origin main
```

Ardından `UpdateService.RepositoryUrl` sabitini gerçek adresle güncelle ve commit et.

- [ ] **Step 6: İlk sürümü üret ve doğrula**

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions'ın yeşil yandığını ve Releases sayfasında `KolayYazdir-win-Setup.exe`
dosyasının oluştuğunu gör. Kurulumu bir bilgisayarda dene.

Güncelleme akışını doğrulamak için sürüm numarasını `v1.0.1` yapıp yeniden
etiketle; kurulu uygulamayı aç, kapat, tekrar aç — yeni sürümle geldiğini gör.

- [ ] **Step 7: Commit**

```bash
git add src/KolayYazdir.App .github README.md
git commit -m "Otomatik güncelleme ve GitHub sürüm akışı"
```

---

## Bitiş kontrolü

Tüm görevler bitince:

```bash
dotnet test
```

Beklenen: tüm projeler geçer; LibreOffice kurulu olmayan makinede ilgili
entegrasyon testleri `Skipped` görünür.

Dükkandaki yazıcıda yerinde doğrulanacak üç şey kalır — bunlar spec'in
"Açık riskler" bölümünde yazılıdır:

1. Elle önlü arkalı sayfa sırası (yüzü aşağı / yukarı çıkaran yazıcı farkı)
2. Kağıt cinsi isimlerinin sürücüden doğru geldiği ("Düz", "Kalın 1")
3. Kenar boşluklarının gerçek çıktıda beklendiği gibi olduğu
