using CADBridge.AutoCad.Models;

namespace CADBridge.AutoCad.Extraction;

/// <summary>
/// AutoCAD'den okunan tek bir entity'nin "aptal veri" temsili — hiçbir
/// AutoCAD'e özgü tip içermez (bu, extraction seam'inin amacıdır: bkz.
/// docs/architecture.md). Noktalar ham çizim biriminde, henüz mm'ye
/// normalize edilmemiş haldedir; normalizasyon <see cref="CadEntityFactory"/>
/// içinde yapılır.
/// </summary>
public sealed record RawEntityRecord(
    string EntityId,
    string Layer,
    string? BlockName,
    IReadOnlyList<(double X, double Y)> Points,
    bool Closed);

/// <summary>
/// Bir çizimin extraction çıktısı — AutoCAD API'sinden bağımsız POCO seam.
/// <see cref="CADBridge.AutoCad"/> tarafı yalnızca bu şekli tüketir;
/// gerçek Transaction/BlockTableRecord traversal'ı Plugin projesinin işidir
/// ve bu tipi doldurmaktan başka bir şey yapmaz.
/// </summary>
public sealed class RawDrawingData
{
    public required string SourceFile { get; init; }

    public required DrawingUnit Units { get; init; }

    public required IReadOnlyList<RawEntityRecord> Entities { get; init; }
}
