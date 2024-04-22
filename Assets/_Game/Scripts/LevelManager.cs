using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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

    [SerializeField] GameObject levelObjectsContainer;


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
    }

    public void SaveLevel()
    {
        Level level = new Level();
        level.levelObjects = new List<Level.LevelObject>();

        level.levelName = "hardcoded level name 2";
        level.levelAuthor = "hardcoded level author 2";
        level.loaderVersion = LOADER_VERSION;

        bool endOfLevelObjects = false;
        int levelObjectIndex = 0;

        if (levelObjectsContainer.transform.childCount <= 0)
            endOfLevelObjects = true;

        while (!endOfLevelObjects)
        {
            Transform workingLevelObjectTransform = levelObjectsContainer.transform.GetChild(levelObjectIndex);

            Level.LevelObject newLevelObject = new Level.LevelObject();

            newLevelObject.type = workingLevelObjectTransform.name.Replace("(Clone)", "");
            newLevelObject.xPosition = workingLevelObjectTransform.position.x;
            newLevelObject.yPosition = workingLevelObjectTransform.position.y;
            newLevelObject.xScale = workingLevelObjectTransform.localScale.x;
            newLevelObject.yScale = workingLevelObjectTransform.localScale.y;
            newLevelObject.rotation = workingLevelObjectTransform.rotation.z;
            level.levelObjects.Add(newLevelObject);

            levelObjectIndex++;

            if (levelObjectIndex >= levelObjectsContainer.transform.childCount)
                endOfLevelObjects = true;
        }

        string json = JsonUtility.ToJson(level, true);

        // TODO: make it save the file name as the name the user inputs
        string saveLocation = Application.persistentDataPath + "/testLevel2.json";

        Debug.Log("Save location: " + saveLocation);

        // TODO: make it save to a more user friendly folder?
        File.WriteAllText(saveLocation, json);

        Debug.Log("Saved level");
    }
}
