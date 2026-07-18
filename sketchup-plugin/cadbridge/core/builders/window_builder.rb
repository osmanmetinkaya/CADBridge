# frozen_string_literal: true

require_relative "../mesh_plan"
require_relative "../defaults"
require_relative "../geometry_utils"

module CADBridge
  module Builders
    # 2D footprint'i parapet (sill) yuksekligi ile parapet+pencere yuksekligi
    # arasinda serbest duran bir kutuya ekstrude eder (wall ile ayni ekstrizyon
    # mantigi, farkli z araligi). Sketchup/Geom bagimliligi YOK.
    module WindowBuilder
      module_function

      # entity: { points: [[x,y],...], ... }
      # Donen: MeshPlan
      def build(entity)
        z_min = Defaults::WINDOW_SILL_HEIGHT_MM
        z_max = Defaults::WINDOW_SILL_HEIGHT_MM + Defaults::WINDOW_HEIGHT_MM
        points, polygons = GeometryUtils.extrude_footprint(entity[:points], z_min, z_max)
        MeshPlan.new(points: points, polygons: polygons, layer_name: nil, review: false)
      end
    end
  end
end
