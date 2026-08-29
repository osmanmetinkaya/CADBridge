using CADBridge.AutoCad.Classification;
using CADBridge.AutoCad.Extraction;
using CADBridge.AutoCad.Models;
using CADBridge.AutoCad.Output;

namespace CADBridge.AutoCad.Pipeline;

/// <summary>
/// Uçtan uca sınıflandırma pipeline'ını bir araya getirir: ham çizim
/// verisini mm'ye normalize eder, iki bağımsız ajanı çalıştırır, sonuçları
/// Comparator ile uzlaştırır ve bridge.json string'i üretir. Tamamen
/// POCO/testable — AutoCAD API'sine bağımlı değildir.
/// </summary>
public sealed class Orchestrator
{
    private readonly IClassificationAgent _layerConventionAgent;
    private readonly IClassificationAgent _geometryInterpreterAgent;
    private readonly ComparatorAgent _comparator;
    private readonly BridgeJsonWriter _writer;

    /// <summary>
    /// Tüm bileşenler stateless olduğu için varsayılan yapılandırmayı kullanır.
    /// </summary>
    public Orchestrator()
        : this(
            new LayerConventionAgent(),
            new GeometryInterpreterAgent(),
            new ComparatorAgent(),
            new BridgeJsonWriter())
    {
    }

    /// <summary>Bağımlılıkların dışarıdan verilebildiği yapıcı (test/genişletme).</summary>
    public Orchestrator(
        IClassificationAgent layerConventionAgent,
        IClassificationAgent geometryInterpreterAgent,
        ComparatorAgent comparator,
        BridgeJsonWriter writer)
    {
        _layerConventionAgent = layerConventionAgent;
        _geometryInterpreterAgent = geometryInterpreterAgent;
        _comparator = comparator;
        _writer = writer;
    }

    /// <summary>
    /// Pipeline'ı çalıştırır ve bridge.json string'i döner.
    /// <paramref name="generatedAt"/> deterministik çıktı için çağıran
    /// tarafından verilir (<c>DateTimeOffset.UtcNow</c> kullanılmaz).
    /// </summary>
    public string Run(RawDrawingData drawingData, DateTimeOffset generatedAt)
    {
        var (entities, unitWarning) = CadEntityFactory.Create(drawingData);

        var layerCandidates = _layerConventionAgent.Classify(entities);
        var geometryCandidates = _geometryInterpreterAgent.Classify(entities);

        var resolved = _comparator.Resolve(new (ClassificationSource, IReadOnlyList<Candidate>)[]
        {
            (ClassificationSource.LayerConvention, layerCandidates),
            (ClassificationSource.GeometryInterpreter, geometryCandidates),
        });

        return _writer.Write(entities, resolved, drawingData.SourceFile, generatedAt, unitWarning);
    }
}
