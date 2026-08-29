# frozen_string_literal: true

module CADBridge
  # Tum mm sabitleri tek yerde. Degerlerin gerekcesi/dogrulanma durumu icin
  # docs/architecture.md "Acik Sorular" bolumune bak.
  module Defaults
    WALL_HEIGHT_MM = 2700
    WINDOW_SILL_HEIGHT_MM = 900
    WINDOW_HEIGHT_MM = 1500
    DOOR_LEAF_THICKNESS_MM = 40
    DOOR_HEIGHT_MM = 2100
    REVIEW_CONFIDENCE_THRESHOLD = 0.6
    DEFAULT_LAYER_NAME = "CADBridge"
    REVIEW_LAYER_NAME = "CADBridge_Review"
  end
end
