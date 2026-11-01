using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

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
        InitializeLevelStorage();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    #endregion

    #region Level File Formats
    [System.Serializable]
    struct LevelHeader
    {
        public byte loaderVersion;
    }

    // Version 1 stores objects in parent-first order. p is the only hierarchy data needed
    // -1 means the level root, otherwise it points to an earlier object.
    [System.Serializable]
    struct LevelV1
    {
        public string contentId;
        public byte loaderVersion;
        public string levelName;
        public string levelAuthor;
        public long savedAtUtcTicks;
        public float startX;
        public float startY;
        public List<LevelObjectV1> levelObjects;
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
    LevelStorage levelStorage;
    string levelLoadJson;
    string lastSavedLevelJson = string.Empty;
    bool isSavingLevel;
    int currentLevelRevision;

    public bool IsSavingLevel => isSavingLevel;
    // This identifies the current in-memory level instance. It changes whenever the
    // existing level contents are cleared or replaced, not when the user edits it.
    public int CurrentLevelRevision => currentLevelRevision;

    [Header("World Object References")]
    [SerializeField] GameObject levelObjectsContainer;
    [SerializeField] GameObject playerStartPoint;
    [SerializeField] TMP_InputField levelSaveNameInput;
    [SerializeField] TMP_InputField levelCodeToCopyInput;
    [SerializeField] TMP_InputField levelCodeInput;

    void InitializeLevelStorage()
    {
        playerLevelsDirectory = Application.persistentDataPath + "/playerLevels";
        string downloadedLevelsDirectory = Application.persistentDataPath + "/downloadedLevels";
        levelStorage = new LevelStorage(playerLevelsDirectory, downloadedLevelsDirectory);
        levelStorage.EnsureLocalContentDirectories();
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

    public void SetLevelCodeToCopyInputToLastSavedLevelJson()
    {
        levelCodeToCopyInput.text = lastSavedLevelJson;
    }

    public void LoadLevelFromLevelCodeInput()
    {
        levelLoadJson = levelCodeInput.text;
        LoadLevel();
    }

    #region Saving
    bool TryGenerateLevelObject(out LevelV1 level)
    {
        level = new LevelV1();

        // get level name input, or just create one if nothing was input
        string levelName = "Custom Level " + DateTime.Now.ToString("MMM dd yyyy h-mm-sstt");
        if (levelSaveNameInput != null && levelSaveNameInput.text != string.Empty)
            levelName = levelSaveNameInput.text.Trim();

        // set values to save
        level.contentId = Guid.NewGuid().ToString();
        level.levelName = levelName;
        level.levelAuthor = "TODO";
        level.loaderVersion = LOADER_VERSION;
        level.savedAtUtcTicks = DateTime.UtcNow.Ticks;
        level.startX = playerStartPoint.transform.position.x;
        level.startY = playerStartPoint.transform.position.y;

        if (!LevelObjectSerialization.TryCaptureChildren(levelObjectsContainer.transform, IsSupportedLevelObjectType, out level.levelObjects))
        {
            level = default;
            return false;
        }

        return true;
    }

    #region Copy Level To Clipboard
    #if UNITY_WEBGL && !UNITY_EDITOR
            [System.Runtime.InteropServices.DllImport("__Internal")]
            private static extern void CopyToClipboard(string str);
    #endif

    public void CopyLevelCodeToClipboard()
    {
        if (!TryGenerateLevelObject(out LevelV1 level))
            return;

        string json = JsonUtility.ToJson(level, false);

        #if UNITY_WEBGL && !UNITY_EDITOR
            CopyToClipboard(json);
        #else
            GUIUtility.systemCopyBuffer = json;
        #endif
    }
    #endregion

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

    // TODO(prefab-previews): Render isolated prefab copies on demand with a transparent
    // background, then cache the results. Review LevelObjectIconCamera for built-in icons
    // and handle translucent objects correctly in transparent PNGs.

    // Saving the JSON is synchronous; only preview capture and disk I/O need the coroutine.
    // Callers that temporarily alter editor-only state may safely restore it once this method
    // invokes onLevelSerialized, before the asynchronous part of the save begins.
    public void SaveLevel(Action onLevelSerialized = null)
    {
        if (isSavingLevel)
        {
            Debug.LogWarning("Level save was ignored because another save is already in progress.");
            return;
        }

        isSavingLevel = true;
        LevelV1 level;
        bool didGenerateLevel;
        try
        {
            didGenerateLevel = TryGenerateLevelObject(out level);
        }
        catch
        {
            isSavingLevel = false;
            throw;
        }
        finally
        {
            onLevelSerialized?.Invoke();
        }

        if (!didGenerateLevel)
        {
            isSavingLevel = false;
            return;
        }

        StartCoroutine(SaveLevelWithPreview(level));
    }

    IEnumerator SaveLevelWithPreview(LevelV1 level)
    {
        try
        {
            string json = JsonUtility.ToJson(level, false);

            string payloadFileName = levelStorage.CreateFileName(level.levelName, level.contentId, ".json");
            string previewFileName = Path.ChangeExtension(payloadFileName, ".png");
            byte[] previewImageBytes = null;
            yield return CaptureLevelPreview(capturedImageBytes => previewImageBytes = capturedImageBytes);

            bool didSave = levelStorage.SaveLevel(LevelSource.PlayerLevels, new LevelCatalogRecord
            {
                id = level.contentId,
                contentType = "level",
                displayName = level.levelName,
                author = level.levelAuthor,
                payloadFileName = payloadFileName,
                previewFileName = previewFileName,
                createdAtUtcTicks = level.savedAtUtcTicks,
                updatedAtUtcTicks = level.savedAtUtcTicks,
                sortOrder = 0
            }, json, previewImageBytes);

            if (!didSave)
                yield break;

            MarkLevelSourceDirty(LevelSource.PlayerLevels);
            lastSavedLevelJson = json;

            SetLevelCodeToCopyInputToLastSavedLevelJson();

            // TODO: display a message in game
            Debug.Log("Saved level.");
        }
        finally
        {
            isSavingLevel = false;
        }
    }
    #endregion

    #region Loading

    #region Load From Clipboard
    #if UNITY_WEBGL && !UNITY_EDITOR
            [DllImport("__Internal")]
            private static extern void ReadFromClipboard();
    #endif

    public void ReceiveClipboardText(string text)
    {
        levelLoadJson = text;
        Debug.Log("Clipboard text received: " + text);
    }

    public void GetLevelJsonFromClipboard()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
            ReadFromClipboard();
        #else
            levelLoadJson = GUIUtility.systemCopyBuffer;
        #endif
    }
    #endregion

    public IEnumerator LoadLevelFromPreview(LevelSource source, string contentId, Action<bool> onComplete)
    {
        if (string.IsNullOrEmpty(contentId))
        {
            Debug.Log("ERROR: Level preview ID is not valid.");
            onComplete?.Invoke(false);
            yield break;
        }

        List<LevelCatalogRecord> records = null;
        yield return levelStorage.LoadCatalog(source, loadedRecords => records = loadedRecords);
        LevelCatalogRecord record = records?.FirstOrDefault(candidate => candidate.id == contentId);
        if (record == null)
        {
            Debug.Log("ERROR: Content catalog entry could not be found. ID: " + contentId);
            onComplete?.Invoke(false);
            yield break;
        }

        string json = null;
        yield return levelStorage.LoadPayload(source, record, loadedJson => json = loadedJson);
        if (string.IsNullOrEmpty(json))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        levelLoadJson = json;

        LoadLevel();
        onComplete?.Invoke(true);
    }

    public void LoadLevel()
    {
        if (string.IsNullOrWhiteSpace(levelLoadJson))
        {
            Debug.LogError("Level could not be loaded because its JSON is empty.");
            return;
        }

        try
        {
            if (TryParseLevel(levelLoadJson, out LevelV1 level))
                LoadLevelVersionOne(level);
        }
        catch (Exception exception)
        {
            Debug.LogError("Level could not be loaded because its JSON is invalid. " + exception.Message);
        }
    }

    static bool TryParseLevel(string json, out LevelV1 level)
    {
        LevelHeader header = JsonUtility.FromJson<LevelHeader>(json);
        switch (header.loaderVersion)
        {
            case 1:
                level = JsonUtility.FromJson<LevelV1>(json);
                return true;
            default:
                Debug.LogError("Level could not be loaded because loader version " + header.loaderVersion + " is not supported.");
                level = default;
                return false;
        }
    }

    void LoadLevelVersionOne(LevelV1 loadedLevel)
    {
        BeginLoadingLevel(loadedLevel.startX, loadedLevel.startY);

        LevelObjectSerialization.InstantiateObjects(loadedLevel.levelObjects, levelObjectsContainer.transform, CreateLevelObject);

        FinishLoadingLevel();
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

    GameObject GetPrefabForType(string type)
    {
        switch (type)
        {
            case "Booster":
                return BoosterPrefab;
            case "BouncyWall":
                return BouncyWallPrefab;
            case "ConstantBooster":
                return ConstantBoosterPrefab;
            case "ConstantMomentumRedirector":
                return ConstantMomentumRedirectorPrefab;
            case "ConstantPuller":
                return ConstantPullerPrefab;
            case "ConstantPusher":
                return ConstantPusherPrefab;
            case "Finish":
                return FinishPrefab;
            case "KillCircle":
                return KillCirclePrefab;
            case "KillWall":
                return KillWallPrefab;
            case "MomentumRedirector":
                return MomentumRedirectorPrefab;
            case "Puller":
                return PullerPrefab;
            case "Pusher":
                return PusherPrefab;
            case "SlipperyWall":
                return SlipperyWallPrefab;
            case "Text":
                return TextPrefab;
            default:
                return null;
        }
    }

    public IEnumerator LoadLevelCatalog(LevelSource source, Action<List<LevelCatalogRecord>> onLoaded)
    {
        yield return levelStorage.LoadCatalog(source, onLoaded);
    }

    public IEnumerator LoadLevelPreview(LevelSource source, LevelCatalogRecord record, Action<Texture2D> onLoaded)
    {
        yield return levelStorage.LoadPreview(source, record, onLoaded);
    }

    public void MarkLevelSourceDirty(LevelSource source)
    {
        UIManager.Instance.MarkLevelSourceDirty(source);
    }
    #endregion
}
