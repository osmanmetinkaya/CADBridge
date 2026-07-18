# frozen_string_literal: true

module CADBridge
  # Saf 2D/3D geometri yardimcilari. Sketchup/Geom bagimliligi YOK -- tamamen
  # aritmetik. Builder'lar bu modulu paylasir (tekrar azaltmak icin).
  module GeometryUtils
    module_function

    # Kapali 2D poligonun alani (shoelace, mutlak deger). points: [[x,y],...]
    def polygon_area(points)
      n = points.length
      return 0.0 if n < 3

      sum = 0.0
      n.times do |i|
        x1, y1 = points[i]
        x2, y2 = points[(i + 1) % n]
        sum += (x1 * y2) - (x2 * y1)
      end
      (sum / 2.0).abs
    end

    # Nokta dizisinin ilk ve son noktasi arasi duz mesafe (kapi yayinin
    # kirisi = acilis genisligi). points: [[x,y],...]
    def chord_length(points)
      raise ArgumentError, "chord_length icin en az 2 nokta gerekli" if points.length < 2

      distance_2d(points.first, points.last)
    end

    # Iki 2D nokta arasi Oklid mesafesi.
    def distance_2d(a, b)
      dx = b[0] - a[0]
      dy = b[1] - a[1]
      Math.sqrt((dx * dx) + (dy * dy))
    end

    # Iki nokta arasi birim yon vektoru (2D). Sifir uzunlukta hata firlatir.
    def polyline_direction(p_from, p_to)
      dx = p_to[0] - p_from[0]
      dy = p_to[1] - p_from[1]
      len = Math.sqrt((dx * dx) + (dy * dy))
      raise ArgumentError, "polyline_direction: iki nokta cakisik (sifir uzunluk)" if len.zero?

      [dx / len, dy / len]
    end

    # Birim vektorun sol-el (CCW) normali (2D).
    def perpendicular(dir)
      [-dir[1], dir[0]]
    end

    # Chord dogrultusunda ortalanmis dikdortgen kanat footprint'i uretir.
    # p_from/p_to chord uclari; width chord uzunlugu; depth kanat kalinligi.
    # Donen: 4 koseli kapali 2D footprint (CCW), chord orta noktasina ortali.
    def rectangle_along_chord(p_from, p_to, width, depth)
      dir = polyline_direction(p_from, p_to)
      normal = perpendicular(dir)
      mid = [(p_from[0] + p_to[0]) / 2.0, (p_from[1] + p_to[1]) / 2.0]

      half_w = width / 2.0
      half_d = depth / 2.0

      # dir ekseni boyunca +-half_w, normal ekseni boyunca +-half_d
      corners = [
        [-half_w, -half_d],
        [half_w, -half_d],
        [half_w, half_d],
        [-half_w, half_d]
      ]
      corners.map do |u, v|
        [
          mid[0] + (dir[0] * u) + (normal[0] * v),
          mid[1] + (dir[1] * u) + (normal[1] * v)
        ]
      end
    end

    # Kapali 2D footprint'i [z_min, z_max] arasinda dikey ekstrude eder.
    # footprint: [[x,y],...] (N kose, kapali kabul edilir).
    # Donen: [points3d, polygons]
    #   points3d: 2N nokta -- ilk N alt (z_min), sonraki N ust (z_max)
    #   polygons: her kenar icin bir dortgen yan yuzey + alt kapak + ust kapak
    def extrude_footprint(footprint, z_min, z_max)
      n = footprint.length
      raise ArgumentError, "extrude_footprint icin en az 3 kose gerekli" if n < 3

      bottom = footprint.map { |x, y| [x, y, z_min] }
      top = footprint.map { |x, y| [x, y, z_max] }
      points = bottom + top

      polygons = []
      # yan yuzeyler: her kenar (i -> i+1) icin alt-i, alt-i+1, ust-i+1, ust-i
      n.times do |i|
        j = (i + 1) % n
        polygons << [i, j, j + n, i + n]
      end
      # alt kapak (0..n-1) ve ust kapak (n..2n-1)
      polygons << (0...n).to_a
      polygons << (n...(2 * n)).to_a

      [points, polygons]
    end
  end
end
