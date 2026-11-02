using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

// Generate static, transparent icons from the same base prefabs used for placement.
[InitializeOnLoad]
public sealed class BaseObjectIconBuilder : IPreprocessBuildWithReport
{
    const string ManagerPath = "Assets/_Game/Prefabs/OnlyOnePerLevel/Managers/LevelManager.prefab";
    const string CapturePath = "Assets/_Game/Scripts/PrefabPreviewCapture.cs";
    const string IconDirectory = "Assets/_Game/Resources/" + BaseObjectCatalog.IconResourceFolder;
    const string CachePath = "Library/BaseObjectIconCache.json";
    const string CacheVersion = "1";

    [Serializable] sealed class Cache { public List<CacheEntry> entries = new List<CacheEntry>(); }
    [Serializable] sealed class CacheEntry { public string type; public string hash; }

    static BaseObjectIconBuilder()
    {
        EditorApplication.playModeStateChanged -= BeforePlayMode;
        EditorApplication.playModeStateChanged += BeforePlayMode;
    }

    public int callbackOrder => 0;

    static void BeforePlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        try { RebuildIcons(); }
        catch (Exception exception) { Debug.LogError("Base object icons could not be generated: " + exception.Message); }
    }

    [MenuItem("Orbit/Prefabs/Refresh Base Object Icons")]
    public static void RefreshIcons()
    {
        try { RebuildIcons(); }
        catch (Exception exception) { Debug.LogError("Base object icons could not be generated: " + exception.Message); }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        try { RebuildIcons(); }
        catch (Exception exception) { throw new BuildFailedException("Base object icons could not be generated: " + exception.Message); }
    }

    static void RebuildIcons()
    {
        GameObject managerObject = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPath);
        LevelManager manager = managerObject != null ? managerObject.GetComponent<LevelManager>() : null;
        if (manager == null)
            throw new InvalidOperationException("The level manager prefab is missing.");

        string[] types = BaseObjectCatalog.All.Select(entry => entry.Type).Distinct()
            .OrderBy(type => type, StringComparer.Ordinal).ToArray();
        if (types.Length == 0)
            throw new InvalidOperationException("The object selection bar has no active base objects.");

        Cache cache = ReadCache();
        Cache newCache = new Cache();
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
            AssetDatabase.CreateFolder("Assets/_Game", "Resources");
        if (!AssetDatabase.IsValidFolder(IconDirectory))
            AssetDatabase.CreateFolder("Assets/_Game/Resources", BaseObjectCatalog.IconResourceFolder);
        int generated = 0;
        foreach (string type in types)
        {
            GameObject prefab = manager.GetPrefabForType(type);
            string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("No base prefab is assigned for '" + type + "'.");
            if (!string.Equals(type, Path.GetFileName(type), StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid base object type: " + type);

            string iconPath = IconDirectory + "/" + type + ".png";
            string hash = CacheVersion + ":" + AssetDatabase.GetAssetDependencyHash(path) + ":" +
                          AssetDatabase.GetAssetDependencyHash(CapturePath) + ":" + QualitySettings.activeColorSpace;
            CacheEntry previous = cache.entries.Find(entry => entry.type == type);
            bool needsRender = previous == null || previous.hash != hash || !File.Exists(iconPath);
            if (needsRender)
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException("A graphics device is required to render an icon for '" + type + "'.");
                byte[] png = CapturePrefab(prefab, type);
                if (png == null || png.Length == 0)
                    throw new InvalidOperationException("The base prefab '" + type + "' has no renderable preview.");
                if (!File.Exists(iconPath) || !File.ReadAllBytes(iconPath).SequenceEqual(png))
                    File.WriteAllBytes(iconPath, png);
                AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                generated++;
            }
            EnsureSpriteImporter(iconPath);
            newCache.entries.Add(new CacheEntry { type = type, hash = hash });
        }
        File.WriteAllText(CachePath, JsonUtility.ToJson(newCache));
        if (generated > 0)
            Debug.Log("Generated " + generated + " base object icon(s).");
    }

    static byte[] CapturePrefab(GameObject prefab, string type)
    {
        GameObject staging = new GameObject("Base Icon Preview " + type) { hideFlags = HideFlags.HideAndDontSave };
        staging.SetActive(false);
        try
        {
            UnityEngine.Object.Instantiate(prefab, staging.transform, false);
            return PrefabPreviewCapture.Capture(staging);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(staging);
        }
    }

    static Cache ReadCache()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                Cache result = JsonUtility.FromJson<Cache>(File.ReadAllText(CachePath));
                if (result?.entries != null && !result.entries.Exists(entry => entry == null))
                    return result;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Base object icon cache will be rebuilt: " + exception.Message);
        }
        return new Cache();
    }

    static void EnsureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("The generated icon could not be imported: " + path);
        if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
            importer.alphaIsTransparency && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed &&
            importer.sRGBTexture && importer.wrapMode == TextureWrapMode.Clamp)
            return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }
}
