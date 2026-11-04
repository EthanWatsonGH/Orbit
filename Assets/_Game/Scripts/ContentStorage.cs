using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// LevelSource is serialized on the existing level selection prefabs.
public enum LevelSource { Game, PlayerLevels, DownloadedLevels }
public enum ContentCollection
{
    GameLevels,
    PlayerLevels,
    // TODO(downloaded-levels): Validate downloads and save them through ContentStorage;
    // files copied directly into this folder do not automatically enter the catalog.
    DownloadedLevels,
    GamePrefabs,
    PlayerPrefabs
}
public enum ContentKind { Level = 1, Prefab = 2 }

[Serializable]
public sealed class ContentData
{
    public byte loaderVersion;
    public ContentKind kind;
    public string id;
    public string name;
    public string author;
    public long savedAtUtcTicks;
    public float startX;
    public float startY;
    public List<LevelObjectV1> objects;
}

[Serializable]
public sealed class ContentRecord
{
    public string id;
    public string displayName;
    public string author;
    public string payloadFileName;
    public string previewFileName;
    public long updatedAtUtcTicks;
    public int sortOrder;
}

[Serializable]
public sealed class ContentCatalog
{
    public List<ContentRecord> entries = new List<ContentRecord>();
}

[Serializable]
sealed class ContentOrder { public List<string> keys = new List<string>(); }

public static class PrefabOrder
{
    public static string BuiltInKey(string type) => "base:" + type;
    public static string PlayerKey(string id) => "player:" + id;
    public static List<string> Merge(IEnumerable<string> available, IEnumerable<string> saved)
    {
        var remaining = new HashSet<string>(available);
        var result = new List<string>();
        if (saved != null)
            foreach (string key in saved)
                if (key != null && remaining.Remove(key)) result.Add(key);
        foreach (string key in available)
            if (key != null && remaining.Remove(key)) result.Add(key);
        return result;
    }
}

// Owns catalogs, JSON, and preview images for every collection. Game levels are
// packaged in StreamingAssets; writable collections use the same catalog format.
public sealed class ContentStorage
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void SyncContentStorage();
#endif
    public const string CatalogFileName = "content-catalog.json";
    readonly string root;
    readonly Dictionary<string, Sprite> previews = new Dictionary<string, Sprite>();
    readonly Dictionary<ContentCollection, List<ContentRecord>> catalogs = new Dictionary<ContentCollection, List<ContentRecord>>();
    readonly Dictionary<ContentCollection, int> revisions = new Dictionary<ContentCollection, int>();

    public ContentStorage(string persistentRoot) { root = Path.Combine(persistentRoot, "content-v1"); }
    public static ContentCollection ForLevelSource(LevelSource source)
    {
        switch (source)
        {
            case LevelSource.Game: return ContentCollection.GameLevels;
            case LevelSource.PlayerLevels: return ContentCollection.PlayerLevels;
            case LevelSource.DownloadedLevels: return ContentCollection.DownloadedLevels;
            default: throw new ArgumentOutOfRangeException(nameof(source));
        }
    }
    public int Revision(ContentCollection collection) => revisions.TryGetValue(collection, out int value) ? value : 0;
    void Changed(ContentCollection collection)
    {
        catalogs.Remove(collection);
        revisions[collection] = Revision(collection) + 1;
    }
    string DirectoryFor(ContentCollection collection)
    {
        switch (collection)
        {
            case ContentCollection.PlayerLevels: return Path.Combine(root, "playerLevels");
            case ContentCollection.DownloadedLevels: return Path.Combine(root, "downloadedLevels");
            case ContentCollection.PlayerPrefabs: return Path.Combine(root, "playerPrefabs");
            default: throw new ArgumentException("Collection is not writable.", nameof(collection));
        }
    }
    static string GamePath(string file) => Path.Combine(Application.streamingAssetsPath, "gameLevels", file);
    static bool SafeFile(string file) => !string.IsNullOrWhiteSpace(file) && Path.GetFileName(file) == file;
    static bool ValidId(string id) => Guid.TryParse(id, out _);
    static string Key(ContentCollection collection, string id) => collection + ":" + id;
    static bool ValidCatalog(ContentCatalog catalog)
    {
        if (catalog?.entries == null) return false;
        var ids = new HashSet<string>();
        foreach (ContentRecord record in catalog.entries)
            if (record == null || string.IsNullOrWhiteSpace(record.id) || !ids.Add(record.id) ||
                !SafeFile(record.payloadFileName) ||
                (!string.IsNullOrEmpty(record.previewFileName) && !SafeFile(record.previewFileName))) return false;
        return true;
    }

    public List<ContentRecord> LocalEntries(ContentCollection collection)
    {
        if (catalogs.TryGetValue(collection, out List<ContentRecord> cached))
            return new List<ContentRecord>(cached);
        if (collection == ContentCollection.GamePrefabs)
        {
            List<ContentRecord> builtIns = BaseObjectCatalog.All.Select(item => new ContentRecord { id = item.Type, displayName = item.Name }).ToList();
            catalogs[collection] = builtIns;
            return new List<ContentRecord>(builtIns);
        }
        try
        {
            string path = Path.Combine(DirectoryFor(collection), CatalogFileName);
            if (!File.Exists(path))
            {
                catalogs[collection] = new List<ContentRecord>();
                return new List<ContentRecord>();
            }
            ContentCatalog catalog = JsonUtility.FromJson<ContentCatalog>(File.ReadAllText(path));
            if (!ValidCatalog(catalog)) throw new InvalidDataException("Invalid catalog.");
            catalogs[collection] = catalog.entries;
            return new List<ContentRecord>(catalog.entries);
        }
        catch (Exception exception) { Debug.LogError("Content catalog could not be read: " + exception.Message); return null; }
    }

    public IEnumerator LoadCatalog(ContentCollection collection, Action<List<ContentRecord>> onLoaded)
    {
        if (collection != ContentCollection.GameLevels) { onLoaded?.Invoke(LocalEntries(collection)); yield break; }
        if (catalogs.TryGetValue(collection, out List<ContentRecord> cached))
        {
            onLoaded?.Invoke(new List<ContentRecord>(cached));
            yield break;
        }
        using (UnityWebRequest request = UnityWebRequest.Get(GamePath(CatalogFileName)))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Game level catalog could not be read: " + request.error);
                onLoaded?.Invoke(null); yield break;
            }
            ContentCatalog catalog = null;
            try { catalog = JsonUtility.FromJson<ContentCatalog>(request.downloadHandler.text); }
            catch (Exception exception) { Debug.LogError("Game level catalog is invalid: " + exception.Message); }
            if (!ValidCatalog(catalog)) { Debug.LogError("Game level catalog is invalid."); onLoaded?.Invoke(null); yield break; }
            catalogs[collection] = catalog.entries;
            onLoaded?.Invoke(new List<ContentRecord>(catalog.entries));
        }
    }

    public string LocalCode(ContentCollection collection, ContentRecord record)
    {
        if (record == null) return null;
        if (collection == ContentCollection.GamePrefabs)
        {
            BaseObjectDefinition definition = BaseObjectCatalog.All.FirstOrDefault(item => item.Type == record.id);
            GameObject prefab = definition?.GetPrefab(LevelManager.Instance);
            if (prefab == null) return null;
            return JsonUtility.ToJson(new ContentData
            {
                loaderVersion = 1, kind = ContentKind.Prefab, id = record.id, name = definition.Name,
                objects = new List<LevelObjectV1> { new LevelObjectV1 { t = definition.Type, p = -1,
                    sx = prefab.transform.localScale.x, sy = prefab.transform.localScale.y } }
            }, false);
        }
        if (!SafeFile(record.payloadFileName)) return null;
        try
        {
            string path = Path.Combine(DirectoryFor(collection), record.payloadFileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) { Debug.LogError("Content could not be read: " + exception.Message); return null; }
    }
    public IEnumerator LoadCode(ContentCollection collection, ContentRecord record, Action<string> onLoaded)
    {
        if (record == null) { onLoaded?.Invoke(null); yield break; }
        if (collection != ContentCollection.GameLevels) { onLoaded?.Invoke(LocalCode(collection, record)); yield break; }
        if (!SafeFile(record.payloadFileName)) { onLoaded?.Invoke(null); yield break; }
        using (UnityWebRequest request = UnityWebRequest.Get(GamePath(record.payloadFileName)))
        {
            yield return request.SendWebRequest();
            onLoaded?.Invoke(request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text : null);
        }
    }

    public Sprite LocalPreview(ContentCollection collection, ContentRecord record)
    {
        if (record == null) return null;
        string key = Key(collection, record.id);
        if (previews.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        if (collection == ContentCollection.GamePrefabs)
        {
            BaseObjectDefinition definition = BaseObjectCatalog.All.FirstOrDefault(item => item.Type == record.id);
            return definition?.LoadIcon();
        }
        if (!SafeFile(record.previewFileName)) return null;
        Texture2D texture = null;
        try
        {
            string path = Path.Combine(DirectoryFor(collection), record.previewFileName);
            if (!File.Exists(path)) return null;
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(texture); return null; }
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            previews[key] = sprite;
            return sprite;
        }
        catch (Exception exception)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            Debug.LogWarning("Content preview could not be read: " + exception.Message); return null;
        }
    }
    public IEnumerator LoadPreview(ContentCollection collection, ContentRecord record, Action<Sprite> onLoaded)
    {
        if (record == null) { onLoaded?.Invoke(null); yield break; }
        if (collection != ContentCollection.GameLevels) { onLoaded?.Invoke(LocalPreview(collection, record)); yield break; }
        string key = Key(collection, record.id);
        if (previews.TryGetValue(key, out Sprite cached) && cached != null) { onLoaded?.Invoke(cached); yield break; }
        if (!SafeFile(record.previewFileName)) { onLoaded?.Invoke(null); yield break; }
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(GamePath(record.previewFileName)))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) { onLoaded?.Invoke(null); yield break; }
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            // Concurrent requests may have populated the same cache while this one ran.
            if (previews.TryGetValue(key, out cached) && cached != null)
            {
                UnityEngine.Object.Destroy(sprite); UnityEngine.Object.Destroy(texture); sprite = cached;
            }
            else previews[key] = sprite;
            onLoaded?.Invoke(sprite);
        }
    }

    public bool Save(ContentCollection collection, ContentData data, byte[] preview)
    {
        if (data == null || !ValidId(data.id) || preview == null || preview.Length == 0 ||
            (collection == ContentCollection.PlayerPrefabs ? data.kind != ContentKind.Prefab : data.kind != ContentKind.Level)) return false;
        string directory;
        try { directory = DirectoryFor(collection); } catch (ArgumentException) { return false; }
        string jsonPath = Path.Combine(directory, data.id + ".json");
        string pngPath = Path.Combine(directory, data.id + ".png");
        bool wroteJson = false, wrotePng = false;
        try
        {
            Directory.CreateDirectory(directory);
            List<ContentRecord> entries = LocalEntries(collection);
            if (entries == null || entries.Any(item => item.id == data.id) || File.Exists(jsonPath) || File.Exists(pngPath)) return false;
            wroteJson = true; File.WriteAllText(jsonPath, JsonUtility.ToJson(data, false));
            wrotePng = true; File.WriteAllBytes(pngPath, preview);
            entries.Add(new ContentRecord { id = data.id, displayName = data.name, author = data.author,
                payloadFileName = data.id + ".json", previewFileName = data.id + ".png", updatedAtUtcTicks = data.savedAtUtcTicks });
            WriteCatalog(directory, entries);
            Changed(collection); Sync(); return true;
        }
        catch (Exception exception)
        {
            try { if (wroteJson) File.Delete(jsonPath); if (wrotePng) File.Delete(pngPath); }
            catch (Exception cleanup) { Debug.LogWarning("Incomplete content files could not be removed: " + cleanup.Message); }
            Sync();
            Debug.LogError("Content could not be saved: " + exception.Message); return false;
        }
    }
    static void WriteCatalog(string directory, List<ContentRecord> entries)
    {
        string path = Path.Combine(directory, CatalogFileName);
        string previous = File.Exists(path) ? File.ReadAllText(path) : null;
        try { File.WriteAllText(path, JsonUtility.ToJson(new ContentCatalog { entries = entries }, false)); }
        catch
        {
            if (previous != null) File.WriteAllText(path, previous);
            else if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }
    public bool Rename(ContentCollection collection, string id, string name)
    {
        if (!ValidId(id) || string.IsNullOrWhiteSpace(name)) return false;
        string oldJson = null, oldCatalog = null, path = null, catalogPath = null;
        try
        {
            string directory = DirectoryFor(collection);
            catalogPath = Path.Combine(directory, CatalogFileName);
            oldCatalog = File.ReadAllText(catalogPath);
            ContentCatalog catalog = JsonUtility.FromJson<ContentCatalog>(oldCatalog);
            ContentRecord record = catalog?.entries?.SingleOrDefault(item => item != null && item.id == id);
            if (record == null || !SafeFile(record.payloadFileName)) return false;
            path = Path.Combine(directory, record.payloadFileName); oldJson = File.ReadAllText(path);
            ContentData data = JsonUtility.FromJson<ContentData>(oldJson);
            if (data == null || data.loaderVersion != 1 || data.id != id) return false;
            data.name = name.Trim(); record.displayName = data.name;
            File.WriteAllText(path, JsonUtility.ToJson(data, false)); WriteCatalog(directory, catalog.entries);
            Changed(collection); Sync(); return true;
        }
        catch (Exception exception)
        {
            try { if (oldJson != null && path != null) File.WriteAllText(path, oldJson); if (oldCatalog != null && catalogPath != null) File.WriteAllText(catalogPath, oldCatalog); }
            catch (Exception rollback) { Debug.LogError("Content rename rollback failed: " + rollback.Message); }
            Sync();
            Debug.LogError("Content could not be renamed: " + exception.Message); return false;
        }
    }
    public bool Delete(ContentCollection collection, string id)
    {
        if (!ValidId(id)) return false;
        try
        {
            string directory = DirectoryFor(collection);
            List<ContentRecord> entries = LocalEntries(collection);
            ContentRecord record = entries?.SingleOrDefault(item => item.id == id);
            if (record == null || !SafeFile(record.payloadFileName) || !SafeFile(record.previewFileName)) return false;
            entries.Remove(record); WriteCatalog(directory, entries);
            foreach (string file in new[] { record.payloadFileName, record.previewFileName })
                try { File.Delete(Path.Combine(directory, file)); } catch (Exception exception) { Debug.LogWarning("Deleted content file could not be removed: " + exception.Message); }
            string key = Key(collection, id);
            if (previews.TryGetValue(key, out Sprite sprite))
            {
                previews.Remove(key);
                if (sprite != null)
                {
                    Texture2D texture = sprite.texture;
                    UnityEngine.Object.Destroy(sprite);
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                }
            }
            Changed(collection); Sync(); return true;
        }
        catch (Exception exception) { Sync(); Debug.LogError("Content could not be deleted: " + exception.Message); return false; }
    }
    public List<string> LoadPrefabOrder()
    {
        try
        {
            string path = Path.Combine(DirectoryFor(ContentCollection.PlayerPrefabs), "order.json");
            if (!File.Exists(path)) return new List<string>();
            return JsonUtility.FromJson<ContentOrder>(File.ReadAllText(path))?.keys;
        }
        catch (Exception exception) { Debug.LogError("Prefab order could not be read: " + exception.Message); return null; }
    }
    public bool SavePrefabOrder(List<string> keys)
    {
        if (keys == null) return false;
        try
        {
            string directory = DirectoryFor(ContentCollection.PlayerPrefabs); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "order.json"), JsonUtility.ToJson(new ContentOrder { keys = keys }, false));
            Changed(ContentCollection.PlayerPrefabs); Sync(); return true;
        }
        catch (Exception exception) { Debug.LogError("Prefab order could not be saved: " + exception.Message); return false; }
    }
    static void Sync()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { SyncContentStorage(); } catch (Exception exception) { Debug.LogError("Content storage sync failed: " + exception.Message); }
#endif
    }
    public void Dispose()
    {
        foreach (Sprite sprite in previews.Values)
            if (sprite != null) { Texture2D texture = sprite.texture; UnityEngine.Object.Destroy(sprite); if (texture != null) UnityEngine.Object.Destroy(texture); }
        previews.Clear();
        catalogs.Clear();
    }
}
