using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class GameLevelCatalogBuilder : IPreprocessBuildWithReport
{
    const string GameLevelsDirectory = "Assets/StreamingAssets/gameLevels";
    const string CatalogFileName = "content-catalog.json";

    [Serializable]
    class LevelMetadata
    {
        public string levelName;
        public string levelAuthor;
    }

    public int callbackOrder => 0;

    [MenuItem("Orbit/Levels/Rebuild Game Level Catalog")]
    public static void RebuildGameLevelCatalog()
    {
        if (!Directory.Exists(GameLevelsDirectory))
        {
            Debug.LogError("ERROR: Game levels directory could not be found: " + GameLevelsDirectory);
            return;
        }

        LevelCatalog catalog = new LevelCatalog();
        string[] levelPaths = Directory.GetFiles(GameLevelsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), CatalogFileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();

        for (int index = 0; index < levelPaths.Length; index++)
        {
            string levelPath = levelPaths[index];
            string payloadFileName = Path.GetFileName(levelPath);
            string levelId = Path.GetFileNameWithoutExtension(levelPath);
            LevelMetadata metadata = JsonUtility.FromJson<LevelMetadata>(File.ReadAllText(levelPath));
            string previewFileName = Path.ChangeExtension(payloadFileName, ".png");

            if (metadata == null || string.IsNullOrWhiteSpace(metadata.levelName))
            {
                Debug.LogWarning("WARNING: Game level has no levelName and will use its file name: " + payloadFileName);
                metadata = metadata ?? new LevelMetadata();
                metadata.levelName = levelId;
            }

            if (!File.Exists(Path.Combine(GameLevelsDirectory, previewFileName)))
                Debug.LogWarning("WARNING: Game level preview image could not be found: " + previewFileName);

            catalog.content.Add(new LevelCatalogRecord
            {
                id = levelId,
                contentType = "level",
                displayName = metadata.levelName,
                author = metadata.levelAuthor,
                payloadFileName = payloadFileName,
                previewFileName = previewFileName,
                sortOrder = index + 1
            });
        }

        string catalogPath = GameLevelsDirectory + "/" + CatalogFileName;
        File.WriteAllText(catalogPath, JsonUtility.ToJson(catalog, true));
        AssetDatabase.ImportAsset(catalogPath);
        Debug.Log("Rebuilt game level catalog with " + catalog.content.Count + " level(s).");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        RebuildGameLevelCatalog();
    }
}
