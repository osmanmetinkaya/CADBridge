# frozen_string_literal: true

require "minitest/autorun"
require_relative "../cadbridge/core/builders/floor_builder"

class TestFloorBuilder < Minitest::Test
  def room_entity
    { points: [[0, 0], [4000, 0], [4000, 3000], [0, 3000]] }
  end

  def test_all_points_at_z_zero
    plan = CADBridge::Builders::FloorBuilder.build(room_entity)
    plan.points.each { |p| assert_equal 0, p[2] }
  end

  def test_single_polygon_all_indices
    plan = CADBridge::Builders::FloorBuilder.build(room_entity)
    assert_equal 1, plan.polygons.length
    assert_equal [0, 1, 2, 3], plan.polygons.first
  end

  def test_point_count_matches_footprint
    plan = CADBridge::Builders::FloorBuilder.build(room_entity)
    assert_equal 4, plan.points.length
  end

  def test_too_few_points_raises
    assert_raises(ArgumentError) do
      CADBridge::Builders::FloorBuilder.build({ points: [[0, 0], [1, 1]] })
    end
  end
end
