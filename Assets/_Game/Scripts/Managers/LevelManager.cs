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
        public string levelName;
        public string levelAuthor;
        public byte loaderVersion;
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
    string gameLevelsDirectory;
    string levelLoadJson;

    [Header("World Object References")]
    [SerializeField] GameObject levelObjectsContainer;
    [SerializeField] GameObject playerStartPoint;
    [SerializeField] TMP_InputField levelSaveNameInput;
    [SerializeField] TMP_InputField levelLoadNameInput;
    [SerializeField] GameObject objectTransformControls;

    [Header("Level Preview References")]
    [SerializeField] GameObject levelPreviewPrefab;
    [SerializeField] GameObject levelsPreviewPanel;
    [SerializeField] TMP_Text noLevelsFoundText;

    void Start()
    {
        // get directory for player's levels
        playerLevelsDirectory = Application.persistentDataPath + "/playerLevels";
        EnsureDirectoryExists(playerLevelsDirectory);

        // get directory for game's levels
        gameLevelsDirectory = Application.streamingAssetsPath + "/gameLevels";
        EnsureDirectoryExists(gameLevelsDirectory);
    }

    void EnsureDirectoryExists(string directory)
    {
        // create directory if it doesn't already exist 
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
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

    #region Saving
    Level GenerateLevelObject()
    {
        Level level = new Level();
        level.levelObjects = new List<Level.LevelObject>();

        // get level name input, or just create one if nothing was input
        string levelName = "Custom Level " + DateTime.Now.ToString("yyyyMMdd HHmmss");
        if (levelSaveNameInput != null && levelSaveNameInput.text != string.Empty)
            levelName = levelSaveNameInput.text.Trim();

        // TODO: make these values get properly set
        // set values to save
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

    IEnumerator SaveScreenshot(string screenshotLocation)
    {
        // TODO: will probably have to change this later when i move the save level button to a different menu. at that point just handle all UI stuff in a manager with events.
        // hide level editor UI while taking screenshot
        GameObject levelEditorCanvas = GameObject.Find("LevelEditor").transform.Find("Canvas").gameObject;
        levelEditorCanvas.SetActive(false);
        objectTransformControls.SetActive(false);

        // wait until the end of the frame before taking the screenshot since the UI is actually hidden at the end of the frame
        yield return new WaitForEndOfFrame();

        // save screenshot for level preview image
        ScreenCapture.CaptureScreenshot(screenshotLocation + ".png");

        // unhide level editor UI
        levelEditorCanvas.SetActive(true);
        objectTransformControls.SetActive(true);
    }

    public void SaveLevel()
    {
        Level level = GenerateLevelObject();

        string json = JsonUtility.ToJson(level, true);

        EnsureDirectoryExists(playerLevelsDirectory);

        string saveLocation = Path.Combine(playerLevelsDirectory, level.levelName);

        File.WriteAllText(saveLocation + ".json", json);

        StartCoroutine(SaveScreenshot(saveLocation));

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

    public bool GetLevelJsonFromFile(string levelPath)
    {
        // if level file is not found, send a message and cancel loading
        if (!File.Exists(levelPath))
        {
            // TODO: display a message in game
            Debug.Log("ERROR: Level not found.");
            return false;
        }

        levelLoadJson = File.ReadAllText(levelPath);

        return true;
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
                                // TODO: display in game
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

                    // TODO: display a message in game
                    Debug.Log("Loaded level.");
                }
                catch
                {
                    // TODO: display a message in game
                    Debug.Log("ERROR: The input string is not valid JSON and can't be loaded. Input: " + levelLoadJson);
                }
                break;
        }
    }

    public IEnumerator LoadLevelPreviews(string levelsDirectory)
    {
        string directory = string.Empty;

        if (levelsDirectory == "player")
        {
            directory = playerLevelsDirectory;
        }
        else if (levelsDirectory == "game")
        {
            directory = gameLevelsDirectory;
        }

        if (!Directory.Exists(directory))
        {
            // TODO: show message in game
            Debug.Log("ERROR: Folder could not be found when trying to load level previews at folder: " + directory);
            yield break;
        }

        // reset previews panel for next load
        foreach (Transform levelPreview in levelsPreviewPanel.transform)
        {
            Destroy(levelPreview.gameObject);
        }
        noLevelsFoundText.text = string.Empty;
        

        // TODO: also account for other file types like .dat for when i do the built in levels with binary serialization
        string[] levelFiles = Directory.GetFiles(directory, "*.json");

        if (levelFiles.Length <= 0) // if no levels found, show message where levels would have been displayed
        {
            noLevelsFoundText.text = "No levels found";
            noLevelsFoundText.color = Color.white;
        }
        else // display levels
        {
            if (levelsDirectory == "player")
            {
                levelFiles = levelFiles.OrderByDescending(file => new FileInfo(file).CreationTime).ToArray(); // order levels by creation time descending
            }
            else if (levelsDirectory == "game")
            {
                levelFiles = levelFiles.OrderBy(file => Path.GetFileName(file)).ToArray(); // order levels by alphabetical name
            }
            

            string[] levelImages = Directory.GetFiles(directory, "*.png");

            foreach (string level in levelFiles)
            {
                try
                {
                    string json = File.ReadAllText(level);

                    Level deserializedLevel = JsonUtility.FromJson<Level>(json);

                    if (!deserializedLevel.Equals(null))
                    {
                        GameObject levelPreview = Instantiate(levelPreviewPrefab, levelsPreviewPanel.transform);

                        string imageFile = Array.Find(levelImages, image => image.Contains(deserializedLevel.levelName)); // since im using the plain level name, the error described below can happen

                        if (string.IsNullOrEmpty(imageFile)) // if image wasn't found, show message where it would have been displayed
                        {
                            levelPreview.transform.GetChild(0).transform.Find("Image").transform.GetChild(0).GetComponent<TMP_Text>().text = "Image not found";
                            levelPreview.transform.GetChild(0).transform.Find("Image").transform.GetComponent<Image>().color = Color.grey;
                            // TODO: show message in game
                            Debug.Log("ERROR: Could not find image. Level file name, image file name, and level name in level file must all match. And image must be in same folder as level.");
                        }
                        else // show display
                        {
                            try
                            {
                                byte[] imageBytes = File.ReadAllBytes(imageFile);

                                Texture2D imageTexture = new Texture2D(2, 2);
                                imageTexture.LoadImage(imageBytes);

                                Sprite imageSprite = Sprite.Create(imageTexture, new Rect(0, 0, imageTexture.width, imageTexture.height), new Vector2(0.5f, 0.5f));

                                levelPreview.transform.GetChild(0).transform.Find("Image").transform.GetComponent<Image>().sprite = imageSprite;
                            }
                            catch
                            {
                                levelPreview.transform.GetChild(0).transform.Find("Image").transform.GetChild(0).GetComponent<TMP_Text>().text = "Image could not be loaded";
                                levelPreview.transform.GetChild(0).transform.Find("Image").transform.GetComponent<Image>().color = Color.grey;
                            }
                        }

                        levelPreview.transform.GetChild(0).transform.Find("LevelName").transform.GetComponent<TMP_Text>().text = deserializedLevel.levelName;
                        levelPreview.transform.GetChild(0).transform.Find("LevelAuthor").transform.GetComponent<TMP_Text>().text = deserializedLevel.levelAuthor;
                        levelPreview.transform.GetChild(0).transform.Find("LevelPath").transform.GetComponent<TMP_Text>().text = level;
                    }
                    else
                    {
                        // TODO: show message in game
                        Debug.Log("ERROR: A level preview failed to load");
                    }
                }
                catch (System.Exception ex)
                {
                    // TODO: show message in game
                    Debug.Log("ERROR: A level preview failed to load or deserialize. Level: " + level + " . Exception message:" + ex.Message);
                }

                yield return null;
            }
        }
    }
    #endregion
}
