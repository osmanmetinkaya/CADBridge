# frozen_string_literal: true

# NOT: Bu dosya bu gelistirme ortaminda hic calistirilmadi/test edilmedi
# (Sketchup/Geom modulleri yalnizca gercek SketchUp uygulamasinda mevcut).
# Kod docs/api-research.md'deki belgeli API desenine gore yazildi.
#
# Extension loader -- algoritma yok. SketchupExtension olarak kaydeder.

require "sketchup.rb"
require "extensions.rb"

module CADBridge
  unless defined?(@loaded) && @loaded
    ext = SketchupExtension.new("CADBridge", "cadbridge/main")
    ext.description = "AutoCAD 2D planlarini 3D SketchUp modeline aktarir"
    ext.version = "0.1.0"
    ext.creator = "CADBridge"
    Sketchup.register_extension(ext, true)
    @loaded = true
  end
end
