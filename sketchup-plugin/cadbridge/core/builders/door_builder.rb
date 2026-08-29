# frozen_string_literal: true

require_relative "../mesh_plan"
require_relative "../defaults"
require_relative "../geometry_utils"

module CADBridge
  module Builders
    # Kapi geometrisi bir ACIK yay (arc, kapali degil). Yayin kirisi (chord)
    # acilis genisligi kabul edilir; chord dogrultusunda bir kanat kutusu
    # ekstrude edilir. Ayrica yayin kendisi z=0'da ince bir referans serit
    # olarak basilir (acilis yonunu gostermek icin).
    # Bir MeshPlan LISTESI doner (kanat kutusu + yay seridi).
    # Sketchup/Geom bagimliligi YOK.
    module DoorBuilder
      module_function

      ARC_STRIP_THICKNESS_MM = 5

      # entity: { points: [[x,y],...], ... }  (acik yay)
      # Donen: Array<MeshPlan>
      def build(entity)
        pts = entity[:points]
        raise ArgumentError, "door yayi icin en az 2 nokta gerekli" if pts.length < 2

        plans = []
        plans << leaf_plan(pts)
        plans << arc_strip_plan(pts)
        plans
      end

      # Chord genisliginde, DOOR_LEAF_THICKNESS_MM derinliginde bir dikdortgen
      # footprint'i zeminden DOOR_HEIGHT_MM'e ekstrude eder.
      def leaf_plan(pts)
        p_from = pts.first
        p_to = pts.last
        width = GeometryUtils.chord_length(pts)
        footprint = GeometryUtils.rectangle_along_chord(
          p_from, p_to, width, Defaults::DOOR_LEAF_THICKNESS_MM
        )
        points, polygons = GeometryUtils.extrude_footprint(footprint, 0, Defaults::DOOR_HEIGHT_MM)
        MeshPlan.new(points: points, polygons: polygons, layer_name: nil, review: false)
      end

      # Yay noktalarini z=0'dan ARC_STRIP_THICKNESS_MM'e cikan ince bir dikey
      # serit (curtain) olarak ekstrude eder -- referans kenar.
      def arc_strip_plan(pts)
        bottom = pts.map { |x, y| [x, y, 0] }
        top = pts.map { |x, y| [x, y, ARC_STRIP_THICKNESS_MM] }
        points = bottom + top

        n = pts.length
        polygons = []
        (n - 1).times do |i|
          # alt-i, alt-i+1, ust-i+1, ust-i
          polygons << [i, i + 1, i + 1 + n, i + n]
        end
        MeshPlan.new(points: points, polygons: polygons, layer_name: nil, review: false)
      end
    end
  end
end
