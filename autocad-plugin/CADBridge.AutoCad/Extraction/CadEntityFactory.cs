using CADBridge.AutoCad.Models;

namespace CADBridge.AutoCad.Extraction;

/// <summary>
/// <see cref="RawDrawingData"/>'yı sınıflandırma ajanlarının tükettiği
/// mm-normalize <see cref="CadEntity"/> listesine çevirir. Her entity'nin
/// noktaları çizimin biriminden hesaplanan ölçek faktörüyle çarpılır.
/// </summary>
public static class CadEntityFactory
{
    /// <summary>
    /// Ham çizim verisini mm-normalize <see cref="CadEntity"/> listesine
    /// çevirir. <c>UnitWarning</c>, birim <see cref="DrawingUnit.Undefined"/>
    /// ise dolu, aksi halde <c>null</c>'dır (bkz. <see cref="UnitNormalizer"/>).
    /// </summary>
    public static (IReadOnlyList<CadEntity> Entities, string? UnitWarning) Create(RawDrawingData data)
    {
        var (scale, warning) = UnitNormalizer.ToMillimeters(data.Units);

        var entities = data.Entities
            .Select(record => ToCadEntity(record, scale))
            .ToList();

        return (entities, warning);
    }

    private static CadEntity ToCadEntity(RawEntityRecord record, double scale) => new()
    {
        EntityId = record.EntityId,
        Layer = record.Layer,
        BlockName = record.BlockName,
        Points = record.Points.Select(p => (p.X * scale, p.Y * scale)).ToList(),
        Closed = record.Closed,
    };
}
