using CADBridge.AutoCad.Classification;
using CADBridge.AutoCad.Models;
using Xunit;

namespace CADBridge.AutoCad.Tests;

public class ComparatorAgentTests
{
    private const int Precision = 3;

    private readonly ComparatorAgent _comparator = new();

    private static Candidate Candidate(
        string entityId,
        CandidateType type,
        double confidence,
        string matchedRule = "rule") => new()
    {
        EntityId = entityId,
        CandidateType = type,
        Confidence = confidence,
        MatchedRule = matchedRule,
    };

    private ResolvedClassification ResolveSingle(Candidate layer, Candidate geometry) =>
        _comparator.Resolve(new (ClassificationSource, IReadOnlyList<Candidate>)[]
        {
            (ClassificationSource.LayerConvention, new[] { layer }),
            (ClassificationSource.GeometryInterpreter, new[] { geometry }),
        }).Single();

    [Fact]
    public void Agreement_HighConfidences_CapsAt095()
    {
        var result = ResolveSingle(
            Candidate("E1", CandidateType.Wall, 0.85),
            Candidate("E1", CandidateType.Wall, 0.8));

        Assert.Equal(CandidateType.Wall, result.Type);
        Assert.Equal(0.95, result.Confidence, Precision);
        Assert.False(result.HasConflict);
        Assert.Equal(2, result.Contributions.Count);
    }

    [Fact]
    public void Agreement_WeakestPair_ProducesNoisyOr0775()
    {
        var result = ResolveSingle(
            Candidate("E1", CandidateType.Wall, 0.55),
            Candidate("E1", CandidateType.Wall, 0.5));

        Assert.Equal(CandidateType.Wall, result.Type);
        Assert.Equal(0.775, result.Confidence, Precision);
        Assert.False(result.HasConflict);
        Assert.Equal(2, result.Contributions.Count);
    }

    [Fact]
    public void SingleSource_LayerOnly_CapsAt055()
    {
        var result = ResolveSingle(
            Candidate("E1", CandidateType.Wall, 0.85),
            Candidate("E1", CandidateType.Unknown, 0.0, "no_match"));

        Assert.Equal(CandidateType.Wall, result.Type);
        Assert.Equal(0.55, result.Confidence, Precision);
        Assert.False(result.HasConflict);
        Assert.Single(result.Contributions);
        Assert.Equal(ClassificationSource.LayerConvention, result.Contributions[0].Source);
    }

    [Fact]
    public void SingleSource_ZeroConfidenceCountsAsNoOpinion()
    {
        // Geometry Unkn/0 elense de Layer 0.0 confidence ile de görüş bildirmemiş sayılır.
        var result = ResolveSingle(
            Candidate("E1", CandidateType.Wall, 0.4),
            Candidate("E1", CandidateType.Door, 0.0, "abstained"));

        Assert.Equal(CandidateType.Wall, result.Type);
        Assert.Single(result.Contributions);
        Assert.False(result.HasConflict);
    }

    [Fact]
    public void Conflict_LayerWinsByHigherConfidence()
    {
        var result = ResolveSingle(
            Candidate("E1", CandidateType.Wall, 0.85, "layer_regex:DUVAR"),
            Candidate("E1", CandidateType.Door, 0.8, "opening_in_wall"));

        Assert.Equal(CandidateType.Wall, result.Type);
        // min(0.7 * 0.85, 0.55) = min(0.595, 0.55) = 0.55
        Assert.Equal(0.55, result.Confidence, Precision);
        Assert.True(result.HasConflict);
        Assert.Equal(2, result.Contributions.Count);

        var winner = Assert.Single(result.Contributions, c => c.Source == ClassificationSource.LayerConvention);
        Assert.Equal(CandidateType.Wall, winner.CandidateType);
        Assert.Equal(0.85, winner.Confidence, Precision);
        Assert.Equal("layer_regex:DUVAR", winner.MatchedRule);

        var loser = Assert.Single(result.Contributions, c => c.Source == ClassificationSource.GeometryInterpreter);
        Assert.Equal(CandidateType.Door, loser.CandidateType);
        Assert.Equal(0.8, loser.Confidence, Precision);
        Assert.Equal("opening_in_wall", loser.MatchedRule);
    }

    [Fact]
    public void Conflict_GeometryWinsByHigherConfidence()
    {
        var result = ResolveSingle(
            Candidate("E1", CandidateType.Door, 0.55),
            Candidate("E1", CandidateType.Wall, 0.8));

        Assert.Equal(CandidateType.Wall, result.Type);
        // min(0.7 * 0.8, 0.55) = min(0.56, 0.55) = 0.55
        Assert.Equal(0.55, result.Confidence, Precision);
        Assert.True(result.HasConflict);
        Assert.Equal(2, result.Contributions.Count);
    }

    [Fact]
    public void Conflict_EqualConfidence_LayerConventionWins()
    {
        var result = ResolveSingle(
            Candidate("E1", CandidateType.Wall, 0.7),
            Candidate("E1", CandidateType.Door, 0.7));

        Assert.Equal(CandidateType.Wall, result.Type);
        Assert.Equal(0.49, result.Confidence, Precision); // min(0.7*0.7, 0.55) = 0.49
        Assert.True(result.HasConflict);
    }

    [Fact]
    public void NoOpinion_BothAbstain_ProducesUnknown()
    {
        var result = ResolveSingle(
            Candidate("E1", CandidateType.Unknown, 0.0, "no_match"),
            Candidate("E1", CandidateType.Unknown, 0.0, "no_match"));

        Assert.Equal(CandidateType.Unknown, result.Type);
        Assert.Equal(0.0, result.Confidence, Precision);
        Assert.Empty(result.Contributions);
        Assert.False(result.HasConflict);
    }

    [Fact]
    public void Nary_ThreeAgreeingSources_UsesFullNoisyOr()
    {
        // N-ary genelleşme: 3 kaynak da hemfikir.
        var result = _comparator.Resolve(new (ClassificationSource, IReadOnlyList<Candidate>)[]
        {
            (ClassificationSource.LayerConvention, new[] { Candidate("E1", CandidateType.Wall, 0.5) }),
            (ClassificationSource.GeometryInterpreter, new[] { Candidate("E1", CandidateType.Wall, 0.5) }),
            // Üçüncü kaynağı da GeometryInterpreter olarak etiketliyoruz (enum bugün 2 değerli);
            // Comparator kaynak sayısından bağımsız çalıştığı için 3 katkı da birleşir.
            (ClassificationSource.GeometryInterpreter, new[] { Candidate("E1", CandidateType.Wall, 0.5) }),
        }).Single();

        // 1 - (0.5)^3 = 0.875
        Assert.Equal(CandidateType.Wall, result.Type);
        Assert.Equal(0.875, result.Confidence, Precision);
        Assert.False(result.HasConflict);
        Assert.Equal(3, result.Contributions.Count);
    }

    [Fact]
    public void MultipleEntities_EachResolvedIndependently()
    {
        var results = _comparator.Resolve(new (ClassificationSource, IReadOnlyList<Candidate>)[]
        {
            (ClassificationSource.LayerConvention, new[]
            {
                Candidate("E1", CandidateType.Wall, 0.85),      // agreement
                Candidate("E2", CandidateType.Wall, 0.85),      // single source
                Candidate("E3", CandidateType.Door, 0.55),      // conflict
            }),
            (ClassificationSource.GeometryInterpreter, new[]
            {
                Candidate("E1", CandidateType.Wall, 0.8),       // agreement
                Candidate("E2", CandidateType.Unknown, 0.0),    // abstains
                Candidate("E3", CandidateType.Wall, 0.8),       // conflict
            }),
        });

        Assert.Equal(3, results.Count);

        var e1 = results.Single(r => r.EntityId == "E1");
        Assert.Equal(CandidateType.Wall, e1.Type);
        Assert.Equal(0.95, e1.Confidence, Precision);
        Assert.False(e1.HasConflict);
        Assert.Equal(2, e1.Contributions.Count);

        var e2 = results.Single(r => r.EntityId == "E2");
        Assert.Equal(CandidateType.Wall, e2.Type);
        Assert.Equal(0.55, e2.Confidence, Precision);
        Assert.False(e2.HasConflict);
        Assert.Single(e2.Contributions);

        var e3 = results.Single(r => r.EntityId == "E3");
        Assert.Equal(CandidateType.Wall, e3.Type);
        Assert.Equal(0.55, e3.Confidence, Precision);
        Assert.True(e3.HasConflict);
        Assert.Equal(2, e3.Contributions.Count);
    }
}
