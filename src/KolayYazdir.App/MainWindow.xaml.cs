using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KolayYazdir.App.ViewModels;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Documents;
using KolayYazdir.Printing;
using ColorMode = KolayYazdir.Core.Models.ColorMode;
using Orientation = KolayYazdir.Core.Models.Orientation;

namespace KolayYazdir.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private Point _dragOrigin;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        PaperSelector.ItemsSource = new object[] { PaperFormat.A4, PaperFormat.A5, PaperFormat.A3 };
        OrientationSelector.ItemsSource = new object[] { Orientation.Portrait, Orientation.Landscape };
        ColorSelector.ItemsSource = new object[] { ColorMode.Color, ColorMode.Monochrome };
        DuplexSelector.ItemsSource = new object[] { DuplexMode.Simplex, DuplexMode.Duplex };
        NUpSelector.ItemsSource = new object[]
        {
            PagesPerSheet.One, PagesPerSheet.Two, PagesPerSheet.Four,
            PagesPerSheet.Nine, PagesPerSheet.Sixteen, PagesPerSheet.ThirtyFive
        };
        PaperTypeSelector.ItemsSource = new object[] { PaperType.Plain, PaperType.Thick };

        _viewModel.RestoreSettings();
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
