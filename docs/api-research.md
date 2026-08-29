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

Düşük güvenli (`confidence < 0.6`) elemanlar için materyal: yarı saydam
turuncu, `RGB(255,128,0)`, alpha `0.5`, materyal adı `CADBridge_Review`.

### MeshPlan — testable/untestable seam

`core/` (saf Ruby, `Sketchup`/`Geom` bağımlılığı yok) ile
`adapter/sketchup_renderer.rb` (gerçek SketchUp API'sine dokunan tek
dosya, bu geliştirme ortamında test edilemez) arasındaki ayrım noktası
`MeshPlan`'dır — `fill_from_mesh`'in beklediği şekle birebir uyan
indeksli, düz veri:

```ruby
MeshPlan = Struct.new(:points, :polygons, :layer_name, :review, keyword_init: true)
# points:  [[x_mm, y_mm, z_mm], ...]
# polygons: [[i0, i1, i2, i3], ...]  (points içindeki 0-tabanlı indeksler)
# layer_name: String (örn. "CADBridge" veya "CADBridge_Review")
# review: bool — true ise adapter CADBridge_Review materyalini uygular
```

`adapter/sketchup_renderer.rb`, bir `MeshPlan`'ı alıp yalnızca
`Geom::PolygonMesh.new` + `add_point`/`add_polygon` +
`entities.fill_from_mesh` + `model.layers.add`/materyal atama
çağırır — hiçbir hesap/algoritma içermez (mekanik döküm).

Her tip için üretilen `MeshPlan`:

| Tip | Geometri kaynağı | 3D üretim |
|-----|-------------------|-----------|
| `wall` | kendi footprint'i (kapalı ince dikdörtgen) | footprint, sabit duvar yüksekliğine (2700mm) ekstrüde edilir |
| `floor` | kapalı poligon | z=0'da düz yüzey |
| `window` | kendi footprint'i (duvar kalınlığı bandında ince dikdörtgen) | footprint'ten **bağımsız**, parapet (900mm) ile parapet+pencere yüksekliği (900+1500=2400mm) arasında serbest duran kutu |
| `door` | kapı kanadı sweep yayı (arc, dikdörtgen açıklık DEĞİL) | yayın kirişi (chord) = açıklık genişliği; zeminden 2100mm yüksekliğe, 40mm kalınlığında bir kanat kutusu + yayın kendisi z=0'da referans kenar olarak |

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
    "unit_warning": null,
    "generated_at": "2026-07-18T12:00:00Z",
    "bridge_version": "0.1"
  },
  "entities": [
    {
      "entity_id": "E-001",
      "type": "wall",
      "confidence": 0.91,
      "source": ["layer_convention", "geometry_interpreter"],
      "matched_rule": ["layer_regex:*DUVAR*", "closed_rect_wall:t=180mm,l=4000mm"],
      "layer": "A-WALL",
      "geometry": {
        "kind": "polyline",
        "points": [[0, 0], [0, 180], [4000, 180], [4000, 0]],
        "closed": true
      },
      "attributes": {}
    }
  ]
}
```

Alan sözlüğü:

| Alan                | Açıklama                                                        |
|---------------------|------------------------------------------------------------------|
| `meta.units`         | Normalize edilmiş birim — her zaman `"mm"`                       |
| `meta.unit_warning`  | Çizimin `INSUNITS`'i `Undefined` geldiyse dolu bir uyarı metni (örn. `"INSUNITS tanımsız, mm varsayıldı"`), aksi halde `null`. |
| `entities[].type`    | Comparator'ın nihai kararı (`wall`/`door`/`window`/`floor`/`unknown`) |
| `entities[].confidence` | Comparator sonrası birleşik güven skoru                       |
| `entities[].source`  | Katkı veren ajan(lar) — `["layer_convention"]`, `["geometry_interpreter"]` veya ikisi birden |
| `entities[].matched_rule` | Şeffaflık için: hangi kural(lar) eşleşti                    |
| `entities[].geometry` | Normalize edilmiş (mm) ham nokta listesi                        |
| `entities[].attributes` | Tipe özgü ek veri, opsiyonel. **MVP'de yalnızca `conflict`** taşınır — `thickness_mm` gibi alanlar henüz doldurulmuyor (bkz. `docs/architecture.md` Açık Sorular). |
| `entities[].attributes.conflict` | Yalnızca `true` iken yazılır — Comparator'ın iki ajanı farklı tiplerde bulduğu (çelişki) durumu işaretler; `source` dizisinin uzunluğu tek başına bunu ayırt edemez (hemfikirlikte de 2 eleman olabilir). Bkz. `agents/comparator-agent.md`. |

Geriye dönük uyumluluk ilkesi: yeni alan eklemek serbest, mevcut alan
adı veya anlamı değiştirilmez.
