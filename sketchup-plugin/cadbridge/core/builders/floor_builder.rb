# frozen_string_literal: true

require_relative "../mesh_plan"
require_relative "../geometry_utils"

module CADBridge
  module Builders
    # Kapali 2D poligonu z=0'da tek bir duz yuzeye cevirir.
    # Sketchup/Geom bagimliligi YOK.
    module FloorBuilder
      module_function

      # entity: { points: [[x,y],...], ... }
      # Donen: MeshPlan -- points her biri [x,y,0], polygon tum indeksler sirayla
      def build(entity)
        pts = entity[:points]
        raise ArgumentError, "floor icin en az 3 nokta gerekli" if pts.length < 3

        points = pts.map { |x, y| [x, y, 0] }
        polygons = [(0...points.length).to_a]
        MeshPlan.new(points: points, polygons: polygons, layer_name: nil, review: false)
      end
    end
  end
end
