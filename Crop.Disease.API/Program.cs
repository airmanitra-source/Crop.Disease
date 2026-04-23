using Crop.Disease.API.Services;

var builder = WebApplication.CreateBuilder(args);

// -- Chemins vers les artefacts -----------------------------------------------
// AppContext.BaseDirectory = bin/Debug/net10.0/ (VS + dotnet run + prod)
// Fallback : dossier du projet (quand lancé depuis VS sans publish)
static string ResolvePath(string configured, string relative)
{
    if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
        return configured;

    // 1. Depuis bin/Debug/net10.0/
    string fromBase = Path.Combine(AppContext.BaseDirectory, relative);
    if (File.Exists(fromBase)) return fromBase;

    // 2. Depuis le répertoire de travail courant (projet VS)
    string fromCwd = Path.Combine(Directory.GetCurrentDirectory(), relative);
    if (File.Exists(fromCwd)) return fromCwd;

    // Retourne le chemin bin/ par défaut (l'erreur sera explicite)
    return fromBase;
}

string onnxInt8   = ResolvePath(builder.Configuration["OnnxInt8Path"]!, "output/model_int8.onnx");
string mlnetZip   = ResolvePath(builder.Configuration["ModelPath"]!,    "output/model.zip");
string labelsPath = ResolvePath(builder.Configuration["LabelsPath"]!,   "output/labels.txt");

bool useOnnx = File.Exists(onnxInt8);
Console.WriteLine(useOnnx
    ? $"[Startup] ONNX INT8 : {onnxInt8}"
    : $"[Startup] ML.NET zip: {mlnetZip}");
Console.WriteLine($"[Startup] Labels    : {labelsPath}");

// -- Services -----------------------------------------------------------------
builder.Services.AddControllers();

if (useOnnx)
    builder.Services.AddSingleton(_ => new InferenceService(onnxInt8,  labelsPath, useOnnx: true));
else
    builder.Services.AddSingleton(_ => new InferenceService(mlnetZip, labelsPath, useOnnx: false));

builder.Services.AddSingleton<RationaleService>();
builder.Services.AddSingleton<SymptomMatcherService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
