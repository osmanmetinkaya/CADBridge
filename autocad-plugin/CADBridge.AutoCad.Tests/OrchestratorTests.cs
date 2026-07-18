using System.Text.Json;
using CADBridge.AutoCad.Extraction;
using CADBridge.AutoCad.Models;
using CADBridge.AutoCad.Pipeline;
using Xunit;

namespace CADBridge.AutoCad.Tests;

public class OrchestratorTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly Orchestrator _orchestrator = new();

    /// <summary>
    /// Sentetik bir çizim: metre biriminde, üç entity —
    /// (1) A-WALL layer'lı ince kapalı dikdörtgen (duvar; iki ajan hemfikir),
    /// (2) tanınmayan bir layer'lı açık kısa polyline (unknown),
    /// (3) A-WINDOW layer'lı ama geometrisi eşleşmeyen entity (tek kaynak).
    /// </summary>
    private static RawDrawingData SampleDrawing() => new()
    {
        SourceFile = "ornek-plan.dwg",
        Units = DrawingUnit.Meters,
        Entities = new[]
        {
            new RawEntityRecord(
                "E-WALL",
                "A-WALL",
                null,
                new (double, double)[] { (0, 0), (4, 0), (4, 0.18), (0, 0.18) },
                Closed: true),
            new RawEntityRecord(
                "E-UNKNOWN",
                "Layer1",
                null,
                new (double, double)[] { (0, 0), (1, 1) },
                Closed: false),
            new RawEntityRecord(
                "E-SINGLE",
                "A-WINDOW",
                null,
                new (double, double)[] { (10, 10), (11, 11) },
                Closed: false),
        },
    };

    [Fact]
    public void Run_ProducesParseableJsonWithMeta()
    {
        var json = _orchestrator.Run(SampleDrawing(), FixedTime);

        using var doc = JsonDocument.Parse(json);
        var meta = doc.RootElement.GetProperty("meta");

        Assert.Equal("ornek-plan.dwg", meta.GetProperty("source_file").GetString());
        Assert.Equal("mm", meta.GetProperty("units").GetString());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("unit_warning").ValueKind);
        Assert.Equal("2026-07-18T12:00:00Z", meta.GetProperty("generated_at").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("entities").GetArrayLength());
    }

    [Fact]
    public void Run_WallEntity_IsWallWithHighAgreedConfidence()
    {
        var json = _orchestrator.Run(SampleDrawing(), FixedTime);

        var wall = FindEntity(json, "E-WALL");

        Assert.Equal("wall", wall.GetProperty("type").GetString());
        // İki ajan hemfikir → 0.6 review eşiğinin üstünde (sistem değişmezi).
        Assert.True(wall.GetProperty("confidence").GetDouble() > 0.6);
        var sources = wall.GetProperty("source").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new[] { "layer_convention", "geometry_interpreter" }, sources);
        // Metre → mm normalizasyonu uçtan uca korunmuş olmalı (0.18m = 180mm).
        var points = wall.GetProperty("geometry").GetProperty("points");
        Assert.Equal(180, points[2][1].GetDouble(), 6);
        Assert.Empty(wall.GetProperty("attributes").EnumerateObject());
    }

    [Fact]
    public void Run_UnrecognizedEntity_IsUnknownWithZeroConfidence()
    {
        var json = _orchestrator.Run(SampleDrawing(), FixedTime);

        var unknown = FindEntity(json, "E-UNKNOWN");

        Assert.Equal("unknown", unknown.GetProperty("type").GetString());
        Assert.Equal(0.0, unknown.GetProperty("confidence").GetDouble(), 6);
        Assert.Empty(unknown.GetProperty("source").EnumerateArray());
    }

    [Fact]
    public void Run_SingleSourceEntity_IsCappedBelowReviewThreshold()
    {
        var json = _orchestrator.Run(SampleDrawing(), FixedTime);

        var single = FindEntity(json, "E-SINGLE");

        Assert.Equal("window", single.GetProperty("type").GetString());
        // Tek kaynak → 0.55 tavanı, 0.6 review eşiğinin altında kalır.
        Assert.True(single.GetProperty("confidence").GetDouble() < 0.6);
        var sources = single.GetProperty("source").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new[] { "layer_convention" }, sources);
    }

    [Fact]
    public void Run_UndefinedUnits_EmitsUnitWarning()
    {
        var drawing = new RawDrawingData
        {
            SourceFile = "belirsiz.dwg",
            Units = DrawingUnit.Undefined,
            Entities = new[]
            {
                new RawEntityRecord("E1", "A-WALL", null, new (double, double)[] { (0, 0), (1, 1) }, Closed: false),
            },
        };

        var json = _orchestrator.Run(drawing, FixedTime);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(
            UnitNormalizer.UndefinedWarning,
            doc.RootElement.GetProperty("meta").GetProperty("unit_warning").GetString());
    }

    private static JsonElement FindEntity(string json, string entityId)
    {
        using var doc = JsonDocument.Parse(json);
        var match = doc.RootElement.GetProperty("entities")
            .EnumerateArray()
            .First(e => e.GetProperty("entity_id").GetString() == entityId);

        // JsonElement, JsonDocument dispose edilince geçersizleşir; klonla.
        return match.Clone();
    }
}
