namespace CADBridge.AutoCad.Models;

/// <summary>
/// Bir entity için "görüş bildiren" tek bir ajanın Comparator'a taşıdığı
/// katkı. Çelişki durumunda kaybeden ajanın katkısı da saklanır (hangi
/// tipte ne kadar güvenle görüş bildirdiği bilgisiyle). Bkz.
/// agents/comparator-agent.md.
/// </summary>
public sealed record AgentContribution(
    ClassificationSource Source,
    CandidateType CandidateType,
    double Confidence,
    string MatchedRule);
