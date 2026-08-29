using CADBridge.AutoCad.Geometry;
using Xunit;

namespace CADBridge.AutoCad.Tests;

public class GeometryMathTests
{
    private static (double X, double Y)[] Rectangle(double width, double length) => new[]
    {
        (0.0, 0.0),
        (length, 0.0),
        (length, width),
        (0.0, width),
    };

    /// <summary>
    /// Merkez etrafında [startDeg, endDeg] aralığında eşit açısal örneklenmiş
    /// yay noktaları üretir (açık polyline).
    /// </summary>
    private static (double X, double Y)[] Arc(double cx, double cy, double radius, double startDeg, double endDeg, int count)
    {
        var points = new (double X, double Y)[count];
        for (var i = 0; i < count; i++)
        {
            var t = startDeg + ((endDeg - startDeg) * i / (count - 1));
            var rad = t * Math.PI / 180.0;
            points[i] = (cx + (radius * Math.Cos(rad)), cy + (radius * Math.Sin(rad)));
        }

        return points;
    }

    [Fact]
    public void PolygonArea_UnitSquare_ReturnsHundred()
    {
        var square = Rectangle(10, 10);

        Assert.Equal(100.0, GeometryMath.PolygonArea(square), 6);
    }

    [Fact]
    public void PolygonArea_ThinRectangle_MatchesWidthTimesLength()
    {
        var rect = Rectangle(180, 4000);

        Assert.Equal(720_000.0, GeometryMath.PolygonArea(rect), 6);
    }

    [Fact]
    public void PolygonArea_ClockwiseOrder_ReturnsAbsoluteValue()
    {
        var clockwise = new[] { (0.0, 0.0), (0.0, 10.0), (10.0, 10.0), (10.0, 0.0) };

        Assert.Equal(100.0, GeometryMath.PolygonArea(clockwise), 6);
    }

    [Fact]
    public void MinimumAreaBoundingBox_AxisAlignedRectangle_ReturnsNormalizedSides()
    {
        var obb = GeometryMath.MinimumAreaBoundingBox(Rectangle(180, 4000));

        Assert.Equal(180.0, obb.Width, 6);
        Assert.Equal(4000.0, obb.Length, 6);
    }

    [Fact]
    public void MinimumAreaBoundingBox_RotatedRectangle_RecoversOriginalSides()
    {
        // 30° döndürülmüş 200x1500 dikdörtgen; OBB dönmeye karşı değişmez.
        const double angle = 30.0 * Math.PI / 180.0;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var baseRect = Rectangle(200, 1500);
        var rotated = baseRect
            .Select(p => ((p.X * cos) - (p.Y * sin), (p.X * sin) + (p.Y * cos)))
            .ToArray();

        var obb = GeometryMath.MinimumAreaBoundingBox(rotated);

        Assert.Equal(200.0, obb.Width, 4);
        Assert.Equal(1500.0, obb.Length, 4);
    }

    [Fact]
    public void Rectangularity_CleanRectangle_IsOne()
    {
        var rect = Rectangle(180, 4000);
        var area = GeometryMath.PolygonArea(rect);
        var obb = GeometryMath.MinimumAreaBoundingBox(rect);

        Assert.Equal(1.0, GeometryMath.Rectangularity(area, obb), 6);
    }

    [Fact]
    public void Rectangularity_Triangle_IsAroundHalf()
    {
        var triangle = new[] { (0.0, 0.0), (1000.0, 0.0), (0.0, 1000.0) };
        var area = GeometryMath.PolygonArea(triangle);
        var obb = GeometryMath.MinimumAreaBoundingBox(triangle);

        // Dik üçgen bounding box'ın yarısını doldurur.
        Assert.Equal(0.5, GeometryMath.Rectangularity(area, obb), 4);
    }

    [Fact]
    public void FitCircle_Semicircle_RecoversRadiusAndSweep()
    {
        var arc = Arc(cx: 100, cy: 50, radius: 800, startDeg: 0, endDeg: 180, count: 19);

        var fit = GeometryMath.FitCircle(arc);

        Assert.Equal(100.0, fit.CenterX, 3);
        Assert.Equal(50.0, fit.CenterY, 3);
        Assert.Equal(800.0, fit.Radius, 3);
        Assert.True(fit.MaxRadialDeviation < 1e-6);
        Assert.Equal(180.0, fit.SweepAngleDegrees, 3);
    }

    [Fact]
    public void FitCircle_QuarterArc_RecoversNinetyDegreeSweep()
    {
        var arc = Arc(cx: 0, cy: 0, radius: 900, startDeg: 0, endDeg: 90, count: 9);

        var fit = GeometryMath.FitCircle(arc);

        Assert.Equal(900.0, fit.Radius, 3);
        Assert.Equal(90.0, fit.SweepAngleDegrees, 3);
        Assert.True(fit.MaxRadialDeviation < 1e-6);
    }

    [Fact]
    public void FitCircle_NoisyArc_ReportsRadialDeviation()
    {
        var arc = Arc(cx: 0, cy: 0, radius: 500, startDeg: 0, endDeg: 90, count: 9).ToArray();

        // Bir noktayı yarıçap yönünde 20mm dışa it.
        arc[4] = (arc[4].X * 1.04, arc[4].Y * 1.04);

        var fit = GeometryMath.FitCircle(arc);

        Assert.True(fit.MaxRadialDeviation > 5.0);
    }
}
