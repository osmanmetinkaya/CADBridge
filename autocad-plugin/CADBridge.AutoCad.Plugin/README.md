# CADBridge.AutoCad.Plugin

Gerçek `Autodesk.AutoCAD.*` API'sine bağımlı ince adapter. Algoritma
içermez: `DrawingExtractor` Transaction/BlockTableRecord ile ModelSpace'i
gezip AutoCAD'e bağımlı olmayan `RawDrawingData` seam'ini doldurur,
`Commands.RunCadBridge` bunu `CADBridge.AutoCad.Pipeline.Orchestrator`'a
devredip sonucu `.bridge.json` olarak yazar. Tüm sınıflandırma/karar
mantığı `CADBridge.AutoCad` projesindedir (bkz. `docs/architecture.md`).

## Derleme

AutoCAD'in yönetilen API DLL'leri (`AcCoreMgd.dll`, `AcDbMgd.dll`,
`AcMgd.dll`) kurulum klasöründen `HintPath` ile referans alınır
(`Private=false`, kopyalanmaz — zaten AutoCAD process'inde yüklüler).
Varsayılan yol `C:\Program Files\Autodesk\AutoCAD 2027\`; farklı bir
makinede/sürümde `-p:AutoCadInstallDir="C:\...\AutoCAD 20XX\"` ile
override edin (veya `AUTOCAD_INSTALL_DIR` ortam değişkeni).

**Hedef framework**: `net10.0` — AutoCAD 2027'nin yönetilen DLL'leri
`System.Runtime 10.0.0.0`'a göre derlenmiş; `net8.0` ile derlemek
`CS1705` sürüm uyuşmazlığı hatası verir. `CADBridge.AutoCad` ve
`CADBridge.AutoCad.Tests` `net8.0`'da kalmaya devam eder (AutoCAD'den
bağımsız, sandboxta/CI'da test edilir); yalnızca bu proje AutoCAD
sürümünün gerektirdiği runtime'a göre ayarlanır.

```powershell
dotnet build CADBridge.AutoCad.Plugin\CADBridge.AutoCad.Plugin.csproj
```

## AutoCAD'e yükleme (NETLOAD)

```
NETLOAD
C:/Users/.../CADBridge.AutoCad.Plugin/bin/Debug/net10.0/CADBridge.AutoCad.Plugin.dll
```

İlk yüklemede imzasız derleme uyarısı ("Security - Unsigned Executable
File") çıkar — "Load Once" ile onaylayın. Komut satırında ters eğik
çizgili yol LISP string'i içinde escape karakteri gibi davranır
(`\n`, `\U...` gibi diziler bozulur); NETLOAD'a **forward slash**'lı
yol verin veya ters eğik çizgileri ikiye katlayın.

Yüklendikten sonra komut:

```
CADBRIDGE
```

Aktif çizimin `db.Filename`'i varsa `<dwg-adı>.bridge.json` olarak
yanına, kaydedilmemiş çizimde `%TEMP%\bridge.json`'a yazar ve işlenen
entity sayısını komut satırına basar.

## Doğrulama durumu

2026-08-26'da gerçek bir AutoCAD 2027 oturumunda (COM automation ile
NETLOAD + CADBRIDGE) uçtan uca doğrulandı: kapalı bir dikdörtgen
polyline `floor` (tek kaynaklı, confidence 0.55, review eşiğinin
altında — beklenen davranış), `A-WALL` katmanında 180mm kalınlığında
kapalı bir dikdörtgen `wall` (layer_convention + geometry_interpreter
hemfikir, confidence 0.95) olarak doğru sınıflandırıldı ve geçerli
bridge.json üretildi. Henüz denenmedi: `BlockReference` (kapı/pencere
blok) extraction gerçek bir blok örneğiyle, ve kaydedilmiş bir .dwg
üzerinde dosya-yanına-yazma yolu.
