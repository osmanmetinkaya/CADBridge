# API Araştırması ve bridge.json Şeması

## AutoCAD .NET API (autocad-plugin)

Hedef: AutoCAD 2026 → .NET 8. Standart okuma deseni:

```csharp
using (Transaction tr = db.TransactionManager.StartTransaction())
{
    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
        bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

    foreach (ObjectId id in modelSpace)
    {
        Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
        // ent.Layer -> layer adı
        // ent is Polyline / LWPolyline -> geometri noktaları
        // ent is BlockReference -> br.Name -> blok adı (kapı/pencere blokları için)
    }
    tr.Commit();
}
```

Notlar:
- **Layer adı**: `Entity.Layer` her zaman dolu; sınıflandırmanın birincil
  girdisi.
- **Blok adı**: Kapı/pencere genelde `BlockReference` olarak eklenir;
  `BlockReference.Name` (dynamic block ise `BlockTableRecord.Name` üzerinden
  effective ad) Layer Convention Agent için ek sinyal.
- **Birim tespiti**: `Database.Insunits` (`UnitsValue` enum: `Millimeters`,
  `Centimeters`, `Meters`, ...). Çizim birimi burada okunup mm'ye
  normalize edilir. `Insunits == UnitsValue.Undefined` durumu için
  varsayılan mm kabul edilir ve bridge.json'a uyarı olarak düşülür.
- Kapalı poligon tespiti (zemin/oda sınırı adayları) için
  `Polyline.Closed` / `LWPolyline.Closed` kullanılır.

## SketchUp Ruby API (sketchup-plugin)

Eklenti, `SketchupExtension` olarak kaydedilir:

```ruby
require 'sketchup.rb'
ext = SketchupExtension.new('CADBridge', 'cadbridge/main')
ext.description = 'AutoCAD 2D planlarini 3D modele aktarir'
Sketchup.register_extension(ext, true)
```

Toplu geometri üretimi için tekil `Entities#add_face` çağırmak yerine
`Geom::PolygonMesh` doldurup `Entities#fill_from_mesh` ile tek seferde
basılır — çok sayıda duvar/zemin için performans farkı büyük:

```ruby
mesh = Geom::PolygonMesh.new
points.each { |pt| mesh.add_point(pt) }
mesh.add_polygon(index_a, index_b, index_c, index_d)
entities.fill_from_mesh(mesh, true, Geom::PolygonMesh::NO_SMOOTH_OR_HIDE)
```

Düşük güvenli elemanlar için ayrı layer:

```ruby
review_layer = model.layers.add('CADBridge_Review')
```

## Ortak candidate şeması

Layer Convention Agent ve Geometry Interpreter Agent, Comparator'a
girdi olarak **aynı şemada** bir liste döner (skill kuralı #3):

| Alan            | Tip    | Açıklama                                            |
|-----------------|--------|------------------------------------------------------|
| `entity_id`     | string | AutoCAD entity handle/ID'sinden türetilmiş kimlik    |
| `candidate_type`| string | `wall` \| `door` \| `window` \| `floor` \| `unknown`  |
| `confidence`    | number | 0.0–1.0                                               |
| `matched_rule`  | string | Hangi kuralın eşleştiğini insan-okunabilir açıklar    |

Bu şemaya yeni alan eklemek serbest; mevcut alan adı/anlamı değiştirilmez.

## bridge.json şeması (v0.1)

```json
{
  "meta": {
    "source_file": "plan.dwg",
    "units": "mm",
    "generated_at": "2026-07-18T12:00:00Z",
    "bridge_version": "0.1"
  },
  "entities": [
    {
      "entity_id": "E-001",
      "type": "wall",
      "confidence": 0.91,
      "source": ["layer_convention", "geometry_interpreter"],
      "matched_rule": ["layer_regex:*DUVAR*", "parallel_offset:180mm"],
      "layer": "A-WALL",
      "geometry": {
        "kind": "polyline",
        "points": [[0, 0], [0, 180], [4000, 180], [4000, 0]],
        "closed": true
      },
      "attributes": {
        "thickness_mm": 180
      }
    }
  ]
}
```

Alan sözlüğü:

| Alan                | Açıklama                                                        |
|---------------------|------------------------------------------------------------------|
| `meta.units`         | Normalize edilmiş birim — her zaman `"mm"`                       |
| `entities[].type`    | Comparator'ın nihai kararı (`wall`/`door`/`window`/`floor`/`unknown`) |
| `entities[].confidence` | Comparator sonrası birleşik güven skoru                       |
| `entities[].source`  | Katkı veren ajan(lar) — `["layer_convention"]`, `["geometry_interpreter"]` veya ikisi birden |
| `entities[].matched_rule` | Şeffaflık için: hangi kural(lar) eşleşti                    |
| `entities[].geometry` | Normalize edilmiş (mm) ham nokta listesi                        |
| `entities[].attributes` | Tipe özgü ek veri (duvar kalınlığı vb.), opsiyonel            |

Geriye dönük uyumluluk ilkesi: yeni alan eklemek serbest, mevcut alan
adı veya anlamı değiştirilmez.
