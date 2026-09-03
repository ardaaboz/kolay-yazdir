using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KolayYazdir.App.Controls;
using KolayYazdir.App.ViewModels;
using KolayYazdir.Documents;
using KolayYazdir.Printing;

namespace KolayYazdir.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private Point _dragOrigin;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.RestoreSettings();

        Loaded += (_, _) =>
        {
            LockSettingsIntoView();
            FitIntoWorkArea();
        };
        // İpucu satırları açılıp kapandıkça blok büyüyüp küçülüyor; taban
        // yükseklik onunla birlikte güncellenmeli.
        Settings.SizeChanged += (_, _) => LockSettingsIntoView();
    }

    /// <summary>
    /// Ayarların kaydırmadan görünmesini pencerenin en küçük boyuna bağlar:
    /// blok ne kadar yer istiyorsa taban o kadar yükselir. Sayı elle yazılmadığı
    /// için yazı tipi, DPI ya da yeni bir ayar satırı kuralı bozamaz.
    /// </summary>
    private void LockSettingsIntoView()
    {
        UpdateLayout();

        // Sol sütunun dışında kalan her şey: başlık çubuğu, kenarlıklar, üst şerit.
        var chrome = ActualHeight - Sidebar.ActualHeight;
        if (chrome <= 0 || Settings.ActualHeight <= 0) return;

        MinHeight = Math.Ceiling(
            chrome + SidebarMetrics.RequiredHeight(PickButton.ActualHeight, Settings.ActualHeight));

        if (Height < MinHeight) Height = MinHeight;
    }

    /// <summary>
    /// Pencereyi ekranın çalışma alanına sığdırır. Windows açılış boyunu ekrana
    /// göre kırpıyor ama görev çubuğunu hesaba katmıyor: yüksek çözünürlüklü bir
    /// ekranda pencerenin alt şeridi çubuğun altında kalıyor, oradaki ayar da
    /// görünmez oluyordu.
    /// </summary>
    private void FitIntoWorkArea()
    {
        var work = SystemParameters.WorkArea;

        if (ActualHeight > work.Height) Height = work.Height;
        if (ActualWidth > work.Width) Width = work.Width;

        UpdateLayout();

        Top = Math.Max(work.Top, Math.Min(Top, work.Bottom - ActualHeight));
        Left = Math.Max(work.Left, Math.Min(Left, work.Right - ActualWidth));
    }

    private async void PickFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = DocumentLoader.FileDialogFilter
        };

        if (Directory.Exists(_viewModel.DefaultFolder)) dialog.InitialDirectory = _viewModel.DefaultFolder;

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
        var acceptable = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(typeof(FileEntry));
        e.Effects = acceptable ? DragDropEffects.Copy : DragDropEffects.None;
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

    /// <summary>
    /// Sol/sağ ok tuşlarıyla sayfa değiştirme. Metin kutusundayken imleci
    /// hareket ettirmek gerektiği için orada devreye girmiyor.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Handled || Keyboard.FocusedElement is TextBox) return;

        if (e.Key == Key.Left)
        {
            _viewModel.PreviousSheetCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            _viewModel.NextSheetCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        switch (await _viewModel.PrintAsync())
        {
            case PrintOutcome.AlreadyPrinting:
                break;

            case PrintOutcome.NothingToPrint:
                PickFiles_Click(sender, e);
                break;

            case PrintOutcome.NoPrinter:
                MessageBox.Show(this,
                    "Yazıcı bulunamadı. Yazıcının açık ve bağlı olduğundan emin ol.",
                    "Kolay Yazdır", MessageBoxButton.OK, MessageBoxImage.Warning);
                break;

            case PrintOutcome.NeedsPaperFlip:
                var answer = MessageBox.Show(this,
                    "Ön yüzler basıldı.\n\nKağıtları çıkarıp ters çevir ve aynı sırayla tepsiye koy, " +
                    "sonra Tamam'a bas.",
                    "Önlü arkalı", MessageBoxButton.OKCancel, MessageBoxImage.Information);

                if (answer == MessageBoxResult.OK && _viewModel.PendingSecondPass is { } secondPass)
                {
                    await secondPass();
                }
                break;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.PersistSettings();
        _viewModel.Dispose();
        base.OnClosing(e);
    }
}
