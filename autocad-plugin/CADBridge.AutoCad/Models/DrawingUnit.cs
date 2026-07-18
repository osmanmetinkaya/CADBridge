namespace CADBridge.AutoCad.Models;

/// <summary>
/// Çizimin kaynak birimi. Autodesk'in <c>UnitsValue</c> enum'undan bilinçli
/// olarak bağımsız tutulur; Plugin projesi tek satırlık bir çeviriyle bu
/// enum'ı doldurur (bkz. docs/architecture.md Test edilebilirlik sınırı).
/// </summary>
public enum DrawingUnit
{
    Millimeters,
    Centimeters,
    Meters,
    Inches,
    Feet,
    Undefined,
}
