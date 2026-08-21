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

    // Version 1 remains only so existing saved and built-in levels can still load.
    [System.Serializable]
    struct LevelV1
    {
        public string contentId;
        public byte loaderVersion;
        public string levelName;
        public string levelAuthor;
        public float playerStartPointXPosition;
        public float playerStartPointYPosition;
        public List<LevelObjectV1> levelObjects;

        [System.Serializable]
        public struct LevelObjectV1
        {
            public string type;
            public float xPosition;
            public float yPosition;
            public float xScale;
            public float yScale;
            public float rotation;
        }
    }

    // Version 2 stores the same objects in parent-first order. parentIndex is the only
    // hierarchy data needed: -1 means the level root, otherwise it points to an earlier object.
    [System.Serializable]
    struct LevelV2
    {
        public string contentId;
        public byte loaderVersion;
        public string levelName;
        public string levelAuthor;
        public long savedAtUtcTicks;
        public float playerStartPointXPosition;
        public float playerStartPointYPosition;
        public List<LevelObjectV2> levelObjects;

        [System.Serializable]
        public struct LevelObjectV2
        {
            // "Group" is reserved for an empty hierarchy object. Other values are prefab names.
            public string type;
            public int parentIndex;
            public float xPosition;
            public float yPosition;
            public float xScale;
            public float yScale;
            public float rotation;
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

    const string GroupObjectType = "Group";
    public const byte LOADER_VERSION = 2;
    public string playerLevelsDirectory { get; private set; }
    LevelStorage levelStorage;
    string levelLoadJson;
    string lastSavedLevelJson = string.Empty;
    bool isSavingLevel;

    public bool IsSavingLevel => isSavingLevel;

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
    bool TryGenerateLevelObject(out LevelV2 level)
    {
        level = new LevelV2();
        level.levelObjects = new List<LevelV2.LevelObjectV2>();

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
        level.playerStartPointXPosition = playerStartPoint.transform.position.x;
        level.playerStartPointYPosition = playerStartPoint.transform.position.y;

        if (!AppendLevelObjects(levelObjectsContainer.transform, -1, level.levelObjects))
        {
            level = default;
            return false;
        }

        return true;
    }

    bool AppendLevelObjects(Transform parentTransform, int parentIndex, List<LevelV2.LevelObjectV2> serializedObjects)
    {
        foreach (Transform childTransform in parentTransform)
        {
            string type = GetLevelObjectType(childTransform);
            if (IsTemporarySelectionGroupType(type))
            {
                Debug.LogError("Level could not be saved because a temporary selection group was still present. Deselect the objects and try again.", childTransform);
                return false;
            }

            if (!IsSupportedLevelObjectType(type))
            {
                Debug.LogError("Level could not be saved because '" + childTransform.name + "' is not a recognized level object or Group.", childTransform);
                return false;
            }

            int childIndex = serializedObjects.Count;
            serializedObjects.Add(new LevelV2.LevelObjectV2
            {
                type = type,
                parentIndex = parentIndex,
                xPosition = childTransform.localPosition.x,
                yPosition = childTransform.localPosition.y,
                xScale = childTransform.localScale.x,
                yScale = childTransform.localScale.y,
                rotation = childTransform.localEulerAngles.z
            });

            if (!AppendLevelObjects(childTransform, childIndex, serializedObjects))
                return false;
        }

        return true;
    }

    static bool IsTemporarySelectionGroupType(string type)
    {
        return type == LevelEditor.TemporarySelectionGroupName ||
               type == LevelEditor.DuplicatingSelectionGroupName;
    }

    static string GetLevelObjectType(Transform levelObjectTransform)
    {
        const string cloneSuffix = "(Clone)";
        string type = levelObjectTransform.name.Trim();

        if (type.EndsWith(cloneSuffix, StringComparison.Ordinal))
            type = type.Substring(0, type.Length - cloneSuffix.Length).TrimEnd();

        return type;
    }

    #region Copy Level To Clipboard
    #if UNITY_WEBGL && !UNITY_EDITOR
            [System.Runtime.InteropServices.DllImport("__Internal")]
            private static extern void CopyToClipboard(string str);
    #endif

    public void CopyLevelCodeToClipboard()
    {
        if (!TryGenerateLevelObject(out LevelV2 level))
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
        UIManager.Instance.HideUiForPreviewCapture();

        // wait until the end of the frame before taking the screenshot since the UI is actually hidden at the end of the frame
        yield return new WaitForEndOfFrame();

        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] previewImageBytes = screenshot.EncodeToPNG();
        Destroy(screenshot);

        UIManager.Instance.RestoreUiAfterPreviewCapture();

        onComplete?.Invoke(previewImageBytes);
    }

    public void SaveLevel(Action onFinished = null)
    {
        if (isSavingLevel)
        {
            Debug.LogWarning("Level save was ignored because another save is already in progress.");
            return;
        }

        isSavingLevel = true;
        StartCoroutine(SaveLevelWithPreview(onFinished));
    }

    IEnumerator SaveLevelWithPreview(Action onFinished)
    {
        try
        {
            if (!TryGenerateLevelObject(out LevelV2 level))
                yield break;

            string json = JsonUtility.ToJson(level, true);

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
            onFinished?.Invoke();
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
            LevelHeader header = JsonUtility.FromJson<LevelHeader>(levelLoadJson);
            switch (header.loaderVersion)
            {
            case 1:
                LoadLevelVersionOne(JsonUtility.FromJson<LevelV1>(levelLoadJson));
                break;
            case 2:
                LoadLevelVersionTwo(JsonUtility.FromJson<LevelV2>(levelLoadJson));
                break;
            default:
                Debug.LogError("Level could not be loaded because loader version " + header.loaderVersion + " is not supported.");
                break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("Level could not be loaded because its JSON is invalid. " + exception.Message);
        }
    }

    void LoadLevelVersionOne(LevelV1 loadedLevel)
    {
        BeginLoadingLevel(loadedLevel.playerStartPointXPosition, loadedLevel.playerStartPointYPosition);

        foreach (LevelV1.LevelObjectV1 levelObject in loadedLevel.levelObjects ?? new List<LevelV1.LevelObjectV1>())
        {
            GameObject prefabToInstantiate = GetPrefabForType(levelObject.type);
            if (prefabToInstantiate == null)
            {
                Debug.LogError("Version 1 level object could not be loaded because type '" + levelObject.type + "' is not recognized.");
                continue;
            }

            GameObject loadedObject = Instantiate(
                prefabToInstantiate,
                new Vector3(levelObject.xPosition, levelObject.yPosition, 0f),
                Quaternion.Euler(0f, 0f, levelObject.rotation),
                levelObjectsContainer.transform);
            loadedObject.transform.localScale = new Vector3(levelObject.xScale, levelObject.yScale, 1f);
        }

        FinishLoadingLevel();
    }

    void LoadLevelVersionTwo(LevelV2 loadedLevel)
    {
        BeginLoadingLevel(loadedLevel.playerStartPointXPosition, loadedLevel.playerStartPointYPosition);

        List<Transform> loadedTransforms = new List<Transform>();
        foreach (LevelV2.LevelObjectV2 levelObject in loadedLevel.levelObjects ?? new List<LevelV2.LevelObjectV2>())
        {
            Transform parentTransform = levelObjectsContainer.transform;
            if (levelObject.parentIndex != -1)
            {
                if (levelObject.parentIndex < 0 || levelObject.parentIndex >= loadedTransforms.Count)
                {
                    Debug.LogError("Version 2 level object '" + levelObject.type + "' has an invalid parent index.");
                    loadedTransforms.Add(null);
                    continue;
                }

                parentTransform = loadedTransforms[levelObject.parentIndex];
                if (parentTransform == null)
                {
                    Debug.LogError("Version 2 level object '" + levelObject.type + "' cannot load because its parent could not be created.");
                    loadedTransforms.Add(null);
                    continue;
                }
            }

            GameObject loadedObject = CreateLevelObject(levelObject.type, parentTransform);
            if (loadedObject == null)
            {
                loadedTransforms.Add(null);
                continue;
            }

            loadedObject.transform.localPosition = new Vector3(levelObject.xPosition, levelObject.yPosition, 0f);
            loadedObject.transform.localRotation = Quaternion.Euler(0f, 0f, levelObject.rotation);
            loadedObject.transform.localScale = new Vector3(levelObject.xScale, levelObject.yScale, 1f);
            loadedTransforms.Add(loadedObject.transform);
        }

        FinishLoadingLevel();
    }

    void BeginLoadingLevel(float playerStartXPosition, float playerStartYPosition)
    {
        DestroyAllExistingLevelObjects();
        playerStartPoint.transform.position = new Vector3(playerStartXPosition, playerStartYPosition, 0f);
    }

    void FinishLoadingLevel()
    {
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
