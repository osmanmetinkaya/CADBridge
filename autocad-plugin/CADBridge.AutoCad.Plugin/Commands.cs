using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CADBridge.AutoCad.Extraction;
using CADBridge.AutoCad.Pipeline;
using Exception = System.Exception;

[assembly: CommandClass(typeof(CADBridge.AutoCad.Plugin.Commands))]

namespace CADBridge.AutoCad.Plugin;

/// <summary>
/// Plugin'in AutoCAD'e görünen tek yüzü. Mekanik döküm: aktif çizimi
/// gezip <see cref="RawDrawingData"/> üretir, tüm sınıflandırma/karar
/// mantığını içeren <see cref="Orchestrator"/>'a devreder, sonucu
/// .dwg'nin yanına bridge.json olarak yazar.
/// </summary>
public class Commands
{
    [CommandMethod("CADBRIDGE")]
    public void RunCadBridge()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Editor ed = doc.Editor;
        Database db = doc.Database;

        string sourceFile = string.IsNullOrEmpty(db.Filename) ? "(unsaved).dwg" : db.Filename;

        try
        {
            RawDrawingData drawingData = DrawingExtractor.Extract(db, sourceFile);

            var orchestrator = new Orchestrator();
            string bridgeJson = orchestrator.Run(drawingData, DateTimeOffset.UtcNow);

            string outputPath = ResolveOutputPath(db.Filename);
            File.WriteAllText(outputPath, bridgeJson);

            ed.WriteMessage($"\nCADBridge: {drawingData.Entities.Count} entity işlendi. Yazıldı: {outputPath}\n");
        }
        catch (Exception ex)
        {
            ed.WriteMessage($"\nCADBridge hata: {ex.Message}\n");
        }
    }

    private static string ResolveOutputPath(string dwgFilename)
    {
        if (string.IsNullOrEmpty(dwgFilename))
        {
            return Path.Combine(Path.GetTempPath(), "bridge.json");
        }

        string directory = Path.GetDirectoryName(dwgFilename) ?? Path.GetTempPath();
        string baseName = Path.GetFileNameWithoutExtension(dwgFilename);
        return Path.Combine(directory, $"{baseName}.bridge.json");
    }
}
