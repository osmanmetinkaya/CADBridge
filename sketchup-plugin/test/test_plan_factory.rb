# frozen_string_literal: true

require "minitest/autorun"
require_relative "../cadbridge/core/plan_factory"

class TestPlanFactory < Minitest::Test
  def wall_entity(confidence)
    {
      entity_id: "E-wall",
      type: "wall",
      confidence: confidence,
      points: [[0, 0], [4000, 0], [4000, 180], [0, 180]]
    }
  end

  def floor_entity
    {
      entity_id: "E-floor",
      type: "floor",
      confidence: 0.9,
      points: [[0, 0], [4000, 0], [4000, 3000], [0, 3000]]
    }
  end

  def window_entity
    {
      entity_id: "E-win",
      type: "window",
      confidence: 0.5,
      points: [[0, 0], [1000, 0], [1000, 180], [0, 180]]
    }
  end

  def door_entity
    r = 800.0
    pts = (0..8).map do |i|
      a = (Math::PI / 2.0) * i / 8.0
      [r * Math.cos(a), r * Math.sin(a)]
    end
    { entity_id: "E-door", type: "door", confidence: 0.9, points: pts }
  end

  def unknown_entity
    { entity_id: "E-unk", type: "unknown", confidence: 0.0, points: [[0, 0], [1, 1]] }
  end

  def test_high_confidence_gets_default_layer
    result = CADBridge::PlanFactory.build([wall_entity(0.91)])
    plan = result[:plans].first
    assert_equal CADBridge::Defaults::DEFAULT_LAYER_NAME, plan.layer_name
    assert_equal false, plan.review
  end

  def test_low_confidence_gets_review_layer
    result = CADBridge::PlanFactory.build([wall_entity(0.5)])
    plan = result[:plans].first
    assert_equal CADBridge::Defaults::REVIEW_LAYER_NAME, plan.layer_name
    assert_equal true, plan.review
  end

  def test_threshold_boundary_exactly_at_threshold_is_not_review
    # confidence == 0.6 esik degil (< karsilastirmasi), review olmamali
    result = CADBridge::PlanFactory.build([wall_entity(0.6)])
    assert_equal false, result[:plans].first.review
  end

  def test_unknown_type_is_skipped
    result = CADBridge::PlanFactory.build([unknown_entity])
    assert_empty result[:plans]
    assert_equal ["E-unk"], result[:skipped]
  end

  def test_unrecognized_type_is_skipped
    ent = { entity_id: "E-x", type: "banana", confidence: 0.9, points: [[0, 0], [1, 1]] }
    result = CADBridge::PlanFactory.build([ent])
    assert_equal ["E-x"], result[:skipped]
  end

  def test_mixed_list_routes_to_correct_builders
    entities = [wall_entity(0.9), floor_entity, window_entity, door_entity, unknown_entity]
    result = CADBridge::PlanFactory.build(entities)

    # unknown skipped
    assert_equal ["E-unk"], result[:skipped]

    # wall(1) + floor(1) + window(1) + door(2) = 5 plan
    assert_equal 5, result[:plans].length

    # wall z araligi 0..2700
    wall_plan = result[:plans][0]
    assert_equal [0, 2700], wall_plan.points.map { |p| p[2] }.uniq.sort

    # floor tumu z=0
    floor_plan = result[:plans][1]
    assert_equal [0], floor_plan.points.map { |p| p[2] }.uniq

    # window 900..2400
    window_plan = result[:plans][2]
    assert_equal [900, 2400], window_plan.points.map { |p| p[2] }.uniq.sort

    # window dusuk confidence -> review
    assert_equal true, window_plan.review
  end

  def test_door_produces_two_plans_both_labelled
    result = CADBridge::PlanFactory.build([door_entity])
    assert_equal 2, result[:plans].length
    result[:plans].each do |plan|
      assert_equal CADBridge::Defaults::DEFAULT_LAYER_NAME, plan.layer_name
      assert_equal false, plan.review
    end
  end
end
