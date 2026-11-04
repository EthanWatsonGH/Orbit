using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public sealed class GameLevelCatalogBuilder : IPreprocessBuildWithReport
{
    const string GameLevelsDirectory = "Assets/StreamingAssets/gameLevels";
    const string CatalogFileName = "content-catalog.json";

    static GameLevelCatalogBuilder()
    {
        EditorApplication.playModeStateChanged -= RebuildBeforeEnteringPlayMode;
        EditorApplication.playModeStateChanged += RebuildBeforeEnteringPlayMode;
    }

    public int callbackOrder => 0;

    static void RebuildBeforeEnteringPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
            RebuildGameLevelCatalog();
    }

    [MenuItem("Orbit/Levels/Rebuild Game Level Catalog")]
    public static void RebuildGameLevelCatalog()
    {
        if (!Directory.Exists(GameLevelsDirectory))
        {
            Debug.LogError("ERROR: Game levels directory could not be found: " + GameLevelsDirectory);
            return;
        }

        ContentCatalog catalog = new ContentCatalog();
        string[] levelPaths = Directory.GetFiles(GameLevelsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), CatalogFileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();

        for (int index = 0; index < levelPaths.Length; index++)
        {
            string levelPath = levelPaths[index];
            string payloadFileName = Path.GetFileName(levelPath);
            ContentData metadata;
            try { metadata = JsonUtility.FromJson<ContentData>(File.ReadAllText(levelPath)); }
            catch (Exception exception) { Debug.LogWarning("Game level is invalid: " + payloadFileName + ": " + exception.Message); continue; }
            if (metadata == null || metadata.loaderVersion != 1 || metadata.kind != ContentKind.Level || metadata.objects == null || string.IsNullOrWhiteSpace(metadata.id))
            {
                Debug.LogWarning("Game level uses an unsupported content format: " + payloadFileName);
                continue;
            }
            if (catalog.entries.Any(entry => entry.id == metadata.id))
            {
                Debug.LogWarning("Game level has a duplicate content ID: " + payloadFileName);
                continue;
            }
            string previewFileName = Path.ChangeExtension(payloadFileName, ".png");
            if (!File.Exists(Path.Combine(GameLevelsDirectory, previewFileName)))
                Debug.LogWarning("Game level preview image could not be found: " + previewFileName);
            catalog.entries.Add(new ContentRecord
            {
                id = metadata.id,
                displayName = string.IsNullOrWhiteSpace(metadata.name) ? Path.GetFileNameWithoutExtension(levelPath) : metadata.name,
                author = metadata.author,
                payloadFileName = payloadFileName,
                previewFileName = previewFileName,
                sortOrder = index + 1
            });
        }

        string catalogPath = GameLevelsDirectory + "/" + CatalogFileName;
        string catalogJson = JsonUtility.ToJson(catalog, false);
        if (File.Exists(catalogPath) && File.ReadAllText(catalogPath) == catalogJson)
            return;

        File.WriteAllText(catalogPath, catalogJson);
        AssetDatabase.ImportAsset(catalogPath);
        Debug.Log("Rebuilt game level catalog with " + catalog.entries.Count + " level(s).");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        RebuildGameLevelCatalog();
    }
}
