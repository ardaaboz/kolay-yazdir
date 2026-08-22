using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KolayYazdir.App.Services;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Documents;
using KolayYazdir.Printing;
using ColorMode = KolayYazdir.Core.Models.ColorMode;
using Orientation = KolayYazdir.Core.Models.Orientation;

namespace KolayYazdir.App.ViewModels;

public enum PrintOutcome { Done, NothingToPrint, NoPrinter, NeedsPaperFlip }

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DocumentLoader _loader = DocumentLoader.Default;
    private readonly SettingsStore _settingsStore = SettingsStore.Default;
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

    /// <summary>Dosya seçme penceresinin açılacağı klasör.</summary>
    public string DefaultFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    /// <summary>Elle önlü arkalıda ikinci geçişi çalıştıran eylem.</summary>
    public Action? PendingSecondPass { get; private set; }

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

    /// <summary>Sürücü kopyalamayı üstlenmiyorsa yaprakları biz çoğaltırız.</summary>
    public bool DriverHandlesCopies => Capabilities?.SupportsMultipleCopies ?? false;

    /// <summary>Herhangi bir ayar değiştiğinde yerleşimi ve önizlemeyi tazeler.</summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // Kendi yazdığımız çıktı alanları döngü kurmasın.
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

        // MediaType bir struct; FirstOrDefault boş listede (0, null) döner.
        if (MediaType is null && MediaTypes.Count > 0) MediaType = MediaTypes[0];
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

        var paper = Paper.SizeOf(PaperSize, Orientation);
        var printable = Capabilities?.PrintableArea ?? new RectPt(0, 0, paper.Width, paper.Height);

        _sheets = LayoutEngine.Build(_documents.Pages, CurrentSettings, printable);
        _preview.Load(_sheets);

        SheetLabel = _preview.Label;
        JobSummary = $"{LeafCount(_sheets)} yaprak · {_documents.Pages.Count} sayfa";
        DrawCurrentSheet();
    }

    private static int LeafCount(IReadOnlyList<Sheet> sheets) => sheets.Count == 0 ? 0 : sheets[^1].Index + 1;

    /// <summary>
    /// Görünen yaprağı arka planda çizer. 35'li yerleşimde tek yaprak otuz beş
    /// sayfa render etmek demek; bunu arayüz iş parçacığında yapmak pencereyi
    /// dondururdu. Önceki çizim sürüyorsa iptal edilir, böylece hızlı ayar
    /// değişikliklerinde sadece son istek tamamlanır.
    /// </summary>
    private async void DrawCurrentSheet()
    {
        if (_documents is not { } documents || _preview.Current is not { } sheet)
        {
            PreviewImage = null;
            return;
        }

        _previewWork?.Cancel();
        _previewWork?.Dispose();
        _previewWork = new CancellationTokenSource();
        var token = _previewWork.Token;
        var color = Color;

        try
        {
            var image = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var renderer = new SheetRenderer(documents);
                using var bitmap = renderer.RenderToBitmap(sheet, RenderConstants.PreviewDpi, color);

                token.ThrowIfCancellationRequested();
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

    public async Task AddFilesAsync(IEnumerable<string> paths, CancellationToken ct)
    {
        var incoming = paths.Where(DocumentLoader.IsSupported).ToList();
        var added = false;

        foreach (var path in incoming)
        {
            if (Files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))) continue;

            Files.Add(new FileEntry(path));
            added = true;
        }

        if (!added) return;

        // Kullanıcı başka bir klasöre gittiyse bir dahaki sefere oradan başla.
        if (Path.GetDirectoryName(incoming[0]) is { Length: > 0 } folder) DefaultFolder = folder;

        await ReloadDocumentsAsync(ct);
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

        Files.Move(from, Math.Clamp(to, 0, Files.Count - 1));
        await ReloadDocumentsAsync(ct);
    }

    /// <summary>Listedeki dosyaları yeniden açar ve yerleşimi tazeler.</summary>
    public async Task ReloadDocumentsAsync(CancellationToken ct)
    {
        _documents?.Dispose();
        _documents = null;

        var loaded = new List<SourceDocument>();
        foreach (var entry in Files)
        {
            entry.Error = null;
            entry.IsLoading = true;
            try
            {
                // PDF ve görseller anında açılır; Word/Excel dış bir sürece gider,
                // o sırada satırda "çevriliyor…" görünür.
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

    public IReadOnlyList<Sheet> SheetsForPrinting() =>
        DriverHandlesCopies ? _sheets : LayoutEngine.Repeat(_sheets, Copies);

    /// <summary>
    /// Yazdırır. Dosya seçilmemişse hata vermez — çağıran görünüm dosya seçme
    /// penceresini açar, kullanıcı doğru yere yönlendirilmiş olur.
    /// </summary>
    public PrintOutcome Print()
    {
        if (_documents is not { } documents || _sheets.Count == 0) return PrintOutcome.NothingToPrint;

        var printerName = PrinterCapabilities.DefaultPrinterName;
        if (printerName is null) return PrintOutcome.NoPrinter;

        var runner = new PrintJobRunner(new SheetRenderer(documents));
        var settings = CurrentSettings;

        var needsManualDuplex = settings.Duplex == DuplexMode.Duplex
            && Capabilities is { SupportsAutomaticDuplex: false };

        if (!needsManualDuplex)
        {
            runner.Run(SheetsForPrinting(), settings, printerName, DriverHandlesCopies);
            return PrintOutcome.Done;
        }

        // İki geçişli baskıda kopyayı sürücüye bırakamayız: sürücü her geçişi
        // ayrı çoğaltır ve deste sırası bozulur.
        var plan = ManualDuplexPlan.Split(LayoutEngine.Repeat(_sheets, Copies));
        var simplex = settings with { Duplex = DuplexMode.Simplex, Copies = 1 };

        runner.Run(plan.FirstPass, simplex, printerName, driverHandlesCopies: false);
        PendingSecondPass = () => runner.Run(plan.SecondPass, simplex, printerName, driverHandlesCopies: false);

        return PrintOutcome.NeedsPaperFlip;
    }

    /// <summary>Açılışta son kullanılan ayarları geri yükler.</summary>
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

    public void Dispose()
    {
        _previewWork?.Cancel();
        _previewWork?.Dispose();
        _documents?.Dispose();
    }
}
