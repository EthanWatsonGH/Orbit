using System;
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
    // TODO: make these capitalized to signify public
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
    
    // player preferences fields
    // TODO: make menus to change these. make them save to / load from file(s) to keep between restarts.
    public float UIScale = 1.5f;
    public float DefaultCameraZoom = 10f;
    public float MaxCameraZoom = 100f;
    public float MinCameraZoom = 2f;
    public float ScrollZoomIncrement = 10f;
    public float KeyboardPanSpeed = 20f;

    public bool touchPointIsOverButton = false;

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
        //Debug.Log("yes");
    }
    public void SetTouchPointIsOverButtonFalse()
    {
        touchPointIsOverButton = false;
        //Debug.Log("no");
    }
}
