# frozen_string_literal: true

require "minitest/autorun"
require_relative "../cadbridge/core/builders/wall_builder"

class TestWallBuilder < Minitest::Test
  def footprint_entity
    { points: [[0, 0], [4000, 0], [4000, 180], [0, 180]] }
  end

  def test_extrudes_to_wall_height
    plan = CADBridge::Builders::WallBuilder.build(footprint_entity)
    z_values = plan.points.map { |p| p[2] }.uniq.sort
    assert_includes z_values, 0
    assert_includes z_values, CADBridge::Defaults::WALL_HEIGHT_MM
    assert_equal [0, 2700], z_values
  end

  def test_point_count_is_double_footprint
    plan = CADBridge::Builders::WallBuilder.build(footprint_entity)
    assert_equal 8, plan.points.length
  end

  def test_polygons_reference_valid_indices
    plan = CADBridge::Builders::WallBuilder.build(footprint_entity)
    max_index = plan.points.length - 1
    plan.polygons.flatten.each do |i|
      assert i >= 0 && i <= max_index, "gecersiz indeks #{i}"
    end
  end

  def test_custom_height
    plan = CADBridge::Builders::WallBuilder.build(footprint_entity, 3000)
    assert_includes plan.points.map { |p| p[2] }, 3000
  end
end
