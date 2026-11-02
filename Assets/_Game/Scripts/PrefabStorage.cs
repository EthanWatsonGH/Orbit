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

// Player prefabs have their own catalog and files; level catalogs remain unchanged.
public sealed class PrefabStorage
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void SyncPrefabStorage();
#endif
    const string CatalogFileName = "prefab-catalog.json";
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
