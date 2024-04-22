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

    [SerializeField] GameObject levelObjects;


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

        level.levelName = "hardcoded level name";
        level.levelAuthor = "hardcoded level author";
        level.loaderVersion = LOADER_VERSION;

        string json = JsonUtility.ToJson(level);

        // TODO: make it save the file name as something
        string saveLocation = Application.persistentDataPath + "/testLevel.json";

        Debug.Log("Save location: " + saveLocation);

        // TODO: make it save to game folder
        File.WriteAllText(saveLocation, json);

        Debug.Log("Saved level");
    }
}
