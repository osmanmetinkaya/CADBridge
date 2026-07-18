# frozen_string_literal: true

require "minitest/autorun"
require_relative "../cadbridge/core/builders/door_builder"

class TestDoorBuilder < Minitest::Test
  # 90 derece kapi sweep yayi, R=800, merkez (0,0). Yay (800,0)'dan (0,800)'e.
  # Chord = sqrt(800^2 + 800^2) = 800*sqrt(2) ~ 1131.37
  def arc_entity
    r = 800.0
    pts = (0..8).map do |i|
      a = (Math::PI / 2.0) * i / 8.0
      [r * Math.cos(a), r * Math.sin(a)]
    end
    { points: pts }
  end

  def test_returns_list_of_two_plans
    plans = CADBridge::Builders::DoorBuilder.build(arc_entity)
    assert_kind_of Array, plans
    assert_equal 2, plans.length
  end

  def test_leaf_width_equals_chord_length
    ent = arc_entity
    expected_chord = 800.0 * Math.sqrt(2)
    # chord dogru hesaplaniyor mu
    assert_in_delta expected_chord,
                    CADBridge::GeometryUtils.chord_length(ent[:points]), 1e-6

    leaf = CADBridge::Builders::DoorBuilder.leaf_plan(ent[:points])
    # footprint alt yuzey 4 kosesi (ilk 4 nokta), 2D olarak kenar uzunluklari
    footprint = leaf.points.first(4).map { |p| [p[0], p[1]] }
    edges = (0...4).map do |i|
      a = footprint[i]
      b = footprint[(i + 1) % 4]
      Math.sqrt(((b[0] - a[0])**2) + ((b[1] - a[1])**2))
    end.sort
    # iki kisa kenar = kanat kalinligi (40), iki uzun kenar = chord genisligi
    assert_in_delta CADBridge::Defaults::DOOR_LEAF_THICKNESS_MM, edges[0], 1e-6
    assert_in_delta CADBridge::Defaults::DOOR_LEAF_THICKNESS_MM, edges[1], 1e-6
    assert_in_delta expected_chord, edges[2], 1e-6
    assert_in_delta expected_chord, edges[3], 1e-6
  end

  def test_leaf_box_z_range
    ent = arc_entity
    leaf = CADBridge::Builders::DoorBuilder.leaf_plan(ent[:points])
    z_values = leaf.points.map { |p| p[2] }.uniq.sort
    assert_equal [0, CADBridge::Defaults::DOOR_HEIGHT_MM], z_values
    assert_equal [0, 2100], z_values
  end

  def test_arc_strip_is_thin_and_at_ground
    ent = arc_entity
    strip = CADBridge::Builders::DoorBuilder.arc_strip_plan(ent[:points])
    z_values = strip.points.map { |p| p[2] }.uniq.sort
    assert_equal [0, CADBridge::Builders::DoorBuilder::ARC_STRIP_THICKNESS_MM], z_values
  end

  def test_all_polygons_valid_indices
    plans = CADBridge::Builders::DoorBuilder.build(arc_entity)
    plans.each do |plan|
      max_index = plan.points.length - 1
      plan.polygons.flatten.each { |i| assert i >= 0 && i <= max_index }
    end
  end
end
