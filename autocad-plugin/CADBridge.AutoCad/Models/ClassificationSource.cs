namespace CADBridge.AutoCad.Models;

/// <summary>
/// Bir candidate'ın hangi sınıflandırma ajanından geldiğini gösterir.
/// Enum genişletilebilir: ileride bir LLM fallback ajanı eklendiğinde
/// Comparator'ın kendisi değişmeden yeni bir kaynak eklenir (bkz.
/// agents/comparator-agent.md). Eşitlik durumunda çelişki çözümünde
/// önce gelen değer (LayerConvention, "insan niyeti taşıyan sinyal")
/// kazanır — sıralama kasıtlıdır.
/// </summary>
public enum ClassificationSource
{
    LayerConvention,
    GeometryInterpreter,
}
