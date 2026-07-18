using CADBridge.AutoCad.Models;

namespace CADBridge.AutoCad.Classification;

/// <summary>
/// Birden çok bağımsız sınıflandırma ajanının (Layer Convention, Geometry
/// Interpreter ve ileride bir üçüncüsü) ürettiği candidate listelerini
/// entity_id bazında birleştirip her entity için tek bir nihai type ve
/// confidence üretir.
///
/// IClassificationAgent'ı implemente ETMEZ: o arayüz ham CadEntity'lerden
/// candidate üreten ajanlar içindir; Comparator zaten üretilmiş candidate
/// listelerini uzlaştırır — farklı bir sözleşme. Formüller ajan sayısından
/// bağımsız (N-ary) çalışır. Bkz. agents/comparator-agent.md.
/// </summary>
public sealed class ComparatorAgent
{
    /// <summary>Hemfikirlik (noisy-OR) sonucuna uygulanan tavan.</summary>
    private const double AgreementCeiling = 0.95;

    /// <summary>Tek-kaynak ve çelişki durumlarına uygulanan sert tavan.</summary>
    private const double WeakCeiling = 0.55;

    /// <summary>Çelişki durumunda max confidence'a uygulanan indirim katsayısı.</summary>
    private const double ConflictFactor = 0.7;

    public IReadOnlyList<ResolvedClassification> Resolve(
        IReadOnlyList<(ClassificationSource Source, IReadOnlyList<Candidate> Candidates)> agentOutputs)
    {
        // entity_id'leri ilk görülme sırasına göre topla; her entity için
        // görüş bildiren ajanların katkılarını (girdi kaynak sırasında) biriktir.
        var order = new List<string>();
        var contributionsByEntity = new Dictionary<string, List<AgentContribution>>();

        foreach (var (source, candidates) in agentOutputs)
        {
            foreach (var candidate in candidates)
            {
                if (!contributionsByEntity.TryGetValue(candidate.EntityId, out var contributions))
                {
                    contributions = new List<AgentContribution>();
                    contributionsByEntity[candidate.EntityId] = contributions;
                    order.Add(candidate.EntityId);
                }

                // Normalizasyon: Unknown veya 0.0 confidence "görüş bildirmedi" sayılır.
                if (candidate.CandidateType == CandidateType.Unknown || candidate.Confidence == 0.0)
                {
                    continue;
                }

                contributions.Add(new AgentContribution(
                    source,
                    candidate.CandidateType,
                    candidate.Confidence,
                    candidate.MatchedRule));
            }
        }

        return order
            .Select(entityId => ResolveEntity(entityId, contributionsByEntity[entityId]))
            .ToList();
    }

    private static ResolvedClassification ResolveEntity(string entityId, List<AgentContribution> contributions)
    {
        // Durum 4 — hiçbiri görüş bildirmedi.
        if (contributions.Count == 0)
        {
            return new ResolvedClassification
            {
                EntityId = entityId,
                Type = CandidateType.Unknown,
                Confidence = 0.0,
                Contributions = Array.Empty<AgentContribution>(),
                HasConflict = false,
            };
        }

        // Durum 3 — tek kaynak.
        if (contributions.Count == 1)
        {
            var only = contributions[0];
            return new ResolvedClassification
            {
                EntityId = entityId,
                Type = only.CandidateType,
                Confidence = Math.Min(only.Confidence, WeakCeiling),
                Contributions = contributions,
                HasConflict = false,
            };
        }

        var distinctTypes = contributions.Select(c => c.CandidateType).Distinct().Count();

        // Durum 1 — hemfikir (2+ ajan aynı tipte).
        if (distinctTypes == 1)
        {
            var noisyOr = 1.0 - contributions.Aggregate(1.0, (product, c) => product * (1.0 - c.Confidence));
            return new ResolvedClassification
            {
                EntityId = entityId,
                Type = contributions[0].CandidateType,
                Confidence = Math.Min(noisyOr, AgreementCeiling),
                Contributions = contributions,
                HasConflict = false,
            };
        }

        // Durum 2 — çelişki (2+ ajan farklı tiplerde).
        var maxConfidence = contributions.Max(c => c.Confidence);

        // argmax: en yüksek confidence; eşitlikte ClassificationSource sırası
        // (LayerConvention önce) kazanır.
        var winner = contributions
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.Source)
            .First();

        return new ResolvedClassification
        {
            EntityId = entityId,
            Type = winner.CandidateType,
            Confidence = Math.Min(ConflictFactor * maxConfidence, WeakCeiling),
            Contributions = contributions,
            HasConflict = true,
        };
    }
}
