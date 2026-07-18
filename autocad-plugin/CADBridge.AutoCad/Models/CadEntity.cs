namespace CADBridge.AutoCad.Models;

/// <summary>
/// AutoCAD entity'sinin extraction sonrası, AutoCAD API'sinden bağımsız
/// hale getirilmiş normalize edilmiş temsili. Geometri noktaları her
/// zaman milimetre cinsindendir (bkz. docs/architecture.md birim
/// normalizasyonu kuralı).
/// </summary>
public sealed class CadEntity
{
    public required string EntityId { get; init; }

    public required string Layer { get; init; }

    /// <summary>Blok referansıysa blok adı; değilse null.</summary>
    public string? BlockName { get; init; }

    public IReadOnlyList<(double X, double Y)> Points { get; init; } = Array.Empty<(double, double)>();

    public bool Closed { get; init; }
}
