using System.Text.Json;
using CADBridge.AutoCad.Models;

namespace CADBridge.AutoCad.Output;

/// <summary>
/// Nihai sınıflandırmaları ve geometriyi <c>bridge.json</c> (v0.1)
/// şemasına serileştirir (tam şema: docs/api-research.md). Yalnızca JSON
/// string üretir — dosyaya yazmak çağıranın sorumluluğudur.
/// </summary>
public sealed class BridgeJsonWriter
{
    private const string BridgeVersion = "0.1";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// bridge.json string'ini üretir. <paramref name="entities"/> ve
    /// <paramref name="classifications"/> <c>EntityId</c> üzerinden eşleştirilir;
    /// bir sınıflandırmaya karşılık gelen geometri bulunamazsa hata fırlatılır.
    /// <paramref name="generatedAt"/> deterministik test için dışarıdan alınır.
    /// </summary>
    public string Write(
        IReadOnlyList<CadEntity> entities,
        IReadOnlyList<ResolvedClassification> classifications,
        string sourceFile,
        DateTimeOffset generatedAt,
        string? unitWarning)
    {
        var entitiesById = entities.ToDictionary(e => e.EntityId);

        var document = new Dictionary<string, object?>
        {
            ["meta"] = new Dictionary<string, object?>
            {
                ["source_file"] = sourceFile,
                ["units"] = "mm",
                ["unit_warning"] = unitWarning,
                ["generated_at"] = generatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["bridge_version"] = BridgeVersion,
            },
            ["entities"] = classifications
                .Select(classification => BuildEntity(classification, entitiesById))
                .ToList(),
        };

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    private static Dictionary<string, object?> BuildEntity(
        ResolvedClassification classification,
        IReadOnlyDictionary<string, CadEntity> entitiesById)
    {
        if (!entitiesById.TryGetValue(classification.EntityId, out var entity))
        {
            throw new InvalidOperationException(
                $"'{classification.EntityId}' sınıflandırmasına karşılık gelen geometri (CadEntity) bulunamadı.");
        }

        return new Dictionary<string, object?>
        {
            ["entity_id"] = classification.EntityId,
            ["type"] = TypeToString(classification.Type),
            ["confidence"] = classification.Confidence,
            ["source"] = classification.Contributions.Select(c => SourceToString(c.Source)).ToList(),
            ["matched_rule"] = classification.Contributions.Select(c => c.MatchedRule).ToList(),
            ["layer"] = entity.Layer,
            ["geometry"] = new Dictionary<string, object?>
            {
                ["kind"] = "polyline",
                ["points"] = entity.Points.Select(p => new[] { p.X, p.Y }).ToList(),
                ["closed"] = entity.Closed,
            },
            ["attributes"] = BuildAttributes(classification),
        };
    }

    private static Dictionary<string, object?> BuildAttributes(ResolvedClassification classification)
    {
        // MVP: yalnızca çelişki işaretlenir; thickness_mm gibi alanlar bilinçli
        // olarak dışarıda bırakıldı (bkz. docs/architecture.md Açık Sorular).
        var attributes = new Dictionary<string, object?>();
        if (classification.HasConflict)
        {
            attributes["conflict"] = true;
        }

        return attributes;
    }

    /// <summary>
    /// <see cref="CandidateType"/>'ı küçük harf string'e çevirir
    /// (<c>Wall</c> → <c>"wall"</c>). Enum isimleri zaten hedef biçime uygun.
    /// </summary>
    private static string TypeToString(CandidateType type) => type.ToString().ToLowerInvariant();

    /// <summary>
    /// <see cref="ClassificationSource"/>'ı snake_case string'e çevirir
    /// (<c>LayerConvention</c> → <c>"layer_convention"</c>).
    /// </summary>
    private static string SourceToString(ClassificationSource source) => source switch
    {
        ClassificationSource.LayerConvention => "layer_convention",
        ClassificationSource.GeometryInterpreter => "geometry_interpreter",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Bilinmeyen ClassificationSource."),
    };
}
