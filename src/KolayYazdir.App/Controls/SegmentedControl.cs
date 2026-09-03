using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace KolayYazdir.App.Controls;

/// <summary>
/// Yan yana duran, tek tıkla seçilen düğme grubu. Açılır kutu yerine bunu
/// kullanıyoruz: seçili olan tek bakışta görünür ve seçim tek tıkla değişir —
/// tezgahta hızlı çalışmanın karşılığı bu.
/// </summary>
public sealed class SegmentedControl : ItemsControl
{
    private static readonly Brush SelectedBackground = Freeze(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly Brush SelectedForeground = Freeze(Color.FromRgb(0x0A, 0x0A, 0x0A));
    private static readonly Brush IdleBackground = Freeze(Color.FromRgb(0x1E, 0x1E, 0x1E));
    private static readonly Brush IdleForeground = Freeze(Color.FromRgb(0xC4, 0xC4, 0xC4));
    private static readonly Brush IdleBorder = Freeze(Color.FromRgb(0x3A, 0x3A, 0x3A));

    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(
        nameof(SelectedValue), typeof(object), typeof(SegmentedControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectionChanged));

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    /// <summary>
    /// Düğme üzerinde görünecek metni üretir. Değerler enum olduğu için
    /// ham hâlleri İngilizce; arayüzde Türkçe görünmeleri gerekiyor.
    /// </summary>
    public static readonly DependencyProperty LabelConverterProperty = DependencyProperty.Register(
        nameof(LabelConverter), typeof(IValueConverter), typeof(SegmentedControl), new PropertyMetadata(null));

    public IValueConverter? LabelConverter
    {
        get => (IValueConverter?)GetValue(LabelConverterProperty);
        set => SetValue(LabelConverterProperty, value);
    }

    public SegmentedControl()
    {
        Focusable = false;

        var panel = new FrameworkElementFactory(typeof(UniformGrid));
        panel.SetValue(UniformGrid.RowsProperty, 1);
        ItemsPanel = new ItemsPanelTemplate(panel);

        ItemContainerGenerator.StatusChanged += (_, _) =>
        {
            if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated) RefreshAppearance();
        };
    }

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
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
        if (Items.Count == 0) return;

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
            Content = owner.LabelConverter?.Convert(
                value, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture) ?? value;
            Margin = new Thickness(3, 0, 3, 0);
            FontSize = 14;
            Padding = new Thickness(0, 9, 0, 9);
            HorizontalContentAlignment = HorizontalAlignment.Center;
            BorderThickness = new Thickness(1);
            Cursor = System.Windows.Input.Cursors.Hand;

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
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            border.SetBinding(Border.BackgroundProperty, FromTemplatedParent(nameof(Background)));
            border.SetBinding(Border.BorderBrushProperty, FromTemplatedParent(nameof(BorderBrush)));
            border.SetBinding(Border.BorderThicknessProperty, FromTemplatedParent(nameof(BorderThickness)));

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            // Padding'i şablona bağlamazsak düğme yüksekliği yazıya yapışık kalır.
            content.SetBinding(MarginProperty, FromTemplatedParent(nameof(Padding)));
            border.AppendChild(content);

            return new ControlTemplate(typeof(SegmentButton)) { VisualTree = border };
        }

        private static Binding FromTemplatedParent(string path) =>
            new(path) { RelativeSource = RelativeSource.TemplatedParent };
    }
}
