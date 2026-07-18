# Comparator Agent

> **Durum: karar verildi, implementasyon sırada.** Fable 5 ile alınan
> mimari karar doğrultusunda dört durumun kesin formülleri, çıktı modeli
> ve genişletilebilirlik şekli aşağıda netleştirildi.

## Amaç

Layer Convention Agent ve Geometry Interpreter Agent'tan (ve ileride
üçüncü bir ajandan) gelen bağımsız candidate listelerini `entity_id`
bazında birleştirip her entity için **tek bir nihai** `type` ve
`confidence` üretmek — projenin asıl değeri burada: tek yönteme
indirgenmeden en güvenilir sonucu bulmak (skill kuralı #2).

## Ön kural: "görüş bildirmeme" normalizasyonu

Bir ajanın çıktısı, `candidate_type == Unknown` **veya**
`confidence == 0.0` ise **"bu ajan görüş bildirmedi"** sayılır — bir
candidate olarak değil. Bu kural olmadan "tek kaynak" ve "çelişki"
durumları birbirine karışır (örn. `wall@0.85` vs `unknown@0.0` bir
çelişki değildir, tek-kaynak durumudur).

## Girdi / Çıktı

- **Girdi**: kaynak-etiketli, N-ajanlı bir liste:
  `IReadOnlyList<(ClassificationSource Source, IReadOnlyList<Candidate> Candidates)>`.
  Bugün için iki eleman (`LayerConvention`, `GeometryInterpreter`)
  verilir, ama Comparator'ın mantığı ajan sayısından bağımsız (N-ary)
  çalışır — üçüncü bir ajan eklendiğinde Comparator'ın kendisi
  değişmez.
- **Çıktı**: `IReadOnlyList<ResolvedClassification>` (bkz. aşağıdaki
  model). bridge.json'a serileştirme bu iterasyonun kapsamında değil —
  ayrı bir `BridgeJsonWriter` ilerideki bir iterasyonda bunu üretecek.

## Uzlaştırma formülleri (N-ary genelleşir)

Bir entity için "görüş bildiren" ajanların confidence değerleri
`c₁, c₂, ..., cₙ` olsun (görüş bildirmeyenler normalizasyon kuralıyla
elenir).

### Durum 1 — Hemfikir (tüm görüş bildirenler aynı `candidate_type`'ta)

```
confidence = min(1 - ∏(1 - cᵢ), 0.95)
```

Noisy-OR birleşimi ("bağımsız iki yöntem aynı yönde hemfikirse, ikisinin
de yanılması ayrı ayrı yanılmalarından çok daha az olası"), üstüne
**0.95 tavanı** — kural tabanlı iki heuristiğin birleşimi asla ~1.0
kesinlik iddia etmemeli.

Sayısal doğrulama (gerçek ajan confidence değerleriyle):

| Layer | Geometry | Ham noisy-OR | Nihai |
|-------|----------|---------------|-------|
| 0.85  | 0.8      | 0.970         | **0.95** |
| 0.75  | 0.8      | 0.950         | 0.95  |
| 0.55  | 0.8      | 0.910         | 0.91  |
| 0.85  | 0.5      | 0.925         | 0.925 |
| 0.55  | 0.5      | 0.775         | 0.775 |

En zayıf hemfikir çift (0.55+0.5 → 0.775) bile review eşiğinin çok
üzerinde kalır — bu **kasıtlı**: iki bağımsız yöntemin aynı tipte
buluşması projenin asıl değeri.

`Source = [görüş bildiren tüm ajanlar]`, `HasConflict = false`.

### Durum 2 — Çelişki (görüş bildirenler farklı `candidate_type`'larda)

```
type = argmax_i(cᵢ)  — eşitlikte LayerConvention kazanır (insan niyeti taşıyan sinyal)
confidence = min(0.7 · max(cᵢ), 0.55)
```

`0.55` **sert tavan** olarak formüle gömülü — mevcut değerlerle
`0.7·max` zaten hep 0.6'nın altında çıkıyor (en kötü 0.85×0.7=0.595),
ama bu, gelecekte bir ajana daha yüksek bir kural eklenirse (örn. 0.9)
kırılabilir. Tavan bu riski önler: **çelişkili bir eleman hiçbir
koşulda review'ı atlayamaz**.

`HasConflict = true`, tüm alternatifler `Contributions`'ta saklanır.

### Durum 3 — Tek kaynak (yalnızca bir ajan görüş bildirdi)

```
confidence = min(c, 0.55)
```

**Tavan 0.6 değil 0.55.** Gerekçe: SketchUp tarafının review eşiği
`confidence < 0.6` olarak zaten sabitlendi (değiştirilemez). Eğer tavan
`0.6` olsaydı, tek-kaynaklı `Layer wall@0.85` → tam `0.6` olurdu ve
`0.6 < 0.6` yanlış olduğu için review'a **düşmezdi** — "tek kaynaklı asla
yüksek güvenli sayılmaz" ilkesi delinirdi. `0.55`, eşikle arasında net
marj bırakır (ileride eşik ±0.02 kalibre edilse bile davranış değişmez)
ve sistemde zaten var olan bir anlamla ("zayıf ama var olan sinyal" —
Layer Agent'ın gömülü eşleşmesi de 0.55) tutarlıdır.

`Source = [tek ajan]`, `HasConflict = false`.

### Durum 4 — Hiçbiri görüş bildirmedi

`type = Unknown`, `confidence = 0`, `Contributions` boş.

## Sistem değişmezi (invariant)

> **0.6 review eşiğinin üstüne çıkmanın tek yolu, en az iki bağımsız
> ajanın hemfikir olmasıdır.** Tek kaynak ve çelişki her zaman
> `< 0.6` kalır (review'a düşer); yalnızca hemfikirlik `≥ 0.6`
> üretebilir.

## Çıktı modeli

```csharp
public enum ClassificationSource
{
    LayerConvention,
    GeometryInterpreter,
    // İleride: LlmFallback (bkz. aşağıda — bugün eklenmiyor)
}

public sealed record AgentContribution(
    ClassificationSource Source,
    CandidateType CandidateType,
    double Confidence,
    string MatchedRule);

public sealed class ResolvedClassification
{
    public required string EntityId { get; init; }
    public required CandidateType Type { get; init; }
    public required double Confidence { get; init; }
    public required IReadOnlyList<AgentContribution> Contributions { get; init; }
    public bool HasConflict { get; init; }
}
```

`Contributions`, yalnızca "görüş bildiren" ajanları içerir (durum 4'te
boş liste). bridge.json'daki düz `source[]` / `matched_rule[]` dizileri
(`docs/api-research.md`) buradan `BridgeJsonWriter` tarafından
projeksiyonla üretilecek — paralel-dizi (indeks eşleşmesi) riski C#
tarafında böylece yok.

`attributes.conflict` (bridge.json, yalnızca `true` iken yazılır):
`source` listesinin boyutu (hemfikirlikte de 2, çelişkide de 2 olabilir)
tek başına ayrım için yeterli sinyal değil — `matched_rule` string'lerini
parse etmeden SketchUp/Ruby tarafının çelişkiyi anlaması için ayrı bir
`HasConflict`/`conflict` alanı gerekli. Review'da "çelişki" (hangi ajana
inanılacağını seçmek) ile "tek kaynaklı zayıf" (onaylamak) farklı
kullanıcı aksiyonları gerektirir.

## LLM fallback için genişletme noktası

**Placeholder interface/ölü constructor parametresi bırakılmıyor.**
Genişletilebilirlik `Resolve` imzasına gömülü: girdi zaten kaynak
sayısından bağımsız bir liste olduğu için, üçüncü bir ajan eklendiğinde
Comparator'ın kendisi **değişmeden** üçüncü bir `(Source, Candidates)`
elemanı eklenir — formüller zaten N-ary genelleşiyor. Ne zaman/hangi
koşulda bir LLM fallback ajanının çağrılacağı (Comparator'dan önce, ayrı
bir Orchestrator katmanının düşük güvenli `ResolvedClassification`'ları
görüp LLM'i tetiklemesi ve Comparator'ı 3 listeyle yeniden koşması
öneriliyor — Comparator saf/deterministik kalır) henüz kararlaştırılmadı;
bugün yazılan tek iz `ClassificationSource` enum'unun genişletilebilir
olmasıdır.

## Kapsam dışı

- Layer/blok adı okuma, geometri analizi (diğer iki ajanın işi).
- bridge.json'un dosyaya yazılması (ayrı bir `BridgeJsonWriter`
  bileşeninin işi olacak).
- LLM fallback ajanının kendisi ve tetikleme mantığı.
