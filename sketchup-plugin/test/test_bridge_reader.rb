# frozen_string_literal: true

require "minitest/autorun"
require_relative "../cadbridge/core/bridge_reader"

class TestBridgeReader < Minitest::Test
  VALID_JSON = <<~JSON
    {
      "meta": {
        "source_file": "plan.dwg",
        "units": "mm",
        "unit_warning": null,
        "generated_at": "2026-07-18T12:00:00Z",
        "bridge_version": "0.1"
      },
      "entities": [
        {
          "entity_id": "E-001",
          "type": "wall",
          "confidence": 0.91,
          "source": ["layer_convention", "geometry_interpreter"],
          "matched_rule": ["layer_regex:*DUVAR*"],
          "layer": "A-WALL",
          "geometry": {
            "kind": "polyline",
            "points": [[0, 0], [0, 180], [4000, 180], [4000, 0]],
            "closed": true
          },
          "attributes": {}
        }
      ]
    }
  JSON

  def test_parses_meta_fields
    result = CADBridge::BridgeReader.parse(VALID_JSON)
    meta = result[:meta]
    assert_equal "plan.dwg", meta[:source_file]
    assert_equal "mm", meta[:units]
    assert_nil meta[:unit_warning]
    assert_equal "0.1", meta[:bridge_version]
  end

  def test_parses_entity_fields
    result = CADBridge::BridgeReader.parse(VALID_JSON)
    assert_equal 1, result[:entities].length
    e = result[:entities].first
    assert_equal "E-001", e[:entity_id]
    assert_equal "wall", e[:type]
    assert_in_delta 0.91, e[:confidence], 1e-9
    assert_equal "A-WALL", e[:layer]
    assert_equal true, e[:closed]
    assert_equal [[0, 0], [0, 180], [4000, 180], [4000, 0]], e[:points]
  end

  def test_points_are_2d
    result = CADBridge::BridgeReader.parse(VALID_JSON)
    result[:entities].first[:points].each do |p|
      assert_equal 2, p.length
    end
  end

  def test_missing_meta_raises
    err = assert_raises(ArgumentError) do
      CADBridge::BridgeReader.parse('{"entities": []}')
    end
    assert_match(/meta/, err.message)
  end

  def test_missing_entities_raises
    json = '{"meta": {"source_file": "a", "units": "mm"}}'
    err = assert_raises(ArgumentError) { CADBridge::BridgeReader.parse(json) }
    assert_match(/entities/, err.message)
  end

  def test_missing_geometry_points_raises
    json = <<~JSON
      {
        "meta": {"source_file": "a", "units": "mm"},
        "entities": [
          {"entity_id": "E-1", "type": "wall", "confidence": 0.9,
           "geometry": {"kind": "polyline", "closed": true}}
        ]
      }
    JSON
    err = assert_raises(ArgumentError) { CADBridge::BridgeReader.parse(json) }
    assert_match(/points/, err.message)
  end

  def test_missing_entity_type_raises
    json = <<~JSON
      {
        "meta": {"source_file": "a", "units": "mm"},
        "entities": [
          {"entity_id": "E-1", "confidence": 0.9,
           "geometry": {"points": [[0,0],[1,1]], "closed": false}}
        ]
      }
    JSON
    err = assert_raises(ArgumentError) { CADBridge::BridgeReader.parse(json) }
    assert_match(/type/, err.message)
  end
end
