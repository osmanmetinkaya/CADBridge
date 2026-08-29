# frozen_string_literal: true

module CADBridge
  # Seam: core/ (saf Ruby) ile adapter/sketchup_renderer.rb arasindaki
  # ayrim noktasi. fill_from_mesh'in bekledigi sekle birebir uyan, indeksli
  # duz veri. Bu Struct icinde Sketchup/Geom'a HIC referans yok.
  #
  # points:      [[x_mm, y_mm, z_mm], ...]
  # polygons:    [[i0, i1, i2, i3], ...]  (points icinde 0-tabanli indeksler)
  # layer_name:  String (orn. "CADBridge" veya "CADBridge_Review")
  # review:      bool -- true ise adapter CADBridge_Review materyalini uygular
  MeshPlan = Struct.new(:points, :polygons, :layer_name, :review, keyword_init: true)
end
