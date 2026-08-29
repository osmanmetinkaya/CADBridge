# frozen_string_literal: true

require "minitest/autorun"
require_relative "../cadbridge/core/builders/window_builder"

class TestWindowBuilder < Minitest::Test
  def footprint_entity
    { points: [[0, 0], [1000, 0], [1000, 180], [0, 180]] }
  end

  def test_extrudes_between_sill_and_top
    plan = CADBridge::Builders::WindowBuilder.build(footprint_entity)
    z_values = plan.points.map { |p| p[2] }.uniq.sort
    sill = CADBridge::Defaults::WINDOW_SILL_HEIGHT_MM
    top = sill + CADBridge::Defaults::WINDOW_HEIGHT_MM
    assert_equal [sill, top], z_values
    assert_equal [900, 2400], z_values
  end

  def test_no_points_at_z_zero
    plan = CADBridge::Builders::WindowBuilder.build(footprint_entity)
    refute_includes plan.points.map { |p| p[2] }, 0
  end

  def test_polygons_reference_valid_indices
    plan = CADBridge::Builders::WindowBuilder.build(footprint_entity)
    max_index = plan.points.length - 1
    plan.polygons.flatten.each { |i| assert i >= 0 && i <= max_index }
  end
end
