# frozen_string_literal: true

require "json"

module CADBridge
  # bridge.json string'ini standart json kutuphanesiyle parse eder ve her
  # entity'yi symbol-key'li duz bir Hash'e cevirir. Sketchup/Geom bagimliligi
  # YOK. Sema icin docs/api-research.md.
  module BridgeReader
    module_function

    # json_string -> { meta: {...}, entities: [ {...}, ... ] }
    # Her entity: { entity_id:, type:, confidence:, points:, closed:, layer: }
    # points 2D kalir ([[x,y],...]) -- bridge.json 2D plan verisi tasir.
    def parse(json_string)
      raw = JSON.parse(json_string)
      raise ArgumentError, "bridge.json bir JSON nesnesi olmali" unless raw.is_a?(Hash)

      {
        meta: parse_meta(raw["meta"]),
        entities: parse_entities(raw["entities"])
      }
    end

    def parse_meta(meta)
      raise ArgumentError, "eksik alan: meta" if meta.nil?
      raise ArgumentError, "meta bir nesne olmali" unless meta.is_a?(Hash)

      {
        source_file: fetch_field(meta, "source_file", "meta.source_file"),
        units: fetch_field(meta, "units", "meta.units"),
        unit_warning: meta["unit_warning"],
        generated_at: meta["generated_at"],
        bridge_version: meta["bridge_version"]
      }
    end

    def parse_entities(entities)
      raise ArgumentError, "eksik alan: entities" if entities.nil?
      raise ArgumentError, "entities bir dizi olmali" unless entities.is_a?(Array)

      entities.map { |e| parse_entity(e) }
    end

    def parse_entity(entity)
      raise ArgumentError, "entity bir nesne olmali" unless entity.is_a?(Hash)

      geometry = entity["geometry"]
      raise ArgumentError, "eksik alan: geometry (entity_id=#{entity['entity_id']})" if geometry.nil?

      points = geometry["points"]
      raise ArgumentError, "eksik alan: geometry.points (entity_id=#{entity['entity_id']})" if points.nil?
      raise ArgumentError, "geometry.points bir dizi olmali (entity_id=#{entity['entity_id']})" unless points.is_a?(Array)

      {
        entity_id: fetch_field(entity, "entity_id", "entity.entity_id"),
        type: fetch_field(entity, "type", "entity.type"),
        confidence: fetch_field(entity, "confidence", "entity.confidence"),
        points: points.map { |p| [p[0], p[1]] },
        closed: geometry["closed"],
        layer: entity["layer"]
      }
    end

    def fetch_field(hash, key, label)
      raise ArgumentError, "eksik alan: #{label}" unless hash.key?(key)

      hash[key]
    end
  end
end
