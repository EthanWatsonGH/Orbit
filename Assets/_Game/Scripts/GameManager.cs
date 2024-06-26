using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region Singleton Setup
    private static GameManager instance;

    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();

                if (instance == null)
                {
                    GameObject newGameManager = new GameObject("GameManager");
                    instance = newGameManager.AddComponent<GameManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    // level editor placeable objects references
    [Header("Level Editor Placable Objects")]
    [SerializeField] public GameObject bouncyWallPrefab;
    [SerializeField] public GameObject boosterPrefab;
    [SerializeField] public GameObject constantPullerPrefab;
    [SerializeField] public GameObject constantPusherPrefab;
    [SerializeField] public GameObject finishPrefab;
    [SerializeField] public GameObject killCirclePrefab;
    [SerializeField] public GameObject killWallPrefab;
    [SerializeField] public GameObject pullerPrefab;
    [SerializeField] public GameObject pusherPrefab;
    [SerializeField] public GameObject slipperyWallPrefab;

    public bool touchPointIsOverButton = false ;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    // TODO: may not need this
    // buttons will use these to say if the touch point is over any button
    public void SetTouchPointIsOverButtonTrue()
    {
        touchPointIsOverButton = true;
        Debug.Log("yes");
    }
    public void SetTouchPointIsOverButtonFalse()
    {
        touchPointIsOverButton = false;
        Debug.Log("no");

    }
}
