using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

[Serializable]
public sealed class SavedObjectPrefab
{
    public string id;
    public byte loaderVersion;
    public string name;
    public long savedAtUtcTicks;
    public List<LevelObjectV1> objects;
}

[Serializable]
public sealed class SavedPrefabRecord
{
    public string id;
    public string name;
    public long savedAtUtcTicks;
}

[Serializable]
sealed class SavedPrefabCatalog
{
    public List<SavedPrefabRecord> prefabs = new List<SavedPrefabRecord>();
}

[Serializable]
sealed class SavedPrefabOrder
{
    public List<string> keys = new List<string>();
}

// The same order is used by the object bar and the library. New entries are
// appended, and missing entries are ignored without changing the stored order.
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

// Player prefabs have their own catalog and files; level catalogs remain unchanged.
public sealed class PrefabStorage
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void SyncPrefabStorage();
#endif
    const string CatalogFileName = "prefab-catalog.json";
    const string OrderFileName = "prefab-order.json";
    readonly string directory;

    public PrefabStorage(string directory)
    {
        this.directory = directory;
    }

    public List<SavedPrefabRecord> LoadCatalog()
    {
        string path = Path.Combine(directory, CatalogFileName);
        if (!File.Exists(path))
            return new List<SavedPrefabRecord>();

        try
        {
            SavedPrefabCatalog catalog = JsonUtility.FromJson<SavedPrefabCatalog>(File.ReadAllText(path));
            if (catalog?.prefabs == null || catalog.prefabs.Exists(record => record == null))
            {
                Debug.LogError("Prefab catalog could not be loaded because it is invalid.");
                return null;
            }
            return catalog.prefabs;
        }
        catch (Exception exception)
        {
            Debug.LogError("Prefab catalog could not be loaded: " + exception.Message);
            return null;
        }
    }

    public List<string> LoadOrder()
    {
        string path = Path.Combine(directory, OrderFileName);
        if (!File.Exists(path)) return new List<string>();
        try
        {
            SavedPrefabOrder order = JsonUtility.FromJson<SavedPrefabOrder>(File.ReadAllText(path));
            if (order?.keys == null) throw new InvalidDataException("Invalid prefab order.");
            return order.keys;
        }
        catch (Exception exception)
        {
            Debug.LogError("Prefab order could not be loaded: " + exception.Message);
            return null;
        }
    }

    public bool SaveOrder(List<string> keys)
    {
        if (keys == null) return false;
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, OrderFileName),
                JsonUtility.ToJson(new SavedPrefabOrder { keys = keys }, false));
#if UNITY_WEBGL && !UNITY_EDITOR
            try { SyncPrefabStorage(); }
            catch (Exception exception) { Debug.LogError("Prefab storage sync could not be requested: " + exception.Message); }
#endif
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Prefab order could not be saved: " + exception.Message);
            return false;
        }
    }

    public bool Save(SavedObjectPrefab prefab, byte[] preview)
    {
        if (prefab == null || !IsValidId(prefab.id) || prefab.objects == null || prefab.objects.Count == 0 ||
            preview == null || preview.Length == 0)
            return false;

        string payloadPath = GetPath(prefab.id, ".json");
        string previewPath = GetPath(prefab.id, ".png");
        bool createdPayload = false;
        bool createdPreview = false;
        try
        {
            Directory.CreateDirectory(directory);
            List<SavedPrefabRecord> records = LoadCatalog();
            if (records == null)
                return false;
            if (records.Exists(record => record.id == prefab.id) || File.Exists(payloadPath) || File.Exists(previewPath))
            {
                Debug.LogError("Prefab could not be saved because its ID already exists.");
                return false;
            }

            createdPayload = true;
            File.WriteAllText(payloadPath, JsonUtility.ToJson(prefab, false));
            createdPreview = true;
            File.WriteAllBytes(previewPath, preview);
            records.Add(new SavedPrefabRecord { id = prefab.id, name = prefab.name, savedAtUtcTicks = prefab.savedAtUtcTicks });
            File.WriteAllText(Path.Combine(directory, CatalogFileName),
                JsonUtility.ToJson(new SavedPrefabCatalog { prefabs = records }, false));
#if UNITY_WEBGL && !UNITY_EDITOR
            // IndexedDB writes are asynchronous; keep the committed files and
            // catalog together even if requesting the browser sync fails.
            try { SyncPrefabStorage(); }
            catch (Exception exception) { Debug.LogError("Prefab storage sync could not be requested: " + exception.Message); }
#endif
            return true;
        }
        catch (Exception exception)
        {
            try
            {
                if (createdPayload) File.Delete(payloadPath);
                if (createdPreview) File.Delete(previewPath);
            }
            catch (Exception cleanupException)
            {
                Debug.LogError("Incomplete prefab files could not be removed: " + cleanupException.Message);
            }
            Debug.LogError("Prefab could not be saved: " + exception.Message);
            return false;
        }
    }

    public bool Rename(string id, string name)
    {
        if (!IsValidId(id) || string.IsNullOrWhiteSpace(name)) return false;
        name = name.Trim();
        string catalogPath = Path.Combine(directory, CatalogFileName);
        string prefabPath = GetPath(id, ".json");
        string previousCatalog = null;
        string previousPrefab = null;
        bool prefabChanged = false;
        bool catalogChanged = false;
        try
        {
            previousCatalog = File.ReadAllText(catalogPath);
            SavedPrefabCatalog catalog = JsonUtility.FromJson<SavedPrefabCatalog>(previousCatalog);
            if (catalog?.prefabs == null || catalog.prefabs.Exists(record => record == null) ||
                catalog.prefabs.FindAll(record => record.id == id).Count != 1)
                throw new InvalidDataException("The prefab catalog does not contain one matching entry.");
            previousPrefab = File.ReadAllText(prefabPath);
            SavedObjectPrefab prefab = JsonUtility.FromJson<SavedObjectPrefab>(previousPrefab);
            // Avoid rewriting data from a format this version does not understand.
            if (prefab == null || prefab.id != id || prefab.loaderVersion != 1 || prefab.objects == null)
                throw new InvalidDataException("The saved prefab is invalid or uses an unsupported version.");
            prefab.name = name;
            catalog.prefabs.Find(record => record.id == id).name = name;
            prefabChanged = true;
            File.WriteAllText(prefabPath, JsonUtility.ToJson(prefab, false));
            catalogChanged = true;
            File.WriteAllText(catalogPath, JsonUtility.ToJson(catalog, false));
            RequestSync();
            return true;
        }
        catch (Exception exception)
        {
            if (prefabChanged)
            {
                try { File.WriteAllText(prefabPath, previousPrefab); }
                catch (Exception rollbackException) { Debug.LogError("Prefab data could not be restored: " + rollbackException.Message); }
            }
            if (catalogChanged)
            {
                try { File.WriteAllText(catalogPath, previousCatalog); }
                catch (Exception rollbackException) { Debug.LogError("Prefab catalog could not be restored: " + rollbackException.Message); }
            }
            if (prefabChanged || catalogChanged) RequestSync();
            Debug.LogError("Prefab could not be renamed: " + exception.Message);
            return false;
        }
    }

    public bool Delete(string id)
    {
        if (!IsValidId(id)) return false;
        string catalogPath = Path.Combine(directory, CatalogFileName);
        string previousCatalog = null;
        bool catalogChanged = false;
        try
        {
            previousCatalog = File.ReadAllText(catalogPath);
            SavedPrefabCatalog catalog = JsonUtility.FromJson<SavedPrefabCatalog>(previousCatalog);
            if (catalog?.prefabs == null || catalog.prefabs.Exists(record => record == null) ||
                catalog.prefabs.FindAll(record => record.id == id).Count != 1)
                throw new InvalidDataException("The prefab catalog does not contain one matching entry.");
            catalog.prefabs.RemoveAll(record => record.id == id);
            catalogChanged = true;
            File.WriteAllText(catalogPath, JsonUtility.ToJson(catalog, false));
        }
        catch (Exception exception)
        {
            if (catalogChanged)
            {
                try { File.WriteAllText(catalogPath, previousCatalog); RequestSync(); }
                catch (Exception rollbackException) { Debug.LogError("Prefab deletion could not be rolled back: " + rollbackException.Message); }
            }
            Debug.LogError("Prefab could not be deleted: " + exception.Message);
            return false;
        }

        // The catalog is authoritative; report any leftover files if cleanup fails.
        foreach (string extension in new[] { ".json", ".png" })
        {
            try { File.Delete(GetPath(id, extension)); }
            catch (Exception exception) { Debug.LogWarning("Deleted prefab file could not be removed: " + exception.Message); }
        }
        RequestSync();
        return true;
    }

    static void RequestSync()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { SyncPrefabStorage(); }
        catch (Exception exception) { Debug.LogError("Prefab storage sync could not be requested: " + exception.Message); }
#endif
    }

    public string LoadJson(string id)
    {
        if (!IsValidId(id))
            return null;
        try
        {
            string path = GetPath(id, ".json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception)
        {
            Debug.LogError("Prefab could not be loaded: " + exception.Message);
            return null;
        }
    }

    public byte[] LoadPreview(string id)
    {
        if (!IsValidId(id))
            return null;
        try
        {
            string path = GetPath(id, ".png");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception exception)
        {
            Debug.LogError("Prefab preview could not be loaded: " + exception.Message);
            return null;
        }
    }

    static bool IsValidId(string id) => Guid.TryParseExact(id, "N", out _);
    string GetPath(string id, string extension) => Path.Combine(directory, id + extension);
}
