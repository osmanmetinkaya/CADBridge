using CADBridge.AutoCad.Classification;
using CADBridge.AutoCad.Models;
using Xunit;

namespace CADBridge.AutoCad.Tests;

public class GeometryInterpreterAgentTests
{
    private readonly GeometryInterpreterAgent _agent = new();

    private static CadEntity Entity(IReadOnlyList<(double X, double Y)> points, bool closed) => new()
    {
        EntityId = "E-TEST",
        Layer = "IRRELEVANT",
        Points = points,
        Closed = closed,
    };

    private static (double X, double Y)[] Rectangle(double width, double length) => new[]
    {
        (0.0, 0.0),
        (length, 0.0),
        (length, width),
        (0.0, width),
    };

    private static (double X, double Y)[] Arc(double radius, double sweepDeg, int count = 9)
    {
        var points = new (double X, double Y)[count];
        for (var i = 0; i < count; i++)
        {
            var t = sweepDeg * i / (count - 1);
            var rad = t * Math.PI / 180.0;
            points[i] = (radius * Math.Cos(rad), radius * Math.Sin(rad));
        }

        return points;
    }

    [Fact]
    public void LongThinClosedRectangle_ClassifiesAsWallWithHighConfidence()
    {
        // 180mm kalın, 4000mm uzun: L≥2000 ve L/W≥6 → 0.8.
        var result = _agent.Classify(new[] { Entity(Rectangle(180, 4000), closed: true) }).Single();

        Assert.Equal(CandidateType.Wall, result.CandidateType);
        Assert.Equal(0.8, result.Confidence);
        Assert.Equal("closed_rect_wall:t=180mm,l=4000mm", result.MatchedRule);
    }

    [Fact]
    public void ShortWallSegment_ClassifiesAsWallWithLowConfidence()
    {
        // 300mm x 1500mm: wall bandında ama L<2000 → 0.5.
        var result = _agent.Classify(new[] { Entity(Rectangle(300, 1500), closed: true) }).Single();

        Assert.Equal(CandidateType.Wall, result.CandidateType);
        Assert.Equal(0.5, result.Confidence);
        Assert.Equal("closed_rect_wall:t=300mm,l=1500mm", result.MatchedRule);
    }

    [Fact]
    public void LargeClosedRectangle_ClassifiesAsFloorWithHighConfidence()
    {
        // 5000x4000mm = 20 m² → floor, 0.8.
        var result = _agent.Classify(new[] { Entity(Rectangle(4000, 5000), closed: true) }).Single();

        Assert.Equal(CandidateType.Floor, result.CandidateType);
        Assert.Equal(0.8, result.Confidence);
        Assert.Equal("closed_area_floor:20.0", result.MatchedRule);
    }

    [Fact]
    public void MediumClosedRectangle_ClassifiesAsFloorWithLowConfidence()
    {
        // 2000x1000mm = 2 m² → floor, 0.5 (1.5-5 m² arası), W=1000≥600.
        var result = _agent.Classify(new[] { Entity(Rectangle(1000, 2000), closed: true) }).Single();

        Assert.Equal(CandidateType.Floor, result.CandidateType);
        Assert.Equal(0.5, result.Confidence);
        Assert.Equal("closed_area_floor:2.0", result.MatchedRule);
    }

    [Fact]
    public void ThinClosedRectangleInWindowRange_ClassifiesAsWindowAtLowConfidence()
    {
        // 180mm x 800mm: L∈[400,1200) → window, her zaman 0.5.
        var result = _agent.Classify(new[] { Entity(Rectangle(180, 800), closed: true) }).Single();

        Assert.Equal(CandidateType.Window, result.CandidateType);
        Assert.Equal(0.5, result.Confidence);
        Assert.Equal("thin_rect_window:w=800mm", result.MatchedRule);
    }

    [Fact]
    public void NinetyDegreeSwingArc_ClassifiesAsDoorWithHighConfidence()
    {
        // R=800mm, 90° sweep → door, 0.8.
        var result = _agent.Classify(new[] { Entity(Arc(radius: 800, sweepDeg: 90), closed: false) }).Single();

        Assert.Equal(CandidateType.Door, result.CandidateType);
        Assert.Equal(0.8, result.Confidence);
        Assert.Equal("door_swing_arc:r=800mm,sweep=90deg", result.MatchedRule);
    }

    [Fact]
    public void WideSwingArc_ClassifiesAsDoorWithLowConfidence()
    {
        // R=1100mm, 110° sweep: geçerli yay ama dar aralık dışı → 0.5.
        var result = _agent.Classify(new[] { Entity(Arc(radius: 1100, sweepDeg: 110), closed: false) }).Single();

        Assert.Equal(CandidateType.Door, result.CandidateType);
        Assert.Equal(0.5, result.Confidence);
        Assert.Equal("door_swing_arc:r=1100mm,sweep=110deg", result.MatchedRule);
    }

    [Fact]
    public void SmallClosedSquare_MatchesNothing_ReturnsUnknown()
    {
        // 100x100mm: wall (L<1200), window (L<400), floor (alan<1.5 m²) hepsi başarısız.
        var result = _agent.Classify(new[] { Entity(Rectangle(100, 100), closed: true) }).Single();

        Assert.Equal(CandidateType.Unknown, result.CandidateType);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal("no_geometric_match", result.MatchedRule);
    }

    [Fact]
    public void CenterlineWall_TwoPointOpenLine_ReturnsUnknown()
    {
        // Kalınlıksız 2 noktalı açık çizgi (centerline duvar): izole
        // geometriden tespit edilemez → unknown.
        var result = _agent.Classify(new[] { Entity(new[] { (0.0, 0.0), (4000.0, 0.0) }, closed: false) }).Single();

        Assert.Equal(CandidateType.Unknown, result.CandidateType);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal("no_geometric_match", result.MatchedRule);
    }

    [Fact]
    public void OpenPolylineThatIsNotAnArc_ReturnsUnknown()
    {
        // Düz-ish çok noktalı açık çizgi yay değil → door reddedilir.
        var straightish = new[] { (0.0, 0.0), (1000.0, 5.0), (2000.0, 0.0), (3000.0, 5.0), (4000.0, 0.0) };
        var result = _agent.Classify(new[] { Entity(straightish, closed: false) }).Single();

        Assert.Equal(CandidateType.Unknown, result.CandidateType);
        Assert.Equal("no_geometric_match", result.MatchedRule);
    }

    [Fact]
    public void PerfectlyCollinearOpenPolyline_ReturnsUnknown_WithoutThrowing()
    {
        // Tam doğrusal (collinear) 5 noktalı açık polyline (ör. ölçü
        // çizgisi / leader / break line). FitCircle degenere sistemde
        // exception fırlatır; MatchDoor bunu yakalayıp null dönmeli, yani
        // batch çökmeden unknown'a düşmeli.
        var collinear = new[] { (0.0, 0.0), (1000.0, 0.0), (2000.0, 0.0), (3000.0, 0.0), (4000.0, 0.0) };
        var result = _agent.Classify(new[] { Entity(collinear, closed: false) }).Single();

        Assert.Equal(CandidateType.Unknown, result.CandidateType);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal("no_geometric_match", result.MatchedRule);
    }
}
