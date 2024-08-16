using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

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
            Destroy(gameObject);
        }
    }
    #endregion

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

    string levelDirectory;

    [Header("World Object References")]
    [SerializeField] GameObject levelObjectsContainer;
    [SerializeField] GameObject playerStartPoint;
    [SerializeField] TMP_InputField levelSaveNameInput;
    [SerializeField] TMP_InputField levelLoadNameInput;

    void Start()
    {
        // get directory for player's levels
        levelDirectory = Application.persistentDataPath + "/playerLevels";
        EnsureLevelDirectoryExists();
    }

    void EnsureLevelDirectoryExists()
    {
        // create player levels directory if it doesn't already exist 
        if (!Directory.Exists(levelDirectory))
        {
            Directory.CreateDirectory(levelDirectory);
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

    public void SaveLevel()
    {
        Level level = new Level();
        level.levelObjects = new List<Level.LevelObject>();

        // TODO: make these values get properly set
        // set values to save
        level.levelName = "hardcoded level name 2";
        level.levelAuthor = "hardcoded level author 2";
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

        string json = JsonUtility.ToJson(level, true);

        EnsureLevelDirectoryExists();

        // get level name input, or just create one if nothing was input
        string levelName = "Custom Level " + DateTime.Now.ToString("yyyyMMdd HHmmss");
        if (levelSaveNameInput != null && levelSaveNameInput.text != string.Empty)
            levelName = levelSaveNameInput.text.Trim();

        string saveLocation = Path.Combine(levelDirectory, levelName + ".json");

        File.WriteAllText(saveLocation, json);

        // TODO: display a message in game
        Debug.Log("Saved level.");
    }

    public void LoadLevel()
    {
        switch (LOADER_VERSION)
        {
            case 1:
                // TODO: make a level selection menu instead of it being through a text input
                if (levelLoadNameInput.text.Trim() == string.Empty)
                {
                    Debug.Log("ERROR: Enter a level to load.");
                    break;
                }

                string levelFileName = levelLoadNameInput.text.Trim() + ".json";

                string loadLocation = Path.Combine(levelDirectory, levelFileName);

                // if level file is not found, send a message and cancel loading
                if (!File.Exists(loadLocation))
                {
                    // TODO: display a message in game
                    Debug.Log("ERROR: Level not found.");
                    break;
                }

                string json = File.ReadAllText(loadLocation);

                Level loadedLevel = JsonUtility.FromJson<Level>(json);

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
                            prefabToInstantiate = LevelManager.Instance.BoosterPrefab;
                            break;
                        case "BouncyWall":
                            prefabToInstantiate = LevelManager.Instance.BouncyWallPrefab;
                            break;
                        case "ConstantPuller":
                            prefabToInstantiate = LevelManager.Instance.ConstantPullerPrefab;
                            break;
                        case "ConstantPusher":
                            prefabToInstantiate = LevelManager.Instance.ConstantPusherPrefab;
                            break;
                        case "Finish":
                            prefabToInstantiate = LevelManager.Instance.FinishPrefab;
                            break;
                        case "KillCircle":
                            prefabToInstantiate = LevelManager.Instance.KillCirclePrefab;
                            break;
                        case "KillWall":
                            prefabToInstantiate = LevelManager.Instance.KillWallPrefab;
                            break;
                        case "Puller":
                            prefabToInstantiate = LevelManager.Instance.PullerPrefab;
                            break;
                        case "Pusher":
                            prefabToInstantiate = LevelManager.Instance.PusherPrefab;
                            break;
                        case "SlipperyWall":
                            prefabToInstantiate = LevelManager.Instance.SlipperyWallPrefab;
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

                // TODO: display a message in game
                Debug.Log("Loaded level.");
                break;
        }
    }
}
