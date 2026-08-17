using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class LevelManager : MonoBehaviour
{
    #region Singleton Setup
    private static LevelManager instance;
    public static LevelManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LevelManager>();
                if (instance == null )
                {
                    GameObject levelManager = new GameObject("LevelManager");
                    instance = levelManager.AddComponent<LevelManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        // singleton setup
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            //Destroy(gameObject);
        }
    }
    #endregion

    // TODO: should i track level save date in file?
    #region Level Struct
    // TODO: may need to make more of these for different loader versions, and check which one to use for each
    [System.Serializable]
    struct Level
    {
        public string contentId;
        public byte loaderVersion;
        public string levelName;
        //public string levelDescription;
        //public string levelAuthorType; // TODO: change to enum?
        public string levelAuthor;
        public float playerStartPointXPosition;
        public float playerStartPointYPosition;
        public List<LevelObject> levelObjects;

        [System.Serializable]
        public struct LevelObject
        {
            public string type;
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
    public GameObject ConstantPullerPrefab;
    public GameObject ConstantPusherPrefab;
    public GameObject FinishPrefab;
    public GameObject KillCirclePrefab;
    public GameObject KillWallPrefab;
    public GameObject PullerPrefab;
    public GameObject PusherPrefab;
    public GameObject SlipperyWallPrefab;

    // increment this if any changes are made to the level loading, with those new changes under a new case in the loading switch
    public const byte LOADER_VERSION = 1;
    public string playerLevelsDirectory { get; private set; }
    LevelStorage levelStorage;
    string levelLoadJson;
    string lastSavedLevelJson = string.Empty;

    [Header("World Object References")]
    [SerializeField] GameObject levelObjectsContainer;
    [SerializeField] GameObject playerStartPoint;
    [SerializeField] TMP_InputField levelSaveNameInput;
    [SerializeField] TMP_InputField levelLoadNameInput;
    [SerializeField] GameObject objectTransformControls;
    [SerializeField] TMP_InputField levelCodeToCopyInput;
    [SerializeField] TMP_InputField levelCodeInput;

    [Header("Level Preview References")]
    [SerializeField] GameObject levelPreviewPrefab;
    [SerializeField] GameObject levelsPreviewPanel;
    [SerializeField] TMP_Text noLevelsFoundText;

    void Start()
    {
        // get directory for player's levels
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
    Level GenerateLevelObject()
    {
        Level level = new Level();
        level.levelObjects = new List<Level.LevelObject>();

        // get level name input, or just create one if nothing was input
        string levelName = "Custom Level " + DateTime.Now.ToString("MMM dd yyyy h-mm-sstt");
        if (levelSaveNameInput != null && levelSaveNameInput.text != string.Empty)
            levelName = levelSaveNameInput.text.Trim();

        // TODO: make these values get properly set
        // set values to save
        level.contentId = Guid.NewGuid().ToString();
        level.levelName = levelName;
        level.levelAuthor = "TO ADD";
        level.loaderVersion = LOADER_VERSION;
        level.playerStartPointXPosition = playerStartPoint.transform.position.x;
        level.playerStartPointYPosition = playerStartPoint.transform.position.y;

        bool endOfLevelObjects = false;
        int levelObjectIndex = 0;

        if (levelObjectsContainer.transform.childCount <= 0)
            endOfLevelObjects = true;

        // save level objects
        while (!endOfLevelObjects)
        {
            Transform workingLevelObjectTransform = levelObjectsContainer.transform.GetChild(levelObjectIndex);

            Level.LevelObject newLevelObject = new Level.LevelObject();

            newLevelObject.type = workingLevelObjectTransform.name.Replace("(Clone)", "");
            newLevelObject.xPosition = workingLevelObjectTransform.position.x;
            newLevelObject.yPosition = workingLevelObjectTransform.position.y;
            newLevelObject.xScale = workingLevelObjectTransform.localScale.x;
            newLevelObject.yScale = workingLevelObjectTransform.localScale.y;
            newLevelObject.rotation = workingLevelObjectTransform.rotation.eulerAngles.z;
            level.levelObjects.Add(newLevelObject);

            levelObjectIndex++;

            if (levelObjectIndex >= levelObjectsContainer.transform.childCount)
                endOfLevelObjects = true;
        }

        return level;
    }

    #region Copy Level To Clipboard
    #if UNITY_WEBGL && !UNITY_EDITOR
            [System.Runtime.InteropServices.DllImport("__Internal")]
            private static extern void CopyToClipboard(string str);
    #endif

    public void CopyLevelCodeToClipboard()
    {
        Level level = GenerateLevelObject();

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
        // hide level editor UI while taking screenshot
        GameObject levelEditorCanvas = GameObject.Find("LevelEditor").transform.Find("Canvas").gameObject;
        bool wasLevelEditorCanvasActive = levelEditorCanvas.activeSelf;
        bool wereObjectTransformControlsActive = objectTransformControls.activeSelf;
        levelEditorCanvas.SetActive(false);
        objectTransformControls.SetActive(false);

        // wait until the end of the frame before taking the screenshot since the UI is actually hidden at the end of the frame
        yield return new WaitForEndOfFrame();

        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] previewImageBytes = screenshot.EncodeToPNG();
        Destroy(screenshot);

        // unhide level editor UI
        levelEditorCanvas.SetActive(wasLevelEditorCanvasActive);
        objectTransformControls.SetActive(wereObjectTransformControlsActive);

        onComplete?.Invoke(previewImageBytes);
    }

    public void SaveLevel()
    {
        StartCoroutine(SaveLevelWithPreview());
    }

    IEnumerator SaveLevelWithPreview()
    {
        Level level = GenerateLevelObject();
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
            createdAtUtcTicks = DateTime.UtcNow.Ticks,
            updatedAtUtcTicks = DateTime.UtcNow.Ticks,
            sortOrder = 0
        }, json, previewImageBytes);

        if (!didSave)
            yield break;

        lastSavedLevelJson = json;

        SetLevelCodeToCopyInputToLastSavedLevelJson();

        // TODO: display a message in game
        Debug.Log("Saved level.");
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
        switch (LOADER_VERSION)
        {
            case 1:
                try
                {
                    // TODO: handle invalid levelLoadJson and display a message in game
                    Level loadedLevel = JsonUtility.FromJson<Level>(levelLoadJson);

                    DestroyAllExistingLevelObjects();

                    // set the player start point position
                    playerStartPoint.transform.position = new Vector3(loadedLevel.playerStartPointXPosition, loadedLevel.playerStartPointYPosition);

                    // loop through level objects, instantiating a new object in game for each object in the file, with the transforms of each
                    foreach (Level.LevelObject levelObject in loadedLevel.levelObjects)
                    {
                        GameObject prefabToInstantiate = null;
                        switch(levelObject.type)
                        {
                            case "Booster":
                                prefabToInstantiate = BoosterPrefab;
                                break;
                            case "BouncyWall":
                                prefabToInstantiate = BouncyWallPrefab;
                                break;
                            case "ConstantBooster":
                                prefabToInstantiate = ConstantBoosterPrefab;
                                break;
                            case "ConstantPuller":
                                prefabToInstantiate = ConstantPullerPrefab;
                                break;
                            case "ConstantPusher":
                                prefabToInstantiate = ConstantPusherPrefab;
                                break;
                            case "Finish":
                                prefabToInstantiate = FinishPrefab;
                                break;
                            case "KillCircle":
                                prefabToInstantiate = KillCirclePrefab;
                                break;
                            case "KillWall":
                                prefabToInstantiate = KillWallPrefab;
                                break;
                            case "Puller":
                                prefabToInstantiate = PullerPrefab;
                                break;
                            case "Pusher":
                                prefabToInstantiate = PusherPrefab;
                                break;
                            case "SlipperyWall":
                                prefabToInstantiate = SlipperyWallPrefab;
                                break;
                            default:
                                Debug.Log("ERROR: An object could not be loaded because its type is not valid.");
                                break;
                        }

                        if (prefabToInstantiate != null)
                        {
                            Vector3 workingLevelObjectPostition = new Vector3(levelObject.xPosition, levelObject.yPosition, 0f);
                            Quaternion workingLevelObjectQuaternion = Quaternion.Euler(0f, 0f, levelObject.rotation);

                            GameObject lastPlacedObject = Instantiate(prefabToInstantiate, workingLevelObjectPostition, workingLevelObjectQuaternion, levelObjectsContainer.transform);

                            lastPlacedObject.transform.localScale = new Vector3(levelObject.xScale, levelObject.yScale);
                        }
                    }

                    EventManager.Instance.RecenterCamera();
                    EventManager.Instance.OnLevelLoad();

                    Debug.Log("Loaded level.");
                }
                catch
                {
                    if (string.IsNullOrEmpty(levelLoadJson))
                        Debug.Log("ERROR: The input string is empty.");
                    else 
                        Debug.Log("ERROR: The input string is not valid JSON and can't be loaded. Input: " + levelLoadJson);
                }
                break;
        }
    }

    void ResetLevelPreviews()
    {
        foreach (Transform levelPreview in levelsPreviewPanel.transform)
        {
            Destroy(levelPreview.gameObject);
        }

        noLevelsFoundText.text = string.Empty;
    }

    void ShowNoLevelsFound()
    {
        noLevelsFoundText.text = "No levels found";
        noLevelsFoundText.color = Color.white;
    }

    GameObject CreateLevelPreview(LevelCatalogRecord record, LevelSource source)
    {
        GameObject levelPreview = Instantiate(levelPreviewPrefab, levelsPreviewPanel.transform);
        Transform previewContent = levelPreview.transform.GetChild(0);

        previewContent.Find("LevelName").GetComponent<TMP_Text>().text = record.displayName;
        previewContent.Find("LevelAuthor").GetComponent<TMP_Text>().text = record.author;

        ButtonEventCaller buttonEventCaller = levelPreview.GetComponentInChildren<ButtonEventCaller>(true);
        if (buttonEventCaller == null)
            Debug.LogError("ERROR: Level preview prefab is missing ButtonEventCaller.");
        else
            buttonEventCaller.ConfigureLevelPreview(source, record.id);

        return levelPreview;
    }

    void ShowPreviewImageError(GameObject levelPreview, string message)
    {
        Transform imageTransform = levelPreview.transform.GetChild(0).Find("Image");
        imageTransform.GetChild(0).GetComponent<TMP_Text>().text = message;
        imageTransform.GetComponent<Image>().color = Color.grey;
    }

    void SetPreviewImage(GameObject levelPreview, Texture2D imageTexture)
    {
        Sprite imageSprite = Sprite.Create(imageTexture, new Rect(0, 0, imageTexture.width, imageTexture.height), new Vector2(0.5f, 0.5f));
        levelPreview.transform.GetChild(0).Find("Image").GetComponent<Image>().sprite = imageSprite;
    }

    public IEnumerator LoadLevelPreviews(LevelSource source)
    {
        ResetLevelPreviews();
        int previewCount = 0;

        List<LevelCatalogRecord> records = null;
        yield return levelStorage.LoadCatalog(source, loadedRecords => records = loadedRecords);
        foreach (LevelCatalogRecord record in records)
        {
            GameObject levelPreview = CreateLevelPreview(record, source);
            Texture2D previewTexture = null;
            yield return levelStorage.LoadPreview(source, record, loadedTexture => previewTexture = loadedTexture);
            if (previewTexture == null)
                ShowPreviewImageError(levelPreview, "Image not found");
            else
                SetPreviewImage(levelPreview, previewTexture);
            previewCount++;
        }

        if (previewCount == 0)
            ShowNoLevelsFound();
    }
    #endregion
}
