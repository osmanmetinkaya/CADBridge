# frozen_string_literal: true

require_relative "defaults"
require_relative "mesh_plan"
require_relative "builders/wall_builder"
require_relative "builders/floor_builder"
require_relative "builders/window_builder"
require_relative "builders/door_builder"

module CADBridge
  # Parse edilmis entity listesini alir, her entity'yi tipine gore dogru
  # builder'a yonlendirir, uretilen MeshPlan(lar)a layer_name/review'i
  # confidence'a gore yazar. Sketchup/Geom bagimliligi YOK.
  module PlanFactory
    module_function

    BUILDERS = {
      "wall" => Builders::WallBuilder,
      "floor" => Builders::FloorBuilder,
      "window" => Builders::WindowBuilder,
      "door" => Builders::DoorBuilder
    }.freeze

    # entities: BridgeReader'dan gelen parse edilmis entity listesi
    # Donen: { plans: [MeshPlan, ...], skipped: [entity_id, ...] }
    def build(entities)
      plans = []
      skipped = []

      entities.each do |entity|
        builder = BUILDERS[entity[:type]]
        if builder.nil?
          # unknown veya taninmayan tip -> mesh uretme, skipped'e ekle
          skipped << entity[:entity_id]
          next
        end

        result = builder.build(entity)
        entity_plans = result.is_a?(Array) ? result : [result]
        review = review?(entity[:confidence])
        entity_plans.each do |plan|
          plan.layer_name = review ? Defaults::REVIEW_LAYER_NAME : Defaults::DEFAULT_LAYER_NAME
          plan.review = review
          plans << plan
        end
      end

      { plans: plans, skipped: skipped }
    end

    # confidence nil veya esigin altindaysa review = true
    def review?(confidence)
      return true if confidence.nil?

      confidence < Defaults::REVIEW_CONFIDENCE_THRESHOLD
    end
  end
end
