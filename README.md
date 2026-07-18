# CADBridge

AutoCAD 2D mimari çizimlerini SketchUp'ta otomatik 3D modele çeviren
plugin köprüsü. Derin AutoCAD bilgisi gerektirmeden, layer-konvansiyon ve
geometri-yorumlama sonuçlarını karşılaştıran hibrit bir sınıflandırma
mimarisi kullanır.

- Mimari genel bakış: [`docs/architecture.md`](docs/architecture.md)
- API notları ve `bridge.json` şeması: [`docs/api-research.md`](docs/api-research.md)
- Ajan spec'leri: [`agents/`](agents/)
- AutoCAD plugin (C#, .NET 8): [`autocad-plugin/`](autocad-plugin/)
- SketchUp plugin (Ruby): `sketchup-plugin/` (henüz oluşturulmadı)