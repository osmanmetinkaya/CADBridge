using CADBridge.AutoCad.Classification;
using CADBridge.AutoCad.Models;
using Xunit;

namespace CADBridge.AutoCad.Tests;

public class LayerConventionAgentTests
{
    private readonly LayerConventionAgent _agent = new();

    private static CadEntity Entity(string layer, string? blockName = null) => new()
    {
        EntityId = "E-TEST",
        Layer = layer,
        BlockName = blockName,
    };

    [Theory]
    [InlineData("A-WALL")]
    [InlineData("DUVAR")]
    public void ExactLayerToken_ClassifiesAsWallWithHighConfidence(string layer)
    {
        var result = _agent.Classify(new[] { Entity(layer) }).Single();

        Assert.Equal(CandidateType.Wall, result.CandidateType);
        Assert.Equal(0.85, result.Confidence);
        Assert.StartsWith("layer_regex:", result.MatchedRule);
    }

    [Fact]
    public void TurkishPrefixedLayerName_ClassifiesAsWall()
    {
        var result = _agent.Classify(new[] { Entity("0-MIMARI-DUVAR-IC") }).Single();

        Assert.Equal(CandidateType.Wall, result.CandidateType);
        Assert.Equal(0.85, result.Confidence);
        Assert.Equal("layer_regex:DUVAR", result.MatchedRule);
    }

    [Fact]
    public void KeywordEmbeddedInUnrelatedLayerName_ClassifiesWithLowerConfidence()
    {
        var result = _agent.Classify(new[] { Entity("kat1_ictoduvar_v2_final") }).Single();

        Assert.Equal(CandidateType.Wall, result.CandidateType);
        Assert.Equal(0.55, result.Confidence);
        Assert.Equal("layer_regex:DUVAR", result.MatchedRule);
    }

    [Fact]
    public void DoorBlockName_ClassifiesAsDoorFromBlockNameEvenWithUnrelatedLayer()
    {
        var result = _agent.Classify(new[] { Entity("A-ANNO-MISC", blockName: "DOOR_SINGLE_900") }).Single();

        Assert.Equal(CandidateType.Door, result.CandidateType);
        Assert.Equal(0.75, result.Confidence);
        Assert.Equal("block_name_regex:DOOR", result.MatchedRule);
    }

    [Fact]
    public void WindowBlockName_TurkishNaming_ClassifiesAsWindow()
    {
        var result = _agent.Classify(new[] { Entity("A-ANNO-MISC", blockName: "PENCERE_1200x1500") }).Single();

        Assert.Equal(CandidateType.Window, result.CandidateType);
        Assert.Equal(0.75, result.Confidence);
        Assert.Equal("block_name_regex:PENCERE", result.MatchedRule);
    }

    [Fact]
    public void UnrecognizedLayerName_ClassifiesAsUnknownWithZeroConfidence()
    {
        var result = _agent.Classify(new[] { Entity("Layer1") }).Single();

        Assert.Equal(CandidateType.Unknown, result.CandidateType);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal("no_match", result.MatchedRule);
    }

    [Fact]
    public void EmptyLayerName_ClassifiesAsUnknown()
    {
        var result = _agent.Classify(new[] { Entity("") }).Single();

        Assert.Equal(CandidateType.Unknown, result.CandidateType);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal("no_match", result.MatchedRule);
    }

    [Fact]
    public void ConflictingExactTokensInSameLayerName_ClassifiesAsUnknown()
    {
        // "DUVAR-KAPI" hem duvar hem kapı için aynı confidence'ta tam
        // eşleşme üretir; bu ajan çelişkiyi kendi başına çözmez.
        var result = _agent.Classify(new[] { Entity("DUVAR-KAPI") }).Single();

        Assert.Equal(CandidateType.Unknown, result.CandidateType);
        Assert.Equal("ambiguous_match", result.MatchedRule);
    }

    [Fact]
    public void FloorLayerName_EnglishAndTurkishVariants_ClassifyAsFloor()
    {
        var results = _agent.Classify(new[] { Entity("A-FLOOR"), Entity("ZEMIN"), Entity("DOSEME") });

        Assert.All(results, r => Assert.Equal(CandidateType.Floor, r.CandidateType));
    }
}
