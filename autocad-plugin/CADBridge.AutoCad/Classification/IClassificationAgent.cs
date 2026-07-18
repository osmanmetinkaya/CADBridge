using CADBridge.AutoCad.Models;

namespace CADBridge.AutoCad.Classification;

/// <summary>
/// Layer Convention Agent ve (ileride) Geometry Interpreter Agent'ın
/// uyguladığı ortak arayüz. Comparator, her iki ajanı da bu arayüz
/// üzerinden simetrik şekilde çağırır.
/// </summary>
public interface IClassificationAgent
{
    IReadOnlyList<Candidate> Classify(IEnumerable<CadEntity> entities);
}
