# frozen_string_literal: true

# NOT: Bu dosya bu gelistirme ortaminda hic calistirilmadi/test edilmedi
# (Sketchup/Geom modulleri yalnizca gercek SketchUp uygulamasinda mevcut).
# Kod docs/api-research.md'deki belgeli API desenine gore yazildi.
#
# Ince orkestrasyon: kullaniciya dosya sectirir, bridge.json okur,
# BridgeReader + PlanFactory (core, testable) cagirir, sonucu
# SketchupRenderer'a (adapter) verir. Algoritma yok.

require "sketchup.rb"
require_relative "core/bridge_reader"
require_relative "core/plan_factory"
require_relative "adapter/sketchup_renderer"

module CADBridge
  module Main
    module_function

    def import_bridge_json
      path = UI.openpanel("bridge.json sec", "", "JSON|*.json||")
      return if path.nil? # kullanici iptal etti

      json = File.read(path)
      parsed = BridgeReader.parse(json)
      result = PlanFactory.build(parsed[:entities])

      Adapter::SketchupRenderer.render(result[:plans])

      msg = "#{result[:plans].length} geometri uretildi."
      unless result[:skipped].empty?
        msg += " Atlanan (unknown/taninmayan) eleman: #{result[:skipped].length}."
      end
      if parsed[:meta][:unit_warning]
        msg += "\nBirim uyarisi: #{parsed[:meta][:unit_warning]}"
      end
      UI.messagebox(msg)
    rescue StandardError => e
      UI.messagebox("CADBridge hata: #{e.message}")
    end

    # Menu komutunu yalnizca bir kez ekle.
    unless file_loaded?(__FILE__)
      menu = UI.menu("Plugins")
      menu.add_item("CADBridge: bridge.json iceri aktar") { import_bridge_json }
      file_loaded(__FILE__)
    end
  end
end
