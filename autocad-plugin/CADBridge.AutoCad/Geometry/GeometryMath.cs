namespace CADBridge.AutoCad.Geometry;

/// <summary>
/// Minimum alanlı yönlendirilmiş bounding box sonucu; kısa kenar
/// <see cref="Width"/> (W), uzun kenar <see cref="Length"/> (L), W ≤ L.
/// </summary>
public sealed record OrientedBoundingBox(double Width, double Length);

/// <summary>
/// En küçük kareler çember uydurma sonucu: merkez, yarıçap, maksimum
/// radyal sapma ve toplam sweep açısı (derece).
/// </summary>
public sealed record CircleFit(
    double CenterX,
    double CenterY,
    double Radius,
    double MaxRadialDeviation,
    double SweepAngleDegrees);

/// <summary>
/// Saf/durumsuz (pure) geometri yardımcıları. Dış kütüphane kullanmaz.
/// Bkz. agents/geometry-interpreter-agent.md.
/// </summary>
public static class GeometryMath
{
    /// <summary>
    /// Kapalı bir polygon'un alanı (shoelace formülü, mutlak değer).
    /// Noktalar sıralı köşelerdir; ilk noktanın sonda tekrarlanması alanı
    /// etkilemez.
    /// </summary>
    public static double PolygonArea(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 3)
        {
            return 0.0;
        }

        double sum = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return Math.Abs(sum) / 2.0;
    }

    /// <summary>
    /// Convex hull (Andrew's monotone chain) + rotating calipers ile
    /// minimum alanlı yönlendirilmiş bounding box. Kenarlardan biri
    /// dikdörtgen kenarına paralel olacak şekilde her hull kenarı yönünde
    /// projeksiyon denenir.
    /// </summary>
    public static OrientedBoundingBox MinimumAreaBoundingBox(IReadOnlyList<(double X, double Y)> points)
    {
        var hull = ConvexHull(points);

        if (hull.Count < 2)
        {
            return new OrientedBoundingBox(0.0, 0.0);
        }

        if (hull.Count == 2)
        {
            var length = Distance(hull[0], hull[1]);
            return Normalize(0.0, length);
        }

        var bestArea = double.PositiveInfinity;
        var bestW = 0.0;
        var bestL = 0.0;

        for (var i = 0; i < hull.Count; i++)
        {
            var a = hull[i];
            var b = hull[(i + 1) % hull.Count];

            var edgeX = b.X - a.X;
            var edgeY = b.Y - a.Y;
            var edgeLen = Math.Sqrt((edgeX * edgeX) + (edgeY * edgeY));
            if (edgeLen < 1e-12)
            {
                continue;
            }

            // Kenar yönü (ux, uy) ve dik yönü (-uy, ux) birim vektörleri.
            var ux = edgeX / edgeLen;
            var uy = edgeY / edgeLen;

            double minU = double.PositiveInfinity, maxU = double.NegativeInfinity;
            double minV = double.PositiveInfinity, maxV = double.NegativeInfinity;

            foreach (var p in hull)
            {
                var proj = (p.X * ux) + (p.Y * uy);
                var perp = (-p.X * uy) + (p.Y * ux);
                minU = Math.Min(minU, proj);
                maxU = Math.Max(maxU, proj);
                minV = Math.Min(minV, perp);
                maxV = Math.Max(maxV, perp);
            }

            var side1 = maxU - minU;
            var side2 = maxV - minV;
            var area = side1 * side2;

            if (area < bestArea)
            {
                bestArea = area;
                bestW = side1;
                bestL = side2;
            }
        }

        return Normalize(bestW, bestL);
    }

    /// <summary>
    /// Dikdörtgensellik: alanın OBB alanına oranı. 1'e ne kadar yakınsa
    /// şekil o kadar temiz dikdörtgen. Dejenere OBB için 0 döner.
    /// </summary>
    public static double Rectangularity(double area, OrientedBoundingBox obb)
    {
        var boxArea = obb.Width * obb.Length;
        return boxArea < 1e-12 ? 0.0 : area / boxArea;
    }

    /// <summary>
    /// Açık bir nokta dizisine en iyi uyan çemberi (Kåsa cebirsel en küçük
    /// kareler) bulur; maksimum radyal sapmayı ve toplam sweep açısını
    /// (derece) hesaplar.
    /// </summary>
    public static CircleFit FitCircle(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 3)
        {
            throw new ArgumentException("Circle fit requires at least 3 points.", nameof(points));
        }

        var n = points.Count;
        double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0, sxz = 0, syz = 0, sz = 0;

        foreach (var p in points)
        {
            var z = (p.X * p.X) + (p.Y * p.Y);
            sx += p.X;
            sy += p.Y;
            sxx += p.X * p.X;
            syy += p.Y * p.Y;
            sxy += p.X * p.Y;
            sxz += p.X * z;
            syz += p.Y * z;
            sz += z;
        }

        // Normal denklemler: [D E F] için 3x3 sistem çöz (Cramer).
        //   sxx*D + sxy*E + sx*F = -sxz
        //   sxy*D + syy*E + sy*F = -syz
        //    sx*D +  sy*E +  n*F = -sz
        var m = new[,]
        {
            { sxx, sxy, sx },
            { sxy, syy, sy },
            { sx, sy, (double)n },
        };
        var rhs = new[] { -sxz, -syz, -sz };

        var (d, e, f) = SolveLinear3(m, rhs);

        var cx = -d / 2.0;
        var cy = -e / 2.0;
        var radius = Math.Sqrt(Math.Max(0.0, (cx * cx) + (cy * cy) - f));

        var maxDeviation = 0.0;
        foreach (var p in points)
        {
            var dist = Distance((cx, cy), p);
            maxDeviation = Math.Max(maxDeviation, Math.Abs(dist - radius));
        }

        var sweep = SweepAngleDegrees(points, cx, cy);

        return new CircleFit(cx, cy, radius, maxDeviation, sweep);
    }

    /// <summary>
    /// Merkez etrafında ardışık noktalar arası açısal adımların işaretli
    /// toplamının mutlak değeri (derece). Wraparound'a dayanıklı.
    /// </summary>
    private static double SweepAngleDegrees(IReadOnlyList<(double X, double Y)> points, double cx, double cy)
    {
        double total = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            var a0 = Math.Atan2(points[i - 1].Y - cy, points[i - 1].X - cx);
            var a1 = Math.Atan2(points[i].Y - cy, points[i].X - cx);
            var step = a1 - a0;

            // (-π, π] aralığına normalize et.
            while (step > Math.PI)
            {
                step -= 2 * Math.PI;
            }

            while (step <= -Math.PI)
            {
                step += 2 * Math.PI;
            }

            total += step;
        }

        return Math.Abs(total) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Andrew's monotone chain convex hull. Saat yönünün tersine (CCW)
    /// sıralı, son nokta ile ilk nokta tekrarlanmadan döner.
    /// </summary>
    private static IReadOnlyList<(double X, double Y)> ConvexHull(IReadOnlyList<(double X, double Y)> points)
    {
        var sorted = points
            .Distinct()
            .OrderBy(p => p.X)
            .ThenBy(p => p.Y)
            .ToList();

        if (sorted.Count <= 2)
        {
            return sorted;
        }

        var lower = new List<(double X, double Y)>();
        foreach (var p in sorted)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
            {
                lower.RemoveAt(lower.Count - 1);
            }

            lower.Add(p);
        }

        var upper = new List<(double X, double Y)>();
        for (var i = sorted.Count - 1; i >= 0; i--)
        {
            var p = sorted[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
            {
                upper.RemoveAt(upper.Count - 1);
            }

            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) =>
        ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));

    private static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static OrientedBoundingBox Normalize(double side1, double side2) =>
        new(Math.Min(side1, side2), Math.Max(side1, side2));

    private static (double, double, double) SolveLinear3(double[,] m, double[] rhs)
    {
        var det = Determinant3(m);
        if (Math.Abs(det) < 1e-12)
        {
            throw new InvalidOperationException("Degenerate point set: cannot fit a circle (collinear points).");
        }

        var x = Determinant3(Replace(m, 0, rhs)) / det;
        var y = Determinant3(Replace(m, 1, rhs)) / det;
        var z = Determinant3(Replace(m, 2, rhs)) / det;
        return (x, y, z);
    }

    private static double[,] Replace(double[,] m, int col, double[] rhs)
    {
        var copy = (double[,])m.Clone();
        for (var row = 0; row < 3; row++)
        {
            copy[row, col] = rhs[row];
        }

        return copy;
    }

    private static double Determinant3(double[,] m) =>
        (m[0, 0] * ((m[1, 1] * m[2, 2]) - (m[1, 2] * m[2, 1])))
        - (m[0, 1] * ((m[1, 0] * m[2, 2]) - (m[1, 2] * m[2, 0])))
        + (m[0, 2] * ((m[1, 0] * m[2, 1]) - (m[1, 1] * m[2, 0])));
}
