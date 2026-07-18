# Geometry Interpreter Agent

> **Durum: karar verildi, implementasyon sırada.** Bu spec, tek-entity
> izolasyon kısıtı altında (bkz. aşağıda) somut formül ve eşiklerle
> netleştirildi. Layer Convention Agent ile aynı `IClassificationAgent`
> arayüzünü (bkz.
> `autocad-plugin/CADBridge.AutoCad/Classification/IClassificationAgent.cs`)
> uygulayacak şekilde implemente edilecek.

## Amaç

`CadEntity` listesindeki her elemanın **saf geometrisinden** (isimlendirmeye
bakmadan) bir `candidate_type` ve `confidence` üretmek. Layer Convention
Agent'ın tersine, layer/blok adını hiç kullanmaz.

## Temel mimari kısıt: izole, entity-başına-bir-candidate

`IClassificationAgent.Classify` her `CadEntity` için **bağımsız olarak**
tam bir `Candidate` üretir; entity'ler arası ilişki/eşleştirme (graph,
pairing) bu ajanın mimarisinde **yok**. Bu yüzden "iki ayrı entity'nin
birbirine paralelliği" veya "bir duvar segmentindeki boşluk" gibi
cross-entity kurallar bu ajanda **uygulanamaz** — her karar tek bir
`CadEntity`'nin kendi noktalarından türetilir.

## Kullanılan türetilmiş ölçüler

- **Alan (A)**: shoelace formülü (kapalı polyline'lar için).
- **Yönlendirilmiş bounding box (OBB)**: convex hull + rotating calipers
  ile bulunan minimum alanlı dikdörtgen; kısa kenar `W` (genişlik/kalınlık),
  uzun kenar `L` (uzunluk), `W ≤ L`.
- **Dikdörtgensellik**: `rect = A / (W · L)` — 1'e ne kadar yakınsa şekil
  o kadar "temiz dikdörtgen".
- **Yay uydurma (açık polyline'lar için)**: en küçük kareler (least-squares)
  çember uydurma; yarıçap `R`, maksimum radyal sapma, sweep açısı.

## Karar ağacı ve formüller (MVP: wall, door, window, floor)

### `wall`

`Closed && n≥3`, `rect ≥ 0.85`, `W ∈ [70, 400]mm`, `L ≥ 1200mm`,
`L/W ≥ 4` → `wall`.

- `confidence = 0.8` — `L ≥ 2000mm` ve `L/W ≥ 6` (uzun/ince, başka
  yorumu olmayan net duvar footprint'i).
- `confidence = 0.5` — geniş bant sağlanıyor ama sınırda (kısa duvar
  parçası, örn. bir niş veya kısa bölme).
- `matched_rule: "closed_rect_wall:t=<W>mm,l=<L>mm"`.

Centerline olarak çizilmiş (kalınlıksız, 2 noktalı açık çizgi) duvarlar bu
ajanla **tespit edilmez** → `unknown` döner; her uzun çizgiye 0.5 wall
vermek ölçü çizgisi/aks/merdiven kenarını da yanlışlıkla wall yapar. Bu
temsil şekli MVP'de yalnızca Layer Convention Agent'ın sinyaliyle
taşınır (tek kaynaklı → Comparator confidence'ı tavanlar → review'a düşer).

### `floor`

`Closed`, wall/window ince-dikdörtgen kuralına takılmadı, `A ≥ 1.5 m²`
(1.5×10⁶ mm²), OBB `W ≥ 600mm` (ince bir şerit değil) → `floor`.

- `confidence = 0.8` — `A ≥ 5 m²` (oda ölçeği, mobilya/blok
  footprint'inden büyük).
- `confidence = 0.5` — `A ∈ [1.5, 5) m²` (küçük oda mı, büyük mobilya mı
  belirsiz).
- `matched_rule: "closed_area_floor:<A_m2>"`.

Not: eski taslaktaki **"en büyük alanlı kapalı polyline = floor"** kuralı
**terk edildi** — bu, çok odalı bir planda yalnızca bir floor üretir ve
bir entity'nin sınıfını diğer entity'lerin varlığına bağlar (izolasyon
kısıtını ihlal eder). Yerine mutlak alan eşiği kondu.

### `door`

Yalnızca **kapı kanadı sweep arc'ı** ile tespit edilir: `!Closed && n≥5`
→ en küçük kareler çember uydur; `maksRadyalSapma ≤ max(0.02·R, 5mm)` ise
yay kabul edilir. `R ∈ [600, 1200]mm` ve `sweepAçısı ∈ [60°, 120°]` →
`door`.

- `confidence = 0.8` — `sweep ∈ [80°, 100°]` (tipik 90° sweep) ve
  `R ∈ [600, 1000]mm`.
- `confidence = 0.5` — diğer geçerli kombinasyonlar (geniş açı/çift
  kanat sınırı).
- `matched_rule: "door_swing_arc:r=<mm>,sweep=<deg>"`.

Eski taslaktaki **"duvar segmentindeki boşluk (gap)"** kuralı **terk
edildi** — gap tanımı gereği iki entity arasındaki ilişkidir, izole
mimaride hesaplanamaz. Kapının düz kanat çizgisi de herhangi bir
çizgiden geometrik olarak ayırt edilemediği için `unknown` döner; kapı
tespiti bu ajanda yalnızca arc'a dayanır.

### `window`

`Closed`, `rect ≥ 0.85`, `W ∈ [70, 400]mm` (duvar kalınlığı bandı),
`L ∈ [400, 1200)mm` → `window`, **her zaman `confidence = 0.5`** (tek
seviye — asla 0.8 verilmez).

- `matched_rule: "thin_rect_window:w=<L>mm"`.

**Neden hep 0.5:** izole tek-entity geometriden kapı/pencere ayrımı
ilkesel olarak yapılamaz — bu boyut aralığındaki kapalı ince dikdörtgen
kapı boşluğu, pencere ya da kısa bir duvar parçası da olabilir. Bilinçli
olarak window'a atanıyor çünkü plan çiziminde pencere genelde duvar
içinde ince kapalı dikdörtgen olarak görünür; kapı ise kanat+sweep
arc'ıyla temsil edilir. Bu MVP'de **wall ve floor güvenilir, door
arc-tabanlı güvenilir, window best-effort (0.5)** — 0.5'lik tahmin
Comparator'da Layer Agent'la hemfikirse yükselir, çelişirse review'a
düşer (hibrit mimarinin amacı tam olarak bu). 1200–1800mm'lik geniş
pencereler bu MVP'de wall@0.5'e kayabilir — bilinen sınırlama.

### Hiçbiri eşleşmezse

`candidate_type = "unknown"`, `confidence = 0.0`,
`matched_rule = "no_geometric_match"`.

## Reddedilen / MVP dışı bırakılan senaryolar

1. **İki ayrı paralel çizgi = duvar** (iç yüz + dış yüz ayrı entity):
   cross-entity pairing/graph gerektirir, `Classify`'ın izolasyon
   kısıtıyla çelişir. Doğru çözüm bu ajanı değiştirmek değil, ajanlardan
   *önce* çalışan ayrı bir "geometri zenginleştirme / pairing
   ön-işleme" katmanı eklemek (paralel çiftleri sentetik tek entity'ye
   birleştirir, ajan izolasyonu korunur) — ileriye bırakıldı, bkz.
   `docs/architecture.md` Açık Sorular.
2. **Centerline duvar**: diğer çizgilerden geometrik olarak ayırt
   edilemez → `unknown`, Layer Agent tek başına taşır.
3. **Duvar gap'inden kapı tespiti**: iki entity ilişkisi gerektirir → red.

## Confidence seviyeleri (özet)

İki-üç seviyeli, Layer Convention Agent ile simetrik:

- `0.8`: net, tek yorumlu geometrik eşleşme.
- `0.5`: geçerli ama sınırda / birden fazla yoruma açık (window için
  her zaman bu seviye).
- `0.0`: hiçbir kural eşleşmedi (`unknown`).

## Kapsam dışı

- Layer/blok adı okuma (Layer Convention Agent'ın işi).
- Comparator'ın karşılaştırma/uzlaştırma mantığı.
- Cross-entity pairing (yukarıda 1 numaralı reddedilen senaryo).
