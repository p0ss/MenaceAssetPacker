// Simple deployment test - runs the same code as the modkit deploy
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Menace.Modkit.Core;
using Menace.Modkit.Core.Bundles;

class DeployTest
{
    static async Task Main(string[] args)
    {
        var gameDataPath = "/home/poss/.steam/debian-installation/steamapps/common/Menace/Menace_Data";
        var stagingPath = "/home/poss/Documents/MenaceModkit/staging/Sellswords";
        var unityVersion = "2022.3.28f1"; // Menace Unity version

        Console.WriteLine("=== Deploy Test ===");
        Console.WriteLine($"Game Data: {gameDataPath}");
        Console.WriteLine($"Modpack: {stagingPath}");

        // Load clone definitions
        var clonesDir = Path.Combine(stagingPath, "clones");
        var cloneFiles = Directory.GetFiles(clonesDir, "*.json");
        Console.WriteLine($"Found {cloneFiles.Length} clone definition files");

        var modpackClones = new Dictionary<string, Dictionary<string, string>>();
        foreach (var cloneFile in cloneFiles)
        {
            var typeName = Path.GetFileNameWithoutExtension(cloneFile);
            var json = File.ReadAllText(cloneFile);
            var definitions = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (definitions != null)
            {
                modpackClones[typeName] = definitions;
                foreach (var (newId, sourceId) in definitions)
                {
                    Console.WriteLine($"  Clone: {newId} <- {sourceId} (type: {typeName})");
                }
            }
        }

        var mergedClones = new MergedCloneSet();
        mergedClones.AddFromModpack(modpackClones);

        // Collect texture entries from assets directory
        var textureEntries = new List<BundleCompiler.TextureEntry>();
        var assetsDir = Path.Combine(stagingPath, "assets");
        if (Directory.Exists(assetsDir))
        {
            var imageFiles = Directory.GetFiles(assetsDir, "*.png", SearchOption.AllDirectories);
            Console.WriteLine($"Found {imageFiles.Length} PNG files in assets/");

            foreach (var imagePath in imageFiles)
            {
                var assetName = Path.GetFileNameWithoutExtension(imagePath);
                var resourcePath = $"assets/textures/sellswords/{assetName}".ToLowerInvariant();

                textureEntries.Add(new BundleCompiler.TextureEntry
                {
                    AssetName = assetName,
                    SourceFilePath = imagePath,
                    ResourcePath = resourcePath,
                    CreateSprite = true
                });
                Console.WriteLine($"  Texture: {assetName} -> {resourcePath}");
            }
        }

        // Create output directory
        var outputDir = Path.Combine(stagingPath, "build", "compiled");
        Directory.CreateDirectory(outputDir);
        var bundlePath = Path.Combine(outputDir, "data.bundle");

        // Run compilation
        Console.WriteLine("\nCompiling bundle...");
        var compiler = new BundleCompiler();
        var emptyPatches = new MergedPatchSet();

        var result = await compiler.CompileDataPatchBundleAsync(
            emptyPatches,
            mergedClones,
            null,  // no audio
            textureEntries.Count > 0 ? textureEntries : null,
            null,  // no models
            gameDataPath,
            unityVersion,
            bundlePath);

        Console.WriteLine($"Success: {result.Success}");
        Console.WriteLine($"Message: {result.Message}");
        Console.WriteLine($"Clones Created: {result.ClonesCreated}");

        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"  {warning}");
        }

        if (result.Success)
        {
            // Copy patched files to game directory
            var patchedResources = Path.Combine(outputDir, "resources.assets.patched");
            var patchedGgm = Path.Combine(outputDir, "globalgamemanagers.patched");

            if (File.Exists(patchedResources))
            {
                var destResources = Path.Combine(gameDataPath, "resources.assets");
                Console.WriteLine($"\nCopying {patchedResources} to {destResources}");
                File.Copy(patchedResources, destResources, true);
            }

            if (File.Exists(patchedGgm))
            {
                var destGgm = Path.Combine(gameDataPath, "globalgamemanagers");
                Console.WriteLine($"Copying {patchedGgm} to {destGgm}");
                File.Copy(patchedGgm, destGgm, true);
            }

            // Copy asset manifest to Mods/compiled/ for CompiledAssetLoader
            var manifestPath = Path.Combine(outputDir, "asset-manifest.json");
            if (File.Exists(manifestPath))
            {
                var gameRoot = Path.GetDirectoryName(gameDataPath)!;
                var modsCompiledDir = Path.Combine(gameRoot, "Mods", "compiled");
                Directory.CreateDirectory(modsCompiledDir);
                var destManifest = Path.Combine(modsCompiledDir, "asset-manifest.json");
                Console.WriteLine($"Copying {manifestPath} to {destManifest}");
                File.Copy(manifestPath, destManifest, true);
            }

            Console.WriteLine("\n=== Deployment Complete ===");
            Console.WriteLine("Try launching the game now!");
        }
    }
}
