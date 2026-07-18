# Comparator Agent

> **Durum: spec only.** Bu tur içinde implementasyon yazılmıyor. Layer
> Convention Agent'ın implementasyonu tamamlandıktan ve Geometry
> Interpreter Agent yazıldıktan sonraki iterasyonda uygulanacak.

## Amaç

Layer Convention Agent ve Geometry Interpreter Agent'tan gelen iki
bağımsız candidate listesini `entity_id` bazında birleştirip her entity
için **tek bir nihai** `type` ve `confidence` üretmek — projenin asıl
değeri burada: tek yönteme indirgenmeden en güvenilir sonucu bulmak
(skill kuralı #2).

## Girdi / Çıktı

- Girdi: iki `IReadOnlyList<Candidate>` (Layer Convention ve Geometry
  Interpreter çıktıları), aynı `entity_id` uzayı üzerinden.
- Çıktı: `bridge.json`'daki `entities[]` listesi — `type`, `confidence`,
  `source[]`, `matched_rule[]` alanlarıyla (bkz. `docs/api-research.md`).

## Uzlaştırma mantığı

Her `entity_id` için dört durum:

1. **Her iki ajan da aynı `candidate_type`'ta hemfikir**:
   `type = <ortak tip>`, `confidence = combine(c1, c2)` (öneri: ağırlıklı
   ortalama yerine `1 - (1-c1)*(1-c2)` gibi "ikisi de doğru der ki daha
   güvenilir" mantığı — kesin formül implementasyon aşamasında
   netleştirilecek), `source = ["layer_convention", "geometry_interpreter"]`.
2. **İki ajan farklı `candidate_type` döndü (çelişki)**: yüksek
   confidence'lı taraf `type` olarak seçilir, ama nihai `confidence`
   düşürülür (öneri: `max(c1, c2) * 0.7` gibi bir ceza çarpanı — kalibre
   edilecek), her iki alternatif `matched_rule`'da saklanır, ayrıca
   `attributes.conflict = true` gibi bir işaretleyici düşünülebilir.
3. **Sadece bir ajan candidate ürettü** (diğeri `unknown`/hiç üretmedi):
   diğer ajanın teyidi olmadığı için `confidence` tavanlanır (öneri:
   `min(c, 0.6)`), `source` tek elemanlı olur.
4. **Hiçbir ajan candidate üretmedi**: `type = "unknown"`,
   `confidence = 0`, `source = []`. Bu, gelecekteki LLM fallback ajanının
   devreye girebileceği asıl nokta (bkz. aşağıda).

## LLM fallback için genişletme noktası (henüz yazılmıyor)

Comparator, ileride üçüncü bir ajan (LLM tabanlı) çağırabilecek şekilde
tasarlanmalı — örn. `IReadOnlyList<Candidate>? TryLlmFallback(CadEntity)`
gibi opsiyonel bir bağımlılık. Şu an için:
- Hangi durumlarda tetikleneceği (sadece durum 4 mü, yoksa durum 2'deki
  çelişkiler için de mi) **kararlaştırılmadı** — `docs/architecture.md`
  Açık Sorular'da.
- Bu ajan bu tur **yazılmıyor**; sadece Comparator'ın arayüzünün buna
  kapalı olmaması gerektiği not ediliyor.

## Kapsam dışı

- Layer/blok adı okuma, geometri analizi (diğer iki ajanın işi).
- bridge.json'un dosyaya yazılması (ayrı bir `BridgeJsonWriter`
  bileşeninin işi olacak).
