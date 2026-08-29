using Autodesk.AutoCAD.DatabaseServices;
using CADBridge.AutoCad.Extraction;
using CADBridge.AutoCad.Models;

namespace CADBridge.AutoCad.Plugin;

/// <summary>
/// Gerçek AutoCAD veritabanını gezip <see cref="RawDrawingData"/> "aptal
/// veri" seam'ini doldurur — algoritma içermez (bkz. docs/architecture.md
/// "Test edilebilirlik sınırı (extraction katmanı)"). Tüm sınıflandırma
/// mantığı CADBridge.AutoCad tarafında, tamamen testable şekilde yaşar.
/// </summary>
public static class DrawingExtractor
{
    /// <summary>
    /// ModelSpace'i Transaction/BlockTableRecord deseniyle gezer: her
    /// Polyline (LWPOLYLINE) ve BlockReference'ı bir <see cref="RawEntityRecord"/>'a
    /// çevirir, çizim biriminden <see cref="DrawingUnit"/>'e tek satırlık
    /// çeviri yapar.
    /// </summary>
    public static RawDrawingData Extract(Database db, string sourceFile)
    {
        var records = new List<RawEntityRecord>();

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in modelSpace)
            {
                Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);

                RawEntityRecord? record = ent switch
                {
                    Polyline pl => FromPolyline(pl),
                    BlockReference br => FromBlockReference(br),
                    _ => null,
                };

                if (record is not null)
                {
                    records.Add(record);
                }
            }

            tr.Commit();
        }

        return new RawDrawingData
        {
            SourceFile = sourceFile,
            Units = ToDrawingUnit(db.Insunits),
            Entities = records,
        };
    }

    private static RawEntityRecord FromPolyline(Polyline pl)
    {
        var points = new List<(double X, double Y)>(pl.NumberOfVertices);
        for (int i = 0; i < pl.NumberOfVertices; i++)
        {
            var pt = pl.GetPoint2dAt(i);
            points.Add((pt.X, pt.Y));
        }

        return new RawEntityRecord(
            EntityId: pl.Handle.ToString(),
            Layer: pl.Layer,
            BlockName: null,
            Points: points,
            Closed: pl.Closed);
    }

    private static RawEntityRecord FromBlockReference(BlockReference br)
    {
        // Dynamic block ise etkin (effective) ad BlockTableRecord.Name
        // üzerinden okunur; DynamicBlockTableRecord anonim bloğu işaret eder.
        string blockName = br.IsDynamicBlock
            ? br.DynamicBlockTableRecord.GetObject(OpenMode.ForRead) is BlockTableRecord dynBtr
                ? dynBtr.Name
                : br.Name
            : br.Name;

        var origin = br.Position;

        return new RawEntityRecord(
            EntityId: br.Handle.ToString(),
            Layer: br.Layer,
            BlockName: blockName,
            Points: new List<(double X, double Y)> { (origin.X, origin.Y) },
            Closed: false);
    }

    /// <summary>
    /// Autodesk'in <see cref="UnitsValue"/>'sundan bizim bağımsız
    /// <see cref="DrawingUnit"/> enum'ımıza tek satırlık çeviri (bkz.
    /// docs/architecture.md — bilinçli olarak Autodesk tipine bağımlılık
    /// kurmuyoruz).
    /// </summary>
    private static DrawingUnit ToDrawingUnit(UnitsValue insunits) => insunits switch
    {
        UnitsValue.Millimeters => DrawingUnit.Millimeters,
        UnitsValue.Centimeters => DrawingUnit.Centimeters,
        UnitsValue.Meters => DrawingUnit.Meters,
        UnitsValue.Inches => DrawingUnit.Inches,
        UnitsValue.Feet => DrawingUnit.Feet,
        _ => DrawingUnit.Undefined,
    };
}
