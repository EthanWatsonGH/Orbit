using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class LevelManager : MonoBehaviour
{
    #region Singleton Setup
    public static LevelManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Duplicate LevelManager in the scene.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeContentStorage();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Content?.Dispose();
            Instance = null;
        }
    }
    #endregion

    [Header("Level Editor Placable Objects Prefabs")]
    public GameObject BoosterPrefab;
    public GameObject BouncyWallPrefab;
    public GameObject ConstantBoosterPrefab;
    public GameObject ConstantMomentumRedirectorPrefab;
    public GameObject ConstantPullerPrefab;
    public GameObject ConstantPusherPrefab;
    public GameObject FinishPrefab;
    public GameObject KillCirclePrefab;
    public GameObject KillWallPrefab;
    public GameObject MomentumRedirectorPrefab;
    public GameObject PullerPrefab;
    public GameObject PusherPrefab;
    public GameObject SlipperyWallPrefab;
    public GameObject TextPrefab;

    public const string GroupObjectType = "Group";
    public const byte LOADER_VERSION = 1;
    public string playerLevelsDirectory { get; private set; }
    public ContentStorage Content { get; private set; }
    bool isSavingLevel;
    int currentLevelRevision;

    public bool IsSavingLevel => isSavingLevel;
    // This identifies the current in-memory level instance. It changes whenever the
    // existing level contents are cleared or replaced, not when the user edits it.
    public int CurrentLevelRevision => currentLevelRevision;

    [Header("World Object References")]
    [SerializeField] GameObject levelObjectsContainer;
    [SerializeField] GameObject playerStartPoint;

    void InitializeContentStorage()
    {
        playerLevelsDirectory = System.IO.Path.Combine(Application.persistentDataPath, "content-v1", "playerLevels");
        Content = new ContentStorage(Application.persistentDataPath);
    }

    public void DestroyAllExistingLevelObjects()
    {
        currentLevelRevision++;

        // reset player start point position to center of world
        playerStartPoint.transform.position = Vector3.zero;

        EventManager.Instance.RecenterCamera();

        foreach (Transform levelObject in levelObjectsContainer.transform)
        {
            Destroy(levelObject.gameObject);
        }
    }

    #region Saving
    bool TryGenerateLevelObject(string name, out ContentData level)
    {
        level = new ContentData
        {
            loaderVersion = LOADER_VERSION,
            kind = ContentKind.Level,
            id = Guid.NewGuid().ToString("N"),
            name = string.IsNullOrWhiteSpace(name) ? "Custom Level " + DateTime.Now.ToString("MMM dd yyyy h-mm-sstt") : name.Trim(),
            author = "TODO",
            savedAtUtcTicks = DateTime.UtcNow.Ticks,
            startX = playerStartPoint.transform.position.x,
            startY = playerStartPoint.transform.position.y
        };
        if (TryCaptureObjects(levelObjectsContainer.transform, out List<LevelObjectV1> objects))
        {
            level.objects = objects;
            if (ValidateContent(level)) return true;
        }
        level = null;
        return false;
    }

    public bool TryCaptureObjects(Transform parent, out List<LevelObjectV1> objects)
    {
        return LevelObjectSerialization.TryCaptureChildren(parent, IsSupportedLevelObjectType, out objects);
    }

    public bool TryReadContent(string json, ContentKind expectedKind, out ContentData data)
    {
        return ContentSerialization.TryRead(json, expectedKind, IsSupportedLevelObjectType, out data);
    }

    bool ValidateContent(ContentData data) => ContentSerialization.Validate(data, IsSupportedLevelObjectType);

    bool TryInstantiateContent(ContentData data, Transform parent, out List<Transform> created)
    {
        created = null;
        if (data == null || parent == null || !ValidateContent(data)) return false;
        GameObject staging = new GameObject("Content Load Staging") { hideFlags = HideFlags.HideAndDontSave };
        staging.SetActive(false);
        staging.transform.SetParent(parent, false);
        try
        {
            List<Transform> items = LevelObjectSerialization.InstantiateObjects(data.objects, staging.transform, CreateLevelObject);
            if (items.Count != data.objects.Count || items.Any(item => item == null)) return false;
            foreach (Transform item in items)
                if (item.parent == staging.transform) item.SetParent(parent, false);
            created = items;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Content objects could not be created: " + exception.Message);
            return false;
        }
        finally { staging.transform.SetParent(null, false); Destroy(staging); }
    }

    public bool TryInstantiatePrefab(ContentData prefab, Transform parent, out GameObject root)
    {
        root = null;
        if (prefab == null || prefab.kind != ContentKind.Prefab || !TryInstantiateContent(prefab, parent, out List<Transform> created) || created.Count == 0) return false;
        root = created[0].gameObject;
        return true;
    }

    public bool TryGetCurrentLevelCode(out string code)
    {
        code = null;
        if (!TryGenerateLevelObject(null, out ContentData level)) return false;
        code = JsonUtility.ToJson(level, false);
        return true;
    }

    IEnumerator CaptureLevelPreview(Action<byte[]> onComplete)
    {
        // Wait for the current editor camera position to settle for this frame.
        yield return new WaitForEndOfFrame();

        if (CameraViewManager.Instance == null ||
            !CameraViewManager.Instance.TryGetLevelPreviewCameras(out Camera worldCamera, out Camera previewCamera))
        {
            Debug.LogError("Level preview could not be captured because its cameras are unavailable.", this);
            onComplete?.Invoke(null);
            yield break;
        }

        int width = worldCamera.pixelWidth;
        int height = worldCamera.pixelHeight;
        if (width <= 0 || height <= 0)
        {
            Debug.LogError("Level preview could not be captured because the active camera has no renderable area.", this);
            onComplete?.Invoke(null);
            yield break;
        }

        onComplete?.Invoke(RenderLevelPreview(worldCamera, previewCamera, width, height));
    }

    static byte[] RenderLevelPreview(Camera worldCamera, Camera previewCamera, int width, int height)
    {
        // This camera has its own culling mask and never renders the screen-space UI.
        // Match the visible world view while keeping the preview background opaque.
        previewCamera.enabled = false;
        previewCamera.transform.SetPositionAndRotation(worldCamera.transform.position, worldCamera.transform.rotation);
        previewCamera.orthographic = worldCamera.orthographic;
        previewCamera.orthographicSize = worldCamera.orthographicSize;
        previewCamera.fieldOfView = worldCamera.fieldOfView;
        previewCamera.nearClipPlane = worldCamera.nearClipPlane;
        previewCamera.farClipPlane = worldCamera.farClipPlane;
        previewCamera.aspect = worldCamera.aspect;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        Color background = worldCamera.backgroundColor;
        background.a = 1f;
        previewCamera.backgroundColor = background;

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = previewCamera.targetTexture;
        RenderTexture renderTarget = null;
        Texture2D image = null;
        try
        {
            renderTarget = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            previewCamera.targetTexture = renderTarget;
            previewCamera.Render();
            RenderTexture.active = renderTarget;
            // RGB24 makes the PNG opaque without altering the already-composited colors.
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            return image.EncodeToPNG();
        }
        finally
        {
            previewCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            if (renderTarget != null)
                RenderTexture.ReleaseTemporary(renderTarget);
            if (image != null)
                Destroy(image);
        }

    }

    public bool SaveLevel(string name, Action<bool> onComplete = null)
    {
        if (isSavingLevel)
            return false;

        if (!TryGenerateLevelObject(name, out ContentData level))
            return false;

        isSavingLevel = true;
        StartCoroutine(SaveLevelWithPreview(level, onComplete));
        return true;
    }

    IEnumerator SaveLevelWithPreview(ContentData level, Action<bool> onComplete)
    {
        bool didSave = false;
        try
        {
            byte[] previewImageBytes = null;
            yield return CaptureLevelPreview(capturedImageBytes => previewImageBytes = capturedImageBytes);
            didSave = Content.Save(ContentCollection.PlayerLevels, level, previewImageBytes);

            if (didSave)
                MarkLevelSourceDirty(LevelSource.PlayerLevels);
        }
        finally
        {
            isSavingLevel = false;
            onComplete?.Invoke(didSave);
        }
    }
    #endregion

    #region Loading

    public IEnumerator LoadLevelFromPreview(LevelSource source, string contentId, Action<bool> onComplete)
    {
        ContentCollection collection = ContentStorage.ForLevelSource(source);
        List<ContentRecord> records = null;
        yield return Content.LoadCatalog(collection, result => records = result);
        ContentRecord record = records?.FirstOrDefault(item => item.id == contentId);
        if (record == null) { onComplete?.Invoke(false); yield break; }
        string code = null;
        yield return Content.LoadCode(collection, record, result => code = result);
        onComplete?.Invoke(TryLoadLevelCode(code));
    }

    public bool TryLoadLevelCode(string json)
    {
        if (!TryReadContent(json, ContentKind.Level, out ContentData level)) return false;
        // Prepare everything before touching the current world.
        GameObject staging = new GameObject("Level Load Staging") { hideFlags = HideFlags.HideAndDontSave };
        staging.SetActive(false);
        try
        {
            if (!TryInstantiateContent(level, staging.transform, out List<Transform> created)) return false;
            BeginLoadingLevel(level.startX, level.startY);
            foreach (Transform item in created)
                if (item != null && item.parent == staging.transform)
                    item.SetParent(levelObjectsContainer.transform, false);
            FinishLoadingLevel();
            return true;
        }
        finally { Destroy(staging); }
    }

    void BeginLoadingLevel(float playerStartXPosition, float playerStartYPosition)
    {
        DestroyAllExistingLevelObjects();
        playerStartPoint.transform.position = new Vector3(playerStartXPosition, playerStartYPosition, 0f);
    }

    void FinishLoadingLevel()
    {
        LevelEditor.NormalizeRotationInvariantCircularObjectRotations(levelObjectsContainer.transform);
        EventManager.Instance.RecenterCamera();
        EventManager.Instance.OnLevelLoad();
        Debug.Log("Loaded level.");
    }

    GameObject CreateLevelObject(string type, Transform parentTransform)
    {
        string normalizedType = type?.Trim();
        if (normalizedType == GroupObjectType)
        {
            GameObject groupObject = new GameObject(GroupObjectType);
            groupObject.transform.SetParent(parentTransform, false);
            return groupObject;
        }

        GameObject prefabToInstantiate = GetPrefabForType(normalizedType);
        if (prefabToInstantiate == null)
        {
            Debug.LogError("Level object could not be loaded because type '" + type + "' is not recognized.");
            return null;
        }

        return Instantiate(prefabToInstantiate, parentTransform);
    }

    bool IsSupportedLevelObjectType(string type)
    {
        return type == GroupObjectType || GetPrefabForType(type) != null;
    }

    public GameObject GetPrefabForType(string type)
    {
        return BaseObjectCatalog.Find(type)?.GetPrefab(this);
    }

    public IEnumerator LoadLevelCatalog(LevelSource source, Action<List<ContentRecord>> onLoaded, bool useCatalogOrder = false)
    {
        List<ContentRecord> records = null;
        yield return Content.LoadCatalog(ContentStorage.ForLevelSource(source), result => records = result);
        if (!useCatalogOrder && records != null)
            records = source == LevelSource.Game ? records.OrderBy(item => item.sortOrder).ThenBy(item => item.displayName).ToList()
                : records.OrderByDescending(item => item.updatedAtUtcTicks).ThenBy(item => item.displayName).ToList();
        onLoaded?.Invoke(records);
    }

    public IEnumerator LoadLevelCode(LevelSource source, ContentRecord record, Action<string> onLoaded)
    {
        yield return Content.LoadCode(ContentStorage.ForLevelSource(source), record, onLoaded);
    }

    public IEnumerator LoadLevelPreview(LevelSource source, ContentRecord record, Action<Sprite> onLoaded)
    {
        yield return Content.LoadPreview(ContentStorage.ForLevelSource(source), record, onLoaded);
    }

    public void MarkLevelSourceDirty(LevelSource source)
    {
        UIManager.Instance.MarkLevelSourceDirty(source);
    }
    #endregion
}
