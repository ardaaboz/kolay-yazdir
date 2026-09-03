using System.Windows;
using KolayYazdir.App.Controls;

namespace KolayYazdir.App.Tests;

/// <summary>
/// Tezgahta ayar aramak için tekerlek çevirmek baskıyı geciktiriyor: yazdırma
/// ayarlarının tamamı kaydırmadan görünmek zorunda. Ölçüyü teste bağlıyoruz ki
/// bir gün eklenen yeni bir satır paneli sessizce taşırmasın.
/// </summary>
public class SidebarLayoutTests
{
    [Fact]
    public void Settings_fit_the_height_reserved_for_them()
    {
        var height = WpfThread.Run(() =>
        {
            var panel = new SettingsPanel();
            panel.Measure(new Size(SidebarMetrics.ContentWidth, double.PositiveInfinity));
            return panel.DesiredSize.Height;
        });

        Assert.InRange(height, 1, SidebarMetrics.SettingsHeightBudget);
    }

    [Fact]
    public void Settings_stay_within_the_sidebar_width()
    {
        var width = WpfThread.Run(() =>
        {
            var panel = new SettingsPanel();
            panel.Measure(new Size(SidebarMetrics.ContentWidth, double.PositiveInfinity));
            return panel.DesiredSize.Width;
        });

        Assert.InRange(width, 1, SidebarMetrics.ContentWidth);
    }

    [Fact]
    public void The_window_floor_leaves_room_for_the_file_list_and_the_settings()
    {
        var required = SidebarMetrics.RequiredHeight(pickButtonHeight: 48, settingsHeight: 400);

        Assert.Equal(24 + 48 + 12 + SidebarMetrics.FileListMinHeight + 12 + 400, required);
    }

    /// <summary>
    /// Ölçülen panel büyürse pencerenin tabanı da büyümeli; küçülürse taban
    /// serbest kalmalı. Aradaki fark birebir panelin farkı kadardır.
    /// </summary>
    [Fact]
    public void A_taller_settings_block_raises_the_window_floor_by_the_same_amount()
    {
        var lower = SidebarMetrics.RequiredHeight(48, 400);
        var higher = SidebarMetrics.RequiredHeight(48, 460);

        Assert.Equal(60, higher - lower);
    }
}
