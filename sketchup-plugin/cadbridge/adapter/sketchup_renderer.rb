# frozen_string_literal: true

# NOT: Bu dosya bu gelistirme ortaminda hic calistirilmadi/test edilmedi
# (Sketchup/Geom modulleri yalnizca gercek SketchUp uygulamasinda mevcut).
# Kod docs/api-research.md'deki belgeli API desenine gore yazildi.
#
# TEK untestable dosya. Bir MeshPlan listesini alip yalnizca
# Geom::PolygonMesh.new + add_point/add_polygon + entities.fill_from_mesh +
# model.layers.add + materyal atama cagirir. HICBIR hesap/algoritma yok
# (mekanik dokum) -- tum geometri core/ tarafinda uretilir.

require "sketchup.rb"
require_relative "../core/defaults"

module CADBridge
  module Adapter
    module SketchupRenderer
      module_function

      # plans: Array<CADBridge::MeshPlan>
      # model: Sketchup::Model (default: aktif model)
      def render(plans, model = Sketchup.active_model)
        entities = model.active_entities
        model.start_operation("CADBridge Import", true)
        begin
          ensure_layer(model, Defaults::DEFAULT_LAYER_NAME)
          review_layer = ensure_layer(model, Defaults::REVIEW_LAYER_NAME)
          review_material = ensure_review_material(model)

          plans.each do |plan|
            faces = fill_plan(entities, plan)
            apply_layer(faces, model, plan.layer_name)
            apply_review_material(faces, review_material) if plan.review
          end

          # review_layer degiskenini kullanmis olmak icin (bos plan durumunda
          # da layer'in olusturulmus olmasini garanti eder)
          review_layer
        ensure
          model.commit_operation
        end
      end

      # MeshPlan -> Geom::PolygonMesh -> fill_from_mesh. Doldurulan face'leri doner.
      def fill_plan(entities, plan)
        mesh = Geom::PolygonMesh.new(plan.points.length)

        # mm -> SketchUp ic birimi (inch). Geom::Point3d girdi olarak modelin
        # length birimini bekler; mm'yi .mm cevrimiyle Length'e ceviriyoruz.
        index_map = plan.points.map do |x, y, z|
          mesh.add_point(Geom::Point3d.new(x.mm, y.mm, z.mm))
        end

        plan.polygons.each do |poly|
          # add_polygon 1-tabanli indeks bekler; index_map add_point'ten donen
          # 1-tabanli indeksleri tutar, plan.polygons ise 0-tabanli -> eslestir.
          mesh.add_polygon(poly.map { |i| index_map[i] })
        end

        before = entities.to_a
        entities.fill_from_mesh(mesh, true, Geom::PolygonMesh::NO_SMOOTH_OR_HIDE)
        (entities.to_a - before).grep(Sketchup::Face)
      end

      def ensure_layer(model, name)
        model.layers[name] || model.layers.add(name)
      end

      def apply_layer(faces, model, layer_name)
        layer = ensure_layer(model, layer_name)
        faces.each { |f| f.layer = layer }
      end

      def ensure_review_material(model)
        mat = model.materials[Defaults::REVIEW_LAYER_NAME]
        return mat if mat

        mat = model.materials.add(Defaults::REVIEW_LAYER_NAME)
        mat.color = Sketchup::Color.new(255, 128, 0)
        mat.alpha = 0.5
        mat
      end

      def apply_review_material(faces, material)
        faces.each do |f|
          f.material = material
          f.back_material = material
        end
      end
    end
  end
end
