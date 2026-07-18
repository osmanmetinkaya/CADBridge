# CADBridge — Mimari

## Problem

Mimari 2D çizimler AutoCAD'de üretiliyor. Bunları SketchUp'ta 3D modele
çevirmek genelde elle yapılıyor ve derin AutoCAD bilgisi (layer düzeni,
blok isimlendirme, çizim disiplini) gerektiriyor. CADBridge bu adımı
otomatikleştirir: kullanıcı AutoCAD'de normal şekilde çizer, SketchUp
tarafında tek tuşla 3D model üretir; düşük güvenilirlikteki elemanlar
kullanıcıya ayrıca işaretlenerek gösterilir (asla sessizce yanlış
varsayılmaz).

## Klasör yapısı

```
CADBridge/
  docs/
    architecture.md      — bu dosya
    api-research.md       — AutoCAD/SketchUp API notları + bridge.json şeması
  agents/
    layer-convention-agent.md
    geometry-interpreter-agent.md
    comparator-agent.md
  autocad-plugin/         — C#, .NET 8, AutoCAD 2026 plugin (extraction + sınıflandırma + bridge.json üretimi)
  sketchup-plugin/        — Ruby, SketchupExtension (bridge.json okuma + 3D geometri üretimi)
```

## Neden hibrit sınıflandırma

Tek başına layer isimlerine güvenmek kırılgan: ofisler arası, hatta aynı
ofis içinde proje bazında layer adlandırma disiplini çok değişken
("A-WALL", "0-MIMARI-DUVAR-IC", "duvarlar", "Layer1"...). Tek başına
geometriye güvenmek de yanıltıcı olabilir (bir merdiven boşluğu bazen
duvar gibi paralel çizgilerle çizilir). Bu yüzden iki bağımsız yöntem
aynı entity için ayrı ayrı tahmin üretir, bir Comparator bu iki tahmini
karşılaştırıp nihai kararı ve güven skorunu belirler.

MVP'de her iki ajan da **kural tabanlı / deterministik** çalışır (regex,
geometrik eşikler). Şu an bir LLM'e bağımlılık yok — offline, hızlı,
ücretsiz. Comparator'ın arayüzü ileride üçüncü bir "LLM fallback" ajanının
(düşük güvenli/eşleşmeyen elemanlar için) eklenebileceği şekilde
tasarlanıyor, ama bu tur onu **yazmıyor** (bkz. Açık Sorular).

## Uçtan uca veri akışı

1. **AutoCAD extraction** (`autocad-plugin`): Transaction/BlockTableRecord
   ile ModelSpace gezilir, her entity (LWPolyline, Polyline, Insert/blok
   referansı) `CadEntity` modeline dönüştürülür: `entity_id`, `layer`,
   `block_name` (varsa), ham geometri noktaları, kaynak birim.
2. **Birim normalizasyonu**: Çizimin `INSUNITS` (veya eşdeğer header
   değişkeni) üzerinden birimi tespit edilir, tüm geometri **milimetreye**
   çevrilir. Ajanlar hep normalize edilmiş mm geometri üzerinden çalışır —
   birim varsayımı yapılmaz, her seferinde tespit edilir.
3. **Layer Convention Agent** ve **Geometry Interpreter Agent** aynı
   `CadEntity` listesi üzerinde **bağımsız ve paralel** çalışır, her ikisi
   de ortak candidate şemasında çıktı üretir:
   `{ entity_id, candidate_type, confidence, matched_rule }`.
4. **Comparator**, iki candidate listesini `entity_id` bazında birleştirir:
   - İki ajan aynı tipte hemfikirse → yüksek birleşik confidence.
   - Çelişki varsa → yüksek confidence'lı taraf seçilir ama düşürülmüş
     confidence ile işaretlenir, her iki alternatif `matched_rule`'da saklanır.
   - Sadece bir ajan tahmin ürettiyse → confidence tavanlanır (tek kaynaklı
     olduğu için asla "yüksek güvenli" sayılmaz).
   - Hiçbiri tahmin üretmediyse → `type: "unknown"`, `confidence: 0`.
5. **bridge.json** üretimi: nihai tip, confidence, kaynak(lar), geometri ve
   orijinal layer bilgisiyle birlikte yazılır (tam şema için
   `docs/api-research.md`).
6. **SketchUp plugin**: `bridge.json` okunur, tipe göre 3D geometri
   üretilir (duvar → ekstrüzyon, zemin → yüzey, kapı/pencere → boşluk/blok).
   Toplu geometri `Geom::PolygonMesh` + `fill_from_mesh` ile oluşturulur.
   **Güven skorunun altındaki elemanlar** (eşik: `confidence < 0.6`) ayrı
   bir `CADBridge_Review` layer'ına konur ve farklı renklendirilir — asla
   sessizce otomatik kabul edilmez.

## Güven skoru ilkesi

Her `bridge.json` elemanı `source` (hangi ajan/ajanlar katkı verdi) ve
`confidence` alanlarını taşımak zorunda. SketchUp tarafı bu bilgiyi
kullanıcıdan gizlemez; düşük güvenli elemanlar ayrıca gösterilir.

## Açık Sorular

Aşağıdaki değerler henüz gerçek proje verisiyle doğrulanmadı — varsayım
olarak işaretleniyor, ileride kalibre edilmeli:

- **Varsayılan duvar yüksekliği**: 2700mm (tipik kat yüksekliği varsayımı,
  doğrulanmadı — bridge.json'da yükseklik bilgisi yoksa SketchUp tarafı
  bunu kullanacak).
- **Pencere parapet (denizlik) yüksekliği**: henüz belirlenmedi.
- **Duvar kalınlığı bandı** (Geometry Interpreter Agent, kapalı
  dikdörtgen footprint'in kısa kenarı): 70–400mm. Gerekçe: TR pratiğinde
  yarım tuğla bölme duvarı ~85mm (sıvasız çizilirse 80'in altına
  düşebilir, bu yüzden alt sınır 70), mantolamalı dış duvar 350–400mm'e
  çıkabilir. Varsayım, gerçek çizim örnekleriyle doğrulanmalı.
- **Kapı sweep arc parametreleri**: yarıçap `600–1200mm`, sweep açısı
  `60°–120°` (TR'de WC/banyo kapıları 60–70cm yaygın). Varsayım.
- **Pencere ince-dikdörtgen bandı**: uzunluk `400–1200mm`, confidence
  her zaman `0.5` (izole geometriden kapı/pencere ayrımı ilkesel olarak
  yapılamadığı için — bkz. `agents/geometry-interpreter-agent.md`).
  1200–1800mm arası geniş pencerelerin wall@0.5'e kayması bilinen bir
  sınırlama.
- **Floor mutlak alan eşikleri**: `1.5 m²` (min yaşanabilir hacim/WC) ve
  `5 m²` (oda ölçeği net eşik), min OBB genişliği `600mm`. "En büyük
  kapalı alan = floor" göreli kuralının yerini aldı — o kural çok odalı
  planlarda yalnızca tek floor üretiyordu. Varsayım.
- **Dikdörtgensellik eşiği** `rect ≥ 0.85`, yay uydurma toleransı
  `max(%2·R, 5mm)`: varsayım.
- **Centerline ve paralel-çift duvar temsilleri**: Geometry Interpreter
  Agent bunları tespit edemiyor (izole entity kısıtı — cross-entity
  ilişki kurmuyor), yalnızca Layer Convention Agent'ın sinyaliyle
  review'a düşüyor. İleride ajanlardan önce çalışan bir "geometri
  zenginleştirme / pairing ön-işleme" katmanı eklenip paralel çiftlerin
  sentetik tek entity'ye birleştirilmesi değerlendirilebilir.
- **Düşük güven eşiği** `confidence < 0.6`: keyfi başlangıç değeri,
  kullanıcı testleriyle ayarlanabilir.
- **LLM fallback**: Comparator arayüzü genişletilebilir bırakıldı ama ne
  zaman/hangi koşulda devreye gireceği (örn. sadece `unknown` elemanlar
  için mi, yoksa çelişkili olanlar için de mi) henüz kararlaştırılmadı.
