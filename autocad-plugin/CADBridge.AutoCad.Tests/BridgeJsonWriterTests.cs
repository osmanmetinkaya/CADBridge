using System.Text.Json;
using CADBridge.AutoCad.Models;
using CADBridge.AutoCad.Output;
using Xunit;

namespace CADBridge.AutoCad.Tests;

public class BridgeJsonWriterTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly BridgeJsonWriter _writer = new();

    private static CadEntity Entity(string id, string layer, bool closed, params (double X, double Y)[] points) => new()
    {
        EntityId = id,
        Layer = layer,
        Points = points,
        Closed = closed,
    };

    [Fact]
    public void Write_ProducesExpectedMeta()
    {
        var json = _writer.Write(
            new[] { Entity("E1", "A-WALL", true, (0, 0), (0, 180)) },
            new[] { Agreement("E1") },
            "plan.dwg",
            FixedTime,
            unitWarning: null);

        using var doc = JsonDocument.Parse(json);
        var meta = doc.RootElement.GetProperty("meta");

        Assert.Equal("plan.dwg", meta.GetProperty("source_file").GetString());
        Assert.Equal("mm", meta.GetProperty("units").GetString());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("unit_warning").ValueKind);
        Assert.Equal("2026-07-18T12:00:00Z", meta.GetProperty("generated_at").GetString());
        Assert.Equal("0.1", meta.GetProperty("bridge_version").GetString());
    }

    [Fact]
    public void Write_UnitWarning_IsSerializedWhenPresent()
    {
        var json = _writer.Write(
            new[] { Entity("E1", "A-WALL", true, (0, 0)) },
            new[] { Agreement("E1") },
            "plan.dwg",
            FixedTime,
            unitWarning: "INSUNITS tanımsız; mm varsayıldı");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(
            "INSUNITS tanımsız; mm varsayıldı",
            doc.RootElement.GetProperty("meta").GetProperty("unit_warning").GetString());
    }

    [Fact]
    public void Write_AgreementEntity_HasBothSourcesAndTypeAndGeometry()
    {
        var json = _writer.Write(
            new[] { Entity("E1", "A-WALL", true, (0, 0), (0, 180), (4000, 180), (4000, 0)) },
            new[] { Agreement("E1") },
            "plan.dwg",
            FixedTime,
            unitWarning: null);

        using var doc = JsonDocument.Parse(json);
        var entity = doc.RootElement.GetProperty("entities")[0];

        Assert.Equal("E1", entity.GetProperty("entity_id").GetString());
        Assert.Equal("wall", entity.GetProperty("type").GetString());
        Assert.Equal(0.95, entity.GetProperty("confidence").GetDouble(), 3);
        Assert.Equal("A-WALL", entity.GetProperty("layer").GetString());

        var sources = entity.GetProperty("source").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new[] { "layer_convention", "geometry_interpreter" }, sources);

        var rules = entity.GetProperty("matched_rule").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new[] { "layer_regex:WALL", "closed_rect_wall:t=180mm,l=4000mm" }, rules);

        var geometry = entity.GetProperty("geometry");
        Assert.Equal("polyline", geometry.GetProperty("kind").GetString());
        Assert.True(geometry.GetProperty("closed").GetBoolean());
        var points = geometry.GetProperty("points");
        Assert.Equal(4, points.GetArrayLength());
        Assert.Equal(4000, points[2][0].GetDouble(), 6);
        Assert.Equal(180, points[2][1].GetDouble(), 6);
    }

    [Fact]
    public void Write_NonConflictEntity_HasEmptyAttributes()
    {
        var json = _writer.Write(
            new[] { Entity("E1", "A-WALL", true, (0, 0)) },
            new[] { Agreement("E1") },
            "plan.dwg",
            FixedTime,
            unitWarning: null);

        using var doc = JsonDocument.Parse(json);
        var attributes = doc.RootElement.GetProperty("entities")[0].GetProperty("attributes");

        Assert.Equal(JsonValueKind.Object, attributes.ValueKind);
        Assert.Empty(attributes.EnumerateObject());
    }

    [Fact]
    public void Write_ConflictEntity_WritesConflictAttribute()
    {
        var conflict = new ResolvedClassification
        {
            EntityId = "E1",
            Type = CandidateType.Wall,
            Confidence = 0.55,
            HasConflict = true,
            Contributions = new[]
            {
                new AgentContribution(ClassificationSource.LayerConvention, CandidateType.Wall, 0.55, "layer_regex:WALL"),
                new AgentContribution(ClassificationSource.GeometryInterpreter, CandidateType.Window, 0.5, "thin_rect_window:w=800mm"),
            },
        };

        var json = _writer.Write(
            new[] { Entity("E1", "A-WALL", true, (0, 0)) },
            new[] { conflict },
            "plan.dwg",
            FixedTime,
            unitWarning: null);

        using var doc = JsonDocument.Parse(json);
        var attributes = doc.RootElement.GetProperty("entities")[0].GetProperty("attributes");

        Assert.True(attributes.GetProperty("conflict").GetBoolean());
    }

    [Fact]
    public void Write_UnknownEntity_HasNoSources()
    {
        var unknown = new ResolvedClassification
        {
            EntityId = "E1",
            Type = CandidateType.Unknown,
            Confidence = 0.0,
            HasConflict = false,
            Contributions = Array.Empty<AgentContribution>(),
        };

        var json = _writer.Write(
            new[] { Entity("E1", "Layer1", false, (0, 0)) },
            new[] { unknown },
            "plan.dwg",
            FixedTime,
            unitWarning: null);

        using var doc = JsonDocument.Parse(json);
        var entity = doc.RootElement.GetProperty("entities")[0];

        Assert.Equal("unknown", entity.GetProperty("type").GetString());
        Assert.Empty(entity.GetProperty("source").EnumerateArray());
        Assert.Empty(entity.GetProperty("matched_rule").EnumerateArray());
    }

    [Fact]
    public void Write_MissingGeometry_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _writer.Write(
            Array.Empty<CadEntity>(),
            new[] { Agreement("E1") },
            "plan.dwg",
            FixedTime,
            unitWarning: null));
    }

    private static ResolvedClassification Agreement(string id) => new()
    {
        EntityId = id,
        Type = CandidateType.Wall,
        Confidence = 0.95,
        HasConflict = false,
        Contributions = new[]
        {
            new AgentContribution(ClassificationSource.LayerConvention, CandidateType.Wall, 0.85, "layer_regex:WALL"),
            new AgentContribution(ClassificationSource.GeometryInterpreter, CandidateType.Wall, 0.8, "closed_rect_wall:t=180mm,l=4000mm"),
        },
    };
}
