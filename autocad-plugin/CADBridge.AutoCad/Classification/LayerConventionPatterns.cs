using System.Text.RegularExpressions;
using CADBridge.AutoCad.Models;

namespace CADBridge.AutoCad.Classification;

/// <summary>
/// İngilizce ve Türkçe mimari çizim konvansiyonlarına göre layer/blok adı
/// kalıp kütüphanesi. Bkz. agents/layer-convention-agent.md.
/// </summary>
public static class LayerConventionPatterns
{
    public sealed record Pattern(CandidateType Type, Regex Regex);

    /// <summary>
    /// Sıra önemli değil; her kalıp bağımsız denenir ve en yüksek
    /// confidence'lı eşleşme kazanır (bkz. LayerConventionAgent).
    /// </summary>
    public static readonly IReadOnlyList<Pattern> Patterns = new[]
    {
        new Pattern(CandidateType.Wall, Compile("WALL|DUVAR")),
        new Pattern(CandidateType.Door, Compile("DOOR|KAPI")),
        new Pattern(CandidateType.Window, Compile("WINDOW|WIN|GLAZ|PENCERE")),
        new Pattern(CandidateType.Floor, Compile("FLOOR|SLAB|ZEMIN|DOSEME|DÖŞEME")),
    };

    private static Regex Compile(string alternation) =>
        new(alternation, RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
