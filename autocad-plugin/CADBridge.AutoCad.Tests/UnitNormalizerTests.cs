using CADBridge.AutoCad.Extraction;
using CADBridge.AutoCad.Models;
using Xunit;

namespace CADBridge.AutoCad.Tests;

public class UnitNormalizerTests
{
    private const int Precision = 6;

    [Theory]
    [InlineData(DrawingUnit.Millimeters, 1.0)]
    [InlineData(DrawingUnit.Centimeters, 10.0)]
    [InlineData(DrawingUnit.Meters, 1000.0)]
    [InlineData(DrawingUnit.Inches, 25.4)]
    [InlineData(DrawingUnit.Feet, 304.8)]
    [InlineData(DrawingUnit.Undefined, 1.0)]
    public void ToMillimeters_ReturnsExpectedScale(DrawingUnit unit, double expectedScale)
    {
        var (scale, _) = UnitNormalizer.ToMillimeters(unit);

        Assert.Equal(expectedScale, scale, Precision);
    }

    [Theory]
    [InlineData(DrawingUnit.Millimeters)]
    [InlineData(DrawingUnit.Centimeters)]
    [InlineData(DrawingUnit.Meters)]
    [InlineData(DrawingUnit.Inches)]
    [InlineData(DrawingUnit.Feet)]
    public void ToMillimeters_DefinedUnits_HaveNoWarning(DrawingUnit unit)
    {
        var (_, warning) = UnitNormalizer.ToMillimeters(unit);

        Assert.Null(warning);
    }

    [Fact]
    public void ToMillimeters_Undefined_AssumesMmAndWarns()
    {
        var (scale, warning) = UnitNormalizer.ToMillimeters(DrawingUnit.Undefined);

        Assert.Equal(1.0, scale, Precision);
        Assert.Equal(UnitNormalizer.UndefinedWarning, warning);
        Assert.NotNull(warning);
    }
}
