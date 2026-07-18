# frozen_string_literal: true

require "minitest/autorun"
require_relative "../cadbridge/core/geometry_utils"

class TestGeometryUtils < Minitest::Test
  U = CADBridge::GeometryUtils

  def test_polygon_area_square
    square = [[0, 0], [10, 0], [10, 10], [0, 10]]
    assert_in_delta 100.0, U.polygon_area(square), 1e-9
  end

  def test_polygon_area_is_absolute_regardless_of_winding
    ccw = [[0, 0], [10, 0], [10, 10], [0, 10]]
    cw = ccw.reverse
    assert_in_delta U.polygon_area(ccw), U.polygon_area(cw), 1e-9
  end

  def test_chord_length_straight
    assert_in_delta 5.0, U.chord_length([[0, 0], [3, 4]]), 1e-9
  end

  # Bir yarim daire yayinin chord'u = 2 x yaricap. R=100, merkez orjin,
  # yay noktalari (100,0) .. (-100,0). Chord = 200.
  def test_chord_length_semicircle
    r = 100.0
    pts = (0..8).map do |i|
      a = Math::PI * i / 8.0
      [r * Math.cos(a), r * Math.sin(a)]
    end
    assert_in_delta 2 * r, U.chord_length(pts), 1e-6
  end

  def test_polyline_direction_unit
    dir = U.polyline_direction([0, 0], [3, 4])
    assert_in_delta 0.6, dir[0], 1e-9
    assert_in_delta 0.8, dir[1], 1e-9
    assert_in_delta 1.0, Math.sqrt((dir[0]**2) + (dir[1]**2)), 1e-9
  end

  def test_polyline_direction_zero_length_raises
    assert_raises(ArgumentError) { U.polyline_direction([1, 1], [1, 1]) }
  end

  def test_rectangle_along_chord_dimensions
    # chord x ekseninde, uzunluk 800, derinlik 40
    rect = U.rectangle_along_chord([0, 0], [800, 0], 800, 40)
    assert_equal 4, rect.length
    xs = rect.map { |p| p[0] }
    ys = rect.map { |p| p[1] }
    assert_in_delta 800.0, xs.max - xs.min, 1e-9
    assert_in_delta 40.0, ys.max - ys.min, 1e-9
    # chord orta noktasina (400,0) ortali
    assert_in_delta 400.0, (xs.max + xs.min) / 2.0, 1e-9
    assert_in_delta 0.0, (ys.max + ys.min) / 2.0, 1e-9
  end

  def test_extrude_footprint_point_count_and_z
    footprint = [[0, 0], [10, 0], [10, 5], [0, 5]]
    points, polygons = U.extrude_footprint(footprint, 0, 2700)
    assert_equal 8, points.length
    z_values = points.map { |p| p[2] }.uniq.sort
    assert_equal [0, 2700], z_values
    # 4 yan yuzey + alt kapak + ust kapak = 6 polygon
    assert_equal 6, polygons.length
    # tum indeksler gecerli
    polygons.flatten.each { |i| assert_includes (0...points.length), i }
  end
end
