using CADBridge.AutoCad.Extraction;
using CADBridge.AutoCad.Models;
using Xunit;

namespace CADBridge.AutoCad.Tests;

public class CadEntityFactoryTests
{
    private const int Precision = 6;

    private static RawDrawingData Drawing(DrawingUnit units, params RawEntityRecord[] entities) => new()
    {
        SourceFile = "plan.dwg",
        Units = units,
        Entities = entities,
    };

    private static RawEntityRecord Record(
        string id,
        IReadOnlyList<(double X, double Y)> points,
        string layer = "A-WALL",
        string? blockName = null,
        bool closed = true) => new(id, layer, blockName, points, closed);

    [Fact]
    public void Create_Meters_ConvertsPointsToMillimeters()
    {
        var data = Drawing(
            DrawingUnit.Meters,
            Record("E1", new (double, double)[] { (0, 0), (1, 0), (1, 2), (0, 2) }));

        var (entities, warning) = CadEntityFactory.Create(data);

        var entity = Assert.Single(entities);
        Assert.Null(warning);
        Assert.Collection(
            entity.Points,
            p => AssertPoint(0, 0, p),
            p => AssertPoint(1000, 0, p),
            p => AssertPoint(1000, 2000, p),
            p => AssertPoint(0, 2000, p));
    }

    [Fact]
    public void Create_Millimeters_LeavesPointsUnchanged()
    {
        var data = Drawing(
            DrawingUnit.Millimeters,
            Record("E1", new (double, double)[] { (0, 0), (4000, 180) }));

        var (entities, warning) = CadEntityFactory.Create(data);

        Assert.Null(warning);
        AssertPoint(0, 0, entities[0].Points[0]);
        AssertPoint(4000, 180, entities[0].Points[1]);
    }

    [Fact]
    public void Create_Inches_ScalesBy254()
    {
        var data = Drawing(
            DrawingUnit.Inches,
            Record("E1", new (double, double)[] { (0, 0), (10, 0) }));

        var (entities, _) = CadEntityFactory.Create(data);

        AssertPoint(254, 0, entities[0].Points[1]);
    }

    [Fact]
    public void Create_Undefined_PropagatesWarning()
    {
        var data = Drawing(
            DrawingUnit.Undefined,
            Record("E1", new (double, double)[] { (0, 0), (5, 5) }));

        var (entities, warning) = CadEntityFactory.Create(data);

        Assert.Equal(UnitNormalizer.UndefinedWarning, warning);
        // Undefined mm varsayar → ölçek 1.0.
        AssertPoint(5, 5, entities[0].Points[1]);
    }

    [Fact]
    public void Create_PreservesMetadata()
    {
        var data = Drawing(
            DrawingUnit.Meters,
            Record("E7", new (double, double)[] { (0, 0) }, layer: "0-DUVAR", blockName: "KAPI-90", closed: false));

        var (entities, _) = CadEntityFactory.Create(data);

        var entity = entities[0];
        Assert.Equal("E7", entity.EntityId);
        Assert.Equal("0-DUVAR", entity.Layer);
        Assert.Equal("KAPI-90", entity.BlockName);
        Assert.False(entity.Closed);
    }

    private static void AssertPoint(double expectedX, double expectedY, (double X, double Y) actual)
    {
        Assert.Equal(expectedX, actual.X, Precision);
        Assert.Equal(expectedY, actual.Y, Precision);
    }
}
