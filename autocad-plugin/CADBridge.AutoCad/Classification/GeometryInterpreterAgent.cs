using CADBridge.AutoCad.Geometry;
using CADBridge.AutoCad.Models;
using static System.FormattableString;

namespace CADBridge.AutoCad.Classification;

/// <summary>
/// Her <see cref="CadEntity"/>'yi yalnızca kendi saf geometrisinden
/// sınıflandırır; layer/blok adına bakmaz ve entity'ler arası ilişki
/// (pairing) kullanmaz. Bkz. agents/geometry-interpreter-agent.md.
/// </summary>
public sealed class GeometryInterpreterAgent : IClassificationAgent
{
    private const double HighConfidence = 0.8;
    private const double LowConfidence = 0.5;

    public IReadOnlyList<Candidate> Classify(IEnumerable<CadEntity> entities) =>
        entities.Select(ClassifyEntity).ToList();

    private static Candidate ClassifyEntity(CadEntity entity)
    {
        // İlk eşleşen kazanır: wall → window → floor → door. wall/window/floor
        // kapalı, door açık entity gerektirdiğinden çakışma olmaz.
        var match = MatchWall(entity)
                    ?? MatchWindow(entity)
                    ?? MatchFloor(entity)
                    ?? MatchDoor(entity);

        return new Candidate
        {
            EntityId = entity.EntityId,
            CandidateType = match?.Type ?? CandidateType.Unknown,
            Confidence = match?.Confidence ?? 0.0,
            MatchedRule = match?.MatchedRule ?? "no_geometric_match",
        };
    }

    private static (CandidateType Type, double Confidence, string MatchedRule)? MatchWall(CadEntity entity)
    {
        if (!entity.Closed || entity.Points.Count < 3)
        {
            return null;
        }

        var obb = GeometryMath.MinimumAreaBoundingBox(entity.Points);
        var rect = GeometryMath.Rectangularity(GeometryMath.PolygonArea(entity.Points), obb);

        if (rect < 0.85 || obb.Width is < 70 or > 400 || obb.Length < 1200 || obb.Length / obb.Width < 4)
        {
            return null;
        }

        var confidence = obb.Length >= 2000 && obb.Length / obb.Width >= 6 ? HighConfidence : LowConfidence;
        var rule = Invariant($"closed_rect_wall:t={obb.Width:F0}mm,l={obb.Length:F0}mm");
        return (CandidateType.Wall, confidence, rule);
    }

    private static (CandidateType Type, double Confidence, string MatchedRule)? MatchWindow(CadEntity entity)
    {
        if (!entity.Closed || entity.Points.Count < 3)
        {
            return null;
        }

        var obb = GeometryMath.MinimumAreaBoundingBox(entity.Points);
        var rect = GeometryMath.Rectangularity(GeometryMath.PolygonArea(entity.Points), obb);

        if (rect < 0.85 || obb.Width is < 70 or > 400 || obb.Length is < 400 or >= 1200)
        {
            return null;
        }

        // İzole geometriden kapı/pencere ayrımı ilkesel olarak yapılamaz;
        // window her zaman düşük güvenle atanır (asla 0.8 verilmez).
        var rule = Invariant($"thin_rect_window:w={obb.Length:F0}mm");
        return (CandidateType.Window, LowConfidence, rule);
    }

    private static (CandidateType Type, double Confidence, string MatchedRule)? MatchFloor(CadEntity entity)
    {
        if (!entity.Closed || entity.Points.Count < 3)
        {
            return null;
        }

        var area = GeometryMath.PolygonArea(entity.Points);
        var obb = GeometryMath.MinimumAreaBoundingBox(entity.Points);

        if (area < 1_500_000 || obb.Width < 600)
        {
            return null;
        }

        var confidence = area >= 5_000_000 ? HighConfidence : LowConfidence;
        var areaM2 = area / 1_000_000.0;
        var rule = Invariant($"closed_area_floor:{areaM2:F1}");
        return (CandidateType.Floor, confidence, rule);
    }

    private static (CandidateType Type, double Confidence, string MatchedRule)? MatchDoor(CadEntity entity)
    {
        if (entity.Closed || entity.Points.Count < 5)
        {
            return null;
        }

        CircleFit fit;
        try
        {
            fit = GeometryMath.FitCircle(entity.Points);
        }
        catch (InvalidOperationException)
        {
            // Doğrusal/degenere noktalar yay olarak uydurulamaz; bu açık
            // polyline sadece door adayı değildir → unknown'a düşer.
            return null;
        }

        var tolerance = Math.Max(0.02 * fit.Radius, 5.0);

        if (fit.MaxRadialDeviation > tolerance
            || fit.Radius is < 600 or > 1200
            || fit.SweepAngleDegrees is < 60 or > 120)
        {
            return null;
        }

        var confidence = fit.SweepAngleDegrees is >= 80 and <= 100 && fit.Radius is >= 600 and <= 1000
            ? HighConfidence
            : LowConfidence;
        var rule = Invariant($"door_swing_arc:r={fit.Radius:F0}mm,sweep={fit.SweepAngleDegrees:F0}deg");
        return (CandidateType.Door, confidence, rule);
    }
}
