using CADBridge.AutoCad.Models;

namespace CADBridge.AutoCad.Extraction;

/// <summary>
/// Çizim birimini milimetre ölçek faktörüne çeviren saf fonksiyon. Ajanlar
/// her zaman mm normalize edilmiş geometri üzerinde çalışır; birim varsayımı
/// yapılmaz, her seferinde tespit edilir (bkz. docs/architecture.md).
/// </summary>
public static class UnitNormalizer
{
    /// <summary>
    /// <see cref="DrawingUnit.Undefined"/> için üretilen uyarı metni; mm
    /// varsayıldığını ama doğrulanmadığını belirtir (bridge.json'a düşer).
    /// </summary>
    public const string UndefinedWarning = "INSUNITS tanımsız; mm varsayıldı";

    /// <summary>
    /// Verilen birimi mm'ye çevirmek için kullanılacak ölçek faktörünü ve —
    /// varsa — bir uyarı döner. <see cref="DrawingUnit.Undefined"/> dışında
    /// <c>Warning</c> her zaman <c>null</c>'dır.
    /// </summary>
    public static (double Scale, string? Warning) ToMillimeters(DrawingUnit unit) => unit switch
    {
        DrawingUnit.Millimeters => (1.0, null),
        DrawingUnit.Centimeters => (10.0, null),
        DrawingUnit.Meters => (1000.0, null),
        DrawingUnit.Inches => (25.4, null),
        DrawingUnit.Feet => (304.8, null),
        DrawingUnit.Undefined => (1.0, UndefinedWarning),
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Bilinmeyen DrawingUnit."),
    };
}
