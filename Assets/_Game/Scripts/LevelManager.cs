using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.InputSystem.iOS;
using UnityEngine.UI;

public class LevelManager : MonoBehaviour
{
    // singleton setup
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

    // increment this if any changes are made to the level loading, with those new changes under a new case in the loading switch
    const byte LOADER_VERSION = 1;

    string levelDirectory;

    [SerializeField] GameObject levelObjectsContainer;
    [SerializeField] GameObject playerStartPoint;

    // TODO: unify this in some way so i don't have to repeat it in multiple scripts
    // level editor placeable objects references
    [Header("Level Editor Placable Objects")]
    [SerializeField] GameObject boosterPrefab;
    [SerializeField] GameObject bouncyWallPrefab;
    [SerializeField] GameObject constantPullerPrefab;
    [SerializeField] GameObject constantPusherPrefab;
    [SerializeField] GameObject finishPrefab;
    [SerializeField] GameObject killCirclePrefab;
    [SerializeField] GameObject killWallPrefab;
    [SerializeField] GameObject pullerPrefab;
    [SerializeField] GameObject pusherPrefab;
    [SerializeField] GameObject slipperyWallPrefab;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    private void Awake()
    {
        // singleton setup
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // file path for player's levels
        levelDirectory = Application.persistentDataPath + "/playerLevels";

        EnsureLevelDirectoryExists();
    }

    void EnsureLevelDirectoryExists()
    {
        // create player levels folder on starup if it doesn't already exist 
        if (!Directory.Exists(levelDirectory))
        {
            Directory.CreateDirectory(levelDirectory);
        }
    }

    public void DestroyAllExistingLevelObjects()
    {
        // reset player start point position
        playerStartPoint.transform.position = Vector3.zero;

        foreach (Transform levelObject in levelObjectsContainer.transform)
        {
            Destroy(levelObject.gameObject);
        }
    }

    public void SaveLevel()
    {
        Level level = new Level();
        level.levelObjects = new List<Level.LevelObject>();

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
        
        // TODO: make it save the file name as the name the player inputs
        string saveLocation = Path.Combine(levelDirectory, "levelName" + ".json");

        Debug.Log("Save location: " + saveLocation);

        File.WriteAllText(saveLocation, json);

        Debug.Log("Saved level");
    }

    public void LoadLevel(string levelFileName)
    {
        // should probably have the loader verion be selected at the root of where i call the load level function in case i ever change the parameters, but whatever
        switch (LOADER_VERSION)
        {
            case 1:
                levelFileName = levelFileName + ".json";

                // TODO: add ability to load from different folders
                string loadLocation = Path.Combine(levelDirectory, levelFileName);

                Debug.Log("Load location: " + loadLocation);

                // if level file is not found, send a message and exit
                if (!File.Exists(loadLocation))
                {
                    // TODO: display a message in game
                    Debug.Log("ERROR: file not found");
                    break;
                }

                string json = File.ReadAllText(loadLocation);

                Level loadedLevel = JsonUtility.FromJson<Level>(json);

                Debug.Log(loadedLevel.levelAuthor);

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
                            prefabToInstantiate = boosterPrefab;
                            break;
                        case "BouncyWall":
                            prefabToInstantiate = bouncyWallPrefab;
                            break;
                        case "ConstantPuller":
                            prefabToInstantiate = constantPullerPrefab;
                            break;
                        case "ConstantPusher":
                            prefabToInstantiate = constantPusherPrefab;
                            break;
                        case "Finish":
                            prefabToInstantiate = finishPrefab;
                            break;
                        case "KillCircle":
                            prefabToInstantiate = killCirclePrefab;
                            break;
                        case "KillWall":
                            prefabToInstantiate = killWallPrefab;
                            break;
                        case "Puller":
                            prefabToInstantiate = pullerPrefab;
                            break;
                        case "Pusher":
                            prefabToInstantiate = pusherPrefab;
                            break;
                        case "SlipperyWall":
                            prefabToInstantiate = slipperyWallPrefab;
                            break;
                        default:
                            // TODO: display in game
                            Debug.Log("ERROR: type not valid");
                            break;
                    }

                    if (prefabToInstantiate != null)
                    {
                        Vector3 workingLevelObjectPostition = new Vector3(levelObject.xPosition, levelObject.yPosition);
                        Quaternion workingLevelObjectQuaternion = Quaternion.Euler(0f, 0f, levelObject.rotation);

                        GameObject lastPlacedObject = Instantiate(prefabToInstantiate, workingLevelObjectPostition, workingLevelObjectQuaternion, levelObjectsContainer.transform);

                        lastPlacedObject.transform.localScale = new Vector3(levelObject.xScale, levelObject.yScale);
                    }
                }

                Debug.Log("Loaded level");
                break;
        }
    }
}
