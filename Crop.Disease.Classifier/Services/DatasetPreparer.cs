using Crop.Disease.Classifier.Models;

namespace Crop.Disease.Classifier.Services
{
    public class DatasetPreparer
    {
        public const int TrainSampleSize = 1500;
        public const int EvalNoisyCount = 60;
        private readonly string _datasetRoot;

        public DatasetPreparer(string datasetRoot) { _datasetRoot = datasetRoot; }

        /// <summary>
        /// EN: Returns ALL image files under datasetRoot, labelled by their parent folder name.
        /// FR: Retourne toutes les images sous datasetRoot, etiquetees par le nom de leur dossier parent.
        /// </summary>
        public List<(string Path, string Label)> LoadAllImages()
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png" };
            var result = new List<(string, string)>();
            foreach (var classDir in Directory.EnumerateDirectories(_datasetRoot))
            {
                string label = Path.GetFileName(classDir);
                foreach (var file in Directory.EnumerateFiles(classDir, "*", SearchOption.AllDirectories))
                    if (extensions.Contains(Path.GetExtension(file)))
                        result.Add((file, label));
            }
            Console.WriteLine("[DatasetPreparer] Total images discovered: " + result.Count);
            return result;
        }

        /// <summary>
        /// EN: Returns a stratified random sample of count images balanced across all label classes.
        /// FR: Retourne un echantillon stratifie de count images equilibre sur toutes les classes.
        /// </summary>
        public List<ImageData> SampleStratified(IReadOnlyList<(string Path, string Label)> all, int count, int seed = 42)
        {
            var rng = new Random(seed);
            var byLabel = all.GroupBy(x => x.Label).ToDictionary(g => g.Key, g => g.ToList());
            int perClass = Math.Max(1, count / byLabel.Count);
            var selected = new List<(string Path, string Label)>();
            foreach (var (label, items) in byLabel)
                selected.AddRange(items.OrderBy(_ => rng.Next()).Take(perClass));
            while (selected.Count > count) selected.RemoveAt(selected.Count - 1);
            while (selected.Count < count) selected.Add(all[rng.Next(all.Count)]);
            selected = selected.OrderBy(_ => rng.Next()).ToList();
            Console.WriteLine("[DatasetPreparer] Stratified sample: " + selected.Count + " images across " + byLabel.Count + " classes");
            return selected.Select(x => new ImageData { ImagePath = x.Path, Label = x.Label }).ToList();
        }

        /// <summary>
        /// EN: Extracts the dataset zip if needed and returns the class-root folder.
        ///     Automatically descends into sub-folders (e.g. extracted/data/) to find the
        ///     directory whose direct children are the prediction class folders.
        /// FR: Extrait le zip du dataset si necessaire et retourne le dossier racine des classes.
        ///     Descend automatiquement dans les sous-dossiers (ex. extracted/data/) pour trouver
        ///     le dossier dont les enfants directs sont les dossiers de classes de prediction.
        /// </summary>
        public static string ExtractIfNeeded(string zipPath)
        {
            string extractDir = Path.Combine(Path.GetDirectoryName(zipPath)!, "dataset_extracted");
            if (!Directory.Exists(extractDir))
            {
                Console.WriteLine("[DatasetPreparer] Extracting " + zipPath + " to " + extractDir + "...");
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);
                Console.WriteLine("[DatasetPreparer] Extraction complete.");
            }
            else
            {
                Console.WriteLine("[DatasetPreparer] Dataset already extracted.");
            }
            return FindClassRoot(extractDir);
        }

        /// <summary>
        /// EN: Walks sub-directories of root until it finds the level where every direct child
        ///     folder contains image files — those are the prediction class folders.
        ///     Handles ZIP structures like: dataset_extracted/ -> data/ -> Blight/, Common_Rust/, ...
        /// FR: Parcourt les sous-dossiers de root jusqu au niveau ou chaque dossier enfant direct
        ///     contient des images — ce sont les dossiers de classes de prediction.
        ///     Gere les structures ZIP : dataset_extracted/ -> data/ -> Blight/, Common_Rust/, ...
        /// </summary>
        private static string FindClassRoot(string root)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png" };
            string[] dirs = Directory.GetDirectories(root);
            if (dirs.Length == 0) return root;

            bool childrenHaveImages = dirs.Any(d =>
                Directory.EnumerateFiles(d, "*", SearchOption.TopDirectoryOnly)
                         .Any(f => extensions.Contains(Path.GetExtension(f))));

            if (childrenHaveImages)
            {
                Console.WriteLine("[DatasetPreparer] Class root detected: " + root);
                Console.WriteLine("[DatasetPreparer] Classes found (" + dirs.Length + "): " +
                    string.Join(", ", dirs.Select(Path.GetFileName)));
                return root;
            }

            foreach (string sub in dirs)
            {
                if (Directory.EnumerateFiles(sub, "*", SearchOption.AllDirectories)
                             .Any(f => extensions.Contains(Path.GetExtension(f))))
                    return FindClassRoot(sub);
            }
            return root;
        }
    }
}