using Crop.Disease.Classifier.Models;
using Crop.Disease.Classifier.Services;

// EN: Crop Disease Classifier — Entry Point
// FR: Point d entree du classificateur de maladies des cultures
// Usage: dotnet run -- [datasetZip] [outputDir]

Console.OutputEncoding = System.Text.Encoding.UTF8;

const string ZipName = "corn-maize-lefDiseaseDataset.zip";
string zipPath   = args.Length > 0 ? args[0] : FindDatasetZip(ZipName);
string outputDir = args.Length > 1 ? args[1] : "./output";
string checkpointDir = Path.Combine(outputDir, "checkpoints");
string evalDir       = Path.Combine(outputDir, "eval_noisy");

if (!File.Exists(zipPath))
{
    Console.Error.WriteLine($"[ERROR] Dataset zip not found: {zipPath}");
    Console.Error.WriteLine("Pass the path as first argument: dotnet run -- <path.zip>");
    return 1;
}

string datasetRoot = DatasetPreparer.ExtractIfNeeded(zipPath);
var    preparer    = new DatasetPreparer(datasetRoot);

var allImages = preparer.LoadAllImages();
if (allImages.Count == 0)
{
    Console.Error.WriteLine("[ERROR] No images found. Check the dataset structure.");
    return 2;
}

Console.WriteLine($"[Main] Sampling {DatasetPreparer.TrainSampleSize} images from {allImages.Count} total (stratified)...");
var sampledImages = preparer.SampleStratified(allImages, DatasetPreparer.TrainSampleSize);

var trainer                  = new ModelTrainer();
var (model, labels, trainView) = trainer.Train(sampledImages, checkpointDir);

Directory.CreateDirectory(outputDir);
string labelsPath = Path.Combine(outputDir, "labels.txt");
await File.WriteAllLinesAsync(labelsPath, labels);
Console.WriteLine($"[Main] Labels ({labels.Length}) saved: {labelsPath}");

var mlContext  = new Microsoft.ML.MLContext(seed: 42);
var exporter   = new OnnxExporter(mlContext);
string mlnetPath = Path.Combine(outputDir, "model.zip");
string onnxPath  = Path.Combine(outputDir, "model.onnx");

exporter.SaveMlNet(model, trainView, mlnetPath);
exporter.ExportToOnnx(model, trainView, onnxPath);

Console.WriteLine($"\n[Main] Generating {DatasetPreparer.EvalNoisyCount}-image noisy eval set...");
var augmentor = new ImageAugmentor(seed: 42);
var evalSet   = augmentor.GenerateEvalSet(allImages, evalDir);
Console.WriteLine($"[Main] Noisy eval set ready: {evalSet.Count} images under {evalDir}");

Console.WriteLine("\n[Main] Evaluating model on noisy eval set...");
var reloadedModel = mlContext.Model.Load(mlnetPath, out _);
var evalPredictor = mlContext.Model.CreatePredictionEngine<ImageData, ImagePrediction>(reloadedModel);

int correct = 0;
var confusionByLabel = new Dictionary<string, (int Correct, int Total)>();

foreach (var (imgPath, trueLabel) in evalSet)
{
    var input  = new ImageData { ImagePath = imgPath, Label = trueLabel };
    var output = evalPredictor.Predict(input);
    bool ok    = string.Equals(output.PredictedLabel, trueLabel, StringComparison.OrdinalIgnoreCase);
    if (ok) correct++;
    if (!confusionByLabel.TryGetValue(trueLabel, out var counts)) counts = (0, 0);
    confusionByLabel[trueLabel] = (counts.Correct + (ok ? 1 : 0), counts.Total + 1);
}

double noisyAcc = evalSet.Count > 0 ? (double)correct / evalSet.Count : 0;
Console.WriteLine($"\n[Main] Noisy eval accuracy: {noisyAcc:P2} ({correct}/{evalSet.Count})");
Console.WriteLine("[Main] Per-class breakdown:");
foreach (var (lbl, (c, t)) in confusionByLabel.OrderBy(x => x.Key))
    Console.WriteLine($"  {lbl,-35} {c}/{t} ({(t > 0 ? (double)c / t : 0):P1})");

Console.WriteLine("\n[Main] Training and evaluation complete.");
Console.WriteLine($"  ML.NET model : {mlnetPath}");
Console.WriteLine($"  ONNX model   : {onnxPath}");
Console.WriteLine($"  Labels       : {labelsPath}");
Console.WriteLine($"  Noisy eval   : {evalDir}");

return 0;
// ── Helper: walk up the directory tree to find the dataset zip ─────────────
// EN: Walks up from the assembly directory until it finds the zip file or exhausts 6 levels.
// FR: Remonte depuis le dossier de l assembly jusqu a trouver le zip (6 niveaux max).
static string FindDatasetZip(string zipName)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (int i = 0; i < 6; i++)
    {
        if (dir is null) break;
        var candidate = Path.Combine(dir.FullName, zipName);
        if (File.Exists(candidate))
        {
            Console.WriteLine($"[Main] Dataset zip auto-discovered: {candidate}");
            return candidate;
        }
        dir = dir.Parent;
    }
    return zipName; // return bare name so the error message is clear
}