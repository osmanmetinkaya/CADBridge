# frozen_string_literal: true

require_relative "../mesh_plan"
require_relative "../defaults"
require_relative "../geometry_utils"

module CADBridge
  module Builders
    # 2D kapali footprint'i sabit duvar yuksekligine dikey ekstrude eder.
    # Sketchup/Geom bagimliligi YOK.
    module WallBuilder
      module_function

      # entity: { points: [[x,y],...], ... }
      # height_mm: ekstrizyon yuksekligi (default Defaults::WALL_HEIGHT_MM)
      # Donen: MeshPlan (layer_name/review plan_factory tarafindan set edilir)
      def build(entity, height_mm = Defaults::WALL_HEIGHT_MM)
        points, polygons = GeometryUtils.extrude_footprint(entity[:points], 0, height_mm)
        MeshPlan.new(points: points, polygons: polygons, layer_name: nil, review: false)
      end
    end
  end
end
