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
