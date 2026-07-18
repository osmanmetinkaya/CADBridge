# Geometry Interpreter Agent

> **Durum: spec only.** Bu tur içinde implementasyon yazılmıyor —
> yalnızca Comparator'ın beklediği ortak şemayla uyumlu bir davranış
> tanımı. Layer Convention Agent ile aynı `IClassificationAgent`
> arayüzünün (bkz. `autocad-plugin/CADBridge.AutoCad/Classification/IClassificationAgent.cs`)
> bir sonraki iterasyonda simetrik bir implementasyonu olacak.

## Amaç

`CadEntity` listesindeki her elemanın **saf geometrisinden** (isimlendirmeye
bakmadan) bir `candidate_type` ve `confidence` üretmek. Layer Convention
Agent'ın tersine, layer/blok adını hiç kullanmaz — bu sayede disiplinsiz
isimlendirilmiş çizimlerde de ikinci bir bağımsız sinyal sağlar.

## Girdi / Çıktı

- Girdi: `IEnumerable<CadEntity>` (normalize edilmiş mm geometri noktaları)
- Çıktı: `IReadOnlyList<Candidate>` — ortak şema
  (`entity_id, candidate_type, confidence, matched_rule`).

## Sezgisel kurallar (MVP: wall, door, window, floor)

- **`wall`**: iki paralel doğru parçası, aralarındaki ofset
  `Açık Sorular`'daki eşik aralığında (varsayım: 80–300mm), ve uzunlukları
  ofsete göre belirgin şekilde büyük (duvar ~ ince ve uzun bir dikdörtgen).
  `matched_rule: "parallel_offset:<mm>"`.
- **`floor`**: kapalı (closed) polyline'lar arasında **en büyük alanlı**
  olanı; iç içe kapalı polyline varsa en dıştaki oda sınırı adayı.
  `matched_rule: "largest_closed_area"`.
- **`door`**: bir duvar segmentindeki boşluk (gap) + tipik kapı genişliği
  aralığı (varsayım: 700–1200mm) veya duvar içinde/üzerinde bir yay (arc,
  kapı kanadı sweep'i) varlığı. `matched_rule: "wall_gap_width:<mm>"` veya
  `"door_swing_arc"`.
- **`window`**: duvar kalınlığıyla uyumlu genişlikte dikdörtgen, kapıdan
  daha dar (varsayım: 400–1800mm), zemin kotunda değil (duvar orta
  yüksekliğinde bir kesit/blok olarak modellenmiş). `matched_rule:
  "wall_thickness_rect:<mm>"`.
- Hiçbir kural eşleşmezse: `candidate_type = "unknown"`, `confidence = 0.0`.

## Confidence atama mantığı (taslak)

- Net geometrik eşleşme (örn. iki paralel çizgi tam eşik aralığında,
  belirsizlik yok): `confidence = 0.8`.
- Sınır değerlere yakın / birden fazla yorumla uyumlu geometri (örn.
  ofset eşik aralığının kenarına yakın): `confidence = 0.5`.
- Bu değerler henüz gerçek çizim verisiyle kalibre edilmedi — bkz.
  `docs/architecture.md` Açık Sorular.

## Kapsam dışı

- Layer/blok adı okuma (Layer Convention Agent'ın işi).
- Comparator'ın karşılaştırma/uzlaştırma mantığı.
