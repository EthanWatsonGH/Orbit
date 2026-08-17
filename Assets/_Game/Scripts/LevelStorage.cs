using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public enum LevelSource
{
    Game,
    MyLevels,
    // TODO(downloaded-levels): Add a validated downloader/importer that calls SaveLevel.
    // Files written directly into this folder bypass its catalog and will not be shown.
    DownloadedLevels
}

[Serializable]
public class LevelCatalog
{
    // Keep this field name so existing content-catalog.json files remain compatible.
    public List<LevelCatalogRecord> content = new List<LevelCatalogRecord>();
}

[Serializable]
public class LevelCatalogRecord
{
    public string id;
    public string contentType;
    public string displayName;
    public string author;
    public string payloadFileName;
    public string previewFileName;
    public long createdAtUtcTicks;
    public long updatedAtUtcTicks;
    public int sortOrder;
}

// The only class that knows how each level source is physically stored.
public sealed class LevelStorage
{
    const string CatalogFileName = "content-catalog.json";
    const string LegacyPlayerIndexFileName = "level-index.json";
    const string GameLevelsDirectoryName = "gameLevels";

    [Serializable]
    class LegacyLevelMetadata
    {
        public string levelName;
        public string levelAuthor;
    }

    readonly string myLevelsDirectory;
    readonly string downloadedLevelsDirectory;

    public LevelStorage(string myLevelsDirectory, string downloadedLevelsDirectory)
    {
        this.myLevelsDirectory = myLevelsDirectory;
        this.downloadedLevelsDirectory = downloadedLevelsDirectory;
    }

    public void EnsureLocalContentDirectories()
    {
        EnsureLocalContentDirectory(LevelSource.MyLevels);
        EnsureLocalContentDirectory(LevelSource.DownloadedLevels);
    }

    public string CreateFileName(string displayName, string id, string extension)
    {
        return SanitizeReadableName(displayName) + "--" + id + extension;
    }

    public IEnumerator LoadCatalog(LevelSource source, Action<List<LevelCatalogRecord>> onLoaded)
    {
        if (source == LevelSource.Game)
        {
            LevelCatalog catalog = null;
            yield return LoadGameCatalog(loadedCatalog => catalog = loadedCatalog);
            onLoaded?.Invoke(SortRecords(catalog, source));
            yield break;
        }

        onLoaded?.Invoke(SortRecords(LoadLocalCatalog(source), source));
    }

    public IEnumerator LoadPayload(LevelSource source, LevelCatalogRecord record, Action<string> onLoaded)
    {
        if (source == LevelSource.Game)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(GetGameContentPath(record.payloadFileName)))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log("ERROR: Game level could not be loaded. Content: " + record.id + ". Error: " + request.error);
                    onLoaded?.Invoke(null);
                    yield break;
                }

                onLoaded?.Invoke(request.downloadHandler.text);
            }

            yield break;
        }

        string payloadPath = GetLocalContentPath(source, record.payloadFileName);
        if (!File.Exists(payloadPath))
        {
            Debug.Log("ERROR: " + source + " level could not be found. Content: " + record.id);
            onLoaded?.Invoke(null);
            yield break;
        }

        try
        {
            onLoaded?.Invoke(File.ReadAllText(payloadPath));
        }
        catch (Exception exception)
        {
            Debug.Log("ERROR: " + source + " level could not be loaded. Content: " + record.id + ". Error: " + exception.Message);
            onLoaded?.Invoke(null);
        }
    }

    public IEnumerator LoadPreview(LevelSource source, LevelCatalogRecord record, Action<Texture2D> onLoaded)
    {
        if (string.IsNullOrEmpty(record.previewFileName))
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        if (source == LevelSource.Game)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(GetGameContentPath(record.previewFileName)))
            {
                yield return request.SendWebRequest();
                onLoaded?.Invoke(request.result == UnityWebRequest.Result.Success ? DownloadHandlerTexture.GetContent(request) : null);
            }

            yield break;
        }

        string previewPath = GetLocalContentPath(source, record.previewFileName);
        if (!File.Exists(previewPath))
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        try
        {
            Texture2D imageTexture = new Texture2D(2, 2);
            onLoaded?.Invoke(imageTexture.LoadImage(File.ReadAllBytes(previewPath)) ? imageTexture : null);
        }
        catch (Exception exception)
        {
            Debug.Log("ERROR: " + source + " preview could not be loaded. Content: " + record.id + ". Error: " + exception.Message);
            onLoaded?.Invoke(null);
        }
    }

    // All local level writes must use this method so payload, preview, and catalog stay in sync.
    // TODO(downloaded-levels): The future downloader must validate the JSON and preview before this call,
    // decide how duplicate IDs and newer versions are handled, and clean up interrupted downloads.
    // Do not write downloaded files directly into the folder; this class intentionally does not rescan it.
    public bool SaveLevel(LevelSource source, LevelCatalogRecord record, string levelJson, byte[] previewImageBytes)
    {
        if (source == LevelSource.Game)
        {
            Debug.Log("ERROR: Game levels are read-only.");
            return false;
        }

        if (record == null || string.IsNullOrEmpty(record.id) || string.IsNullOrEmpty(record.payloadFileName) || string.IsNullOrEmpty(levelJson))
        {
            Debug.Log("ERROR: A local level could not be saved because its record is incomplete.");
            return false;
        }

        if (!string.IsNullOrEmpty(record.previewFileName) && (previewImageBytes == null || previewImageBytes.Length == 0))
        {
            Debug.Log("ERROR: A local level could not be saved because its preview image is empty.");
            return false;
        }

        try
        {
            EnsureLocalContentDirectory(source);
            File.WriteAllText(GetLocalContentPath(source, record.payloadFileName), levelJson);

            if (!string.IsNullOrEmpty(record.previewFileName))
                File.WriteAllBytes(GetLocalContentPath(source, record.previewFileName), previewImageBytes);

            LevelCatalog catalog = LoadLocalCatalog(source);
            catalog.content.RemoveAll(existingRecord => existingRecord.id == record.id);
            catalog.content.Add(record);
            return SaveLocalCatalog(source, catalog);
        }
        catch (Exception exception)
        {
            Debug.Log("ERROR: " + source + " level could not be saved. Content: " + record.id + ". Error: " + exception.Message);
            return false;
        }
    }

    IEnumerator LoadGameCatalog(Action<LevelCatalog> onLoaded)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(GetGameContentPath(CatalogFileName)))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("ERROR: Game level catalog could not be loaded. Error: " + request.error);
                onLoaded?.Invoke(new LevelCatalog());
                yield break;
            }

            LevelCatalog catalog = JsonUtility.FromJson<LevelCatalog>(request.downloadHandler.text) ?? new LevelCatalog();
            if (catalog.content == null)
                catalog.content = new List<LevelCatalogRecord>();

            onLoaded?.Invoke(catalog);
        }
    }

    LevelCatalog LoadLocalCatalog(LevelSource source)
    {
        EnsureLocalContentDirectory(source);
        string catalogPath = GetLocalContentPath(source, CatalogFileName);
        if (!File.Exists(catalogPath))
        {
            LevelCatalog migratedCatalog = MigrateLegacyLocalContent(source);
            SaveLocalCatalog(source, migratedCatalog);
            return migratedCatalog;
        }

        try
        {
            LevelCatalog catalog = JsonUtility.FromJson<LevelCatalog>(File.ReadAllText(catalogPath)) ?? new LevelCatalog();
            if (catalog.content == null)
                catalog.content = new List<LevelCatalogRecord>();
            return catalog;
        }
        catch (Exception exception)
        {
            Debug.Log("ERROR: " + source + " level catalog could not be loaded. Error: " + exception.Message);
            return new LevelCatalog();
        }
    }

    LevelCatalog MigrateLegacyLocalContent(LevelSource source)
    {
        LevelCatalog catalog = new LevelCatalog();
        string contentDirectory = GetLocalContentDirectory(source);

        try
        {
            foreach (string payloadPath in Directory.GetFiles(contentDirectory, "*.json"))
            {
                string fileName = Path.GetFileName(payloadPath);
                if (fileName == CatalogFileName || fileName == LegacyPlayerIndexFileName)
                    continue;

                LegacyLevelMetadata metadata = JsonUtility.FromJson<LegacyLevelMetadata>(File.ReadAllText(payloadPath));
                string displayName = string.IsNullOrWhiteSpace(metadata.levelName) ? Path.GetFileNameWithoutExtension(payloadPath) : metadata.levelName;
                DateTime createdAt = File.GetCreationTimeUtc(payloadPath);
                DateTime updatedAt = File.GetLastWriteTimeUtc(payloadPath);
                catalog.content.Add(new LevelCatalogRecord
                {
                    id = Guid.NewGuid().ToString(),
                    contentType = "level",
                    displayName = displayName,
                    author = metadata.levelAuthor,
                    payloadFileName = fileName,
                    previewFileName = Path.ChangeExtension(fileName, ".png"),
                    createdAtUtcTicks = createdAt.Ticks,
                    updatedAtUtcTicks = updatedAt.Ticks
                });
            }
        }
        catch (Exception exception)
        {
            Debug.Log("ERROR: Existing " + source + " levels could not be migrated. Error: " + exception.Message);
        }

        return catalog;
    }

    bool SaveLocalCatalog(LevelSource source, LevelCatalog catalog)
    {
        try
        {
            EnsureLocalContentDirectory(source);
            File.WriteAllText(GetLocalContentPath(source, CatalogFileName), JsonUtility.ToJson(catalog, true));
            return true;
        }
        catch (Exception exception)
        {
            Debug.Log("ERROR: " + source + " level catalog could not be saved. Error: " + exception.Message);
            return false;
        }
    }

    string GetLocalContentPath(LevelSource source, string fileName)
    {
        return Path.Combine(GetLocalContentDirectory(source), fileName);
    }

    void EnsureLocalContentDirectory(LevelSource source)
    {
        string directory = GetLocalContentDirectory(source);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    string GetLocalContentDirectory(LevelSource source)
    {
        switch (source)
        {
            case LevelSource.MyLevels:
                return myLevelsDirectory;
            case LevelSource.DownloadedLevels:
                return downloadedLevelsDirectory;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, "Game levels are not stored in a local directory.");
        }
    }

    string GetGameContentPath(string fileName)
    {
        return Path.Combine(Application.streamingAssetsPath, GameLevelsDirectoryName, fileName);
    }

    static List<LevelCatalogRecord> SortRecords(LevelCatalog catalog, LevelSource source)
    {
        if (catalog == null || catalog.content == null)
            return new List<LevelCatalogRecord>();

        if (source == LevelSource.Game)
            return catalog.content.OrderBy(record => record.sortOrder).ThenBy(record => record.displayName).ToList();

        return catalog.content.OrderByDescending(record => record.updatedAtUtcTicks).ThenBy(record => record.displayName).ToList();
    }

    static string SanitizeReadableName(string displayName)
    {
        string sanitizedName = string.IsNullOrWhiteSpace(displayName) ? "custom-level" : displayName.Trim().ToLowerInvariant();
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            sanitizedName = sanitizedName.Replace(invalidCharacter, '-');

        sanitizedName = sanitizedName.Replace(' ', '-');
        while (sanitizedName.Contains("--"))
            sanitizedName = sanitizedName.Replace("--", "-");

        sanitizedName = sanitizedName.Trim('-');
        if (string.IsNullOrEmpty(sanitizedName))
            sanitizedName = "custom-level";

        const int maxReadableNameLength = 48;
        return sanitizedName.Length > maxReadableNameLength ? sanitizedName.Substring(0, maxReadableNameLength).Trim('-') : sanitizedName;
    }
}
