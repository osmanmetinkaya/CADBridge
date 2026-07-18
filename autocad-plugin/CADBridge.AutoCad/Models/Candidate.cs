namespace CADBridge.AutoCad.Models;

public enum CandidateType
{
    Unknown,
    Wall,
    Door,
    Window,
    Floor,
}

/// <summary>
/// Layer Convention Agent ve Geometry Interpreter Agent'ın Comparator'a
/// verdiği ortak ara çıktı şeması (bkz. docs/api-research.md).
/// </summary>
public sealed class Candidate
{
    public required string EntityId { get; init; }

    public required CandidateType CandidateType { get; init; }

    public required double Confidence { get; init; }

    public required string MatchedRule { get; init; }
}
