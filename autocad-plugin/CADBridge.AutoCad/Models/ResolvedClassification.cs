namespace CADBridge.AutoCad.Models;

/// <summary>
/// Comparator'ın bir entity için ürettiği nihai sınıflandırma. bridge.json'a
/// serileştirme ayrı bir bileşenin işi (bkz. agents/comparator-agent.md).
/// </summary>
public sealed class ResolvedClassification
{
    public required string EntityId { get; init; }

    public required CandidateType Type { get; init; }

    public required double Confidence { get; init; }

    /// <summary>
    /// Yalnızca "görüş bildiren" ajanların katkıları (durum 4'te boş liste).
    /// </summary>
    public required IReadOnlyList<AgentContribution> Contributions { get; init; }

    public bool HasConflict { get; init; }
}
