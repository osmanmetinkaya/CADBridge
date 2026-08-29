using System.Text.RegularExpressions;
using CADBridge.AutoCad.Models;

namespace CADBridge.AutoCad.Classification;

/// <summary>
/// Layer/blok adına dayalı kural tabanlı sınıflandırma. Geometriye
/// bakmaz. Bkz. agents/layer-convention-agent.md.
/// </summary>
public sealed class LayerConventionAgent : IClassificationAgent
{
    private const double ExactMatchConfidence = 0.85;
    private const double PartialMatchConfidence = 0.55;
    private const double BlockNameMatchConfidence = 0.75;

    public IReadOnlyList<Candidate> Classify(IEnumerable<CadEntity> entities) =>
        entities.Select(ClassifyEntity).ToList();

    private static Candidate ClassifyEntity(CadEntity entity)
    {
        var best = FindBestMatch(entity);

        return new Candidate
        {
            EntityId = entity.EntityId,
            CandidateType = best?.Type ?? CandidateType.Unknown,
            Confidence = best?.Confidence ?? 0.0,
            MatchedRule = best?.MatchedRule ?? "no_match",
        };
    }

    private static (CandidateType Type, double Confidence, string MatchedRule)? FindBestMatch(CadEntity entity)
    {
        var matches = new List<(CandidateType Type, double Confidence, string MatchedRule)>();

        foreach (var token in Tokenize(entity.Layer))
        {
            foreach (var pattern in LayerConventionPatterns.Patterns)
            {
                var match = pattern.Regex.Match(token);
                if (!match.Success)
                {
                    continue;
                }

                var isWholeTokenMatch = match.Length == token.Length;
                var confidence = isWholeTokenMatch ? ExactMatchConfidence : PartialMatchConfidence;
                matches.Add((pattern.Type, confidence, $"layer_regex:{match.Value.ToUpperInvariant()}"));
            }
        }

        if (!string.IsNullOrWhiteSpace(entity.BlockName))
        {
            foreach (var pattern in LayerConventionPatterns.Patterns)
            {
                var match = pattern.Regex.Match(entity.BlockName);
                if (match.Success)
                {
                    matches.Add((pattern.Type, BlockNameMatchConfidence, $"block_name_regex:{match.Value.ToUpperInvariant()}"));
                }
            }
        }

        if (matches.Count == 0)
        {
            return null;
        }

        var maxConfidence = matches.Max(m => m.Confidence);
        var topTypes = matches.Where(m => m.Confidence == maxConfidence).Select(m => m.Type).Distinct().ToList();

        // Aynı confidence'ta birden fazla farklı tip eşleştiyse, bu ajan
        // çelişkiyi kendi içinde çözmez — unknown döner (bkz.
        // agents/layer-convention-agent.md).
        if (topTypes.Count > 1)
        {
            return (CandidateType.Unknown, 0.0, "ambiguous_match");
        }

        return matches.First(m => m.Confidence == maxConfidence);
    }

    private static IEnumerable<string> Tokenize(string value) =>
        Regex.Split(value, @"[^\p{L}0-9]+").Where(token => token.Length > 0);
}
