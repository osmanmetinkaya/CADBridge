# Layer Convention Agent

## Amaç

`CadEntity` listesindeki her elemanın **layer adını** ve varsa **blok
adını** yaygın mimari çizim konvansiyonlarına (İngilizce ve Türkçe) göre
eşleştirip bir `candidate_type` ve `confidence` üretmek. Geometriye
bakmaz — sadece isimlendirme sinyali kullanır.

## Girdi / Çıktı

- Girdi: `IEnumerable<CadEntity>` (`entity_id`, `layer`, `block_name?`)
- Çıktı: `IReadOnlyList<Candidate>` — ortak şema
  (`entity_id, candidate_type, confidence, matched_rule`),
  bkz. `docs/api-research.md`.
- Her `CadEntity` için **en fazla bir** candidate üretilir (birden çok
  kural eşleşirse en yüksek confidence'lı kural kazanır).

## Kalıp kütüphanesi (MVP: wall, door, window, floor)

Eşleştirme, layer adı büyük/küçük harf duyarsız ve kelime sınırı
gözetmeksizin **içerir** (regex) mantığıyla çalışır; blok adı için de
aynı kalıplar denenir.

| candidate_type | İngilizce kalıplar                  | Türkçe kalıplar                       |
|----------------|--------------------------------------|-----------------------------------------|
| `wall`         | `WALL`, `A-WALL`                     | `DUVAR`                                  |
| `door`         | `DOOR`, `A-DOOR`                     | `KAPI`                                   |
| `window`       | `WINDOW`, `A-WIN`, `A-GLAZ`          | `PENCERE`                                |
| `floor`        | `FLOOR`, `SLAB`, `A-FLOOR`           | `ZEMIN`, `DOSEME`, `DÖŞEME`              |

## Confidence atama mantığı

- **Tam/prefix eşleşme** (örn. layer tam olarak `A-WALL` ya da
  `A-WALL-INT`, standart prefix yapısına uyuyor): `confidence = 0.85`.
- **Kısmi/gömülü eşleşme** (kalıp, daha büyük ve alışılmadık bir string
  içinde geçiyor, örn. `kat1_ictoduvar_v2_final` içinde `DUVAR` geçiyor):
  `confidence = 0.55`.
- **Blok adından eşleşme** (kapı/pencere için `block_name` üzerinden):
  `confidence = 0.75` — blok isimleri genelde daha disiplinli ama yine de
  layer kadar güvenilir sayılmaz.
- **Hiçbir kalıp eşleşmedi**: `candidate_type = "unknown"`,
  `confidence = 0.0`, `matched_rule = "no_match"`.
- Birden fazla tip için eşleşme varsa (örn. layer hem `DUVAR` hem
  `KAPI` içeriyor — nadir ama olası), en yüksek confidence'lı tip seçilir;
  eşitlik durumunda `unknown` döner (Comparator'a çelişkiyi çözmesi için
  bırakılmaz; bu ajan kendi içinde tekil bir cevap vermek zorunda).

## matched_rule formatı

`"layer_regex:<kalıp>"` veya `"block_name_regex:<kalıp>"`, örn.
`"layer_regex:DUVAR"`, `"block_name_regex:WINDOW"`.

## Kapsam dışı (bu ajanın yapmadığı şeyler)

- Geometri okuma/analiz (Geometry Interpreter Agent'ın işi).
- İki ajanın sonucunu karşılaştırma (Comparator'ın işi).
- LLM'e başvurma (şu an yok — bkz. `docs/architecture.md` Açık Sorular).
