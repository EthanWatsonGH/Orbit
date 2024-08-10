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

    [Header("Player Preferences Fields")]
    // TODO: make menus to change these. make them save to / load from file(s) to keep between restarts.
    public float UIScale = 1.5f;
    public float ObjectTransformControlsOffsetMultiplier = 1f;
    public float DefaultCameraZoom = 10f;
    public float MaxCameraZoom = 100f;
    public float MinCameraZoom = 2f;
    public float ScrollZoomIncrement = 10f;
    public float KeyboardPanSpeed = 20f;
    public int FramerateLimit = 60;

    // TODO: move this stuff to be handled in UI manager
    public bool TouchPointIsOverButton = false;

    void Start()
    {
        Application.targetFrameRate = FramerateLimit;
    }

    void Update()
    {
        // TODO: this is just for testing. move this to only be updated on an event when the player changes the setting
        Application.targetFrameRate = FramerateLimit;
    }

    // buttons will use these to say if the touch point is over any button
    public void SetTouchPointIsOverButtonTrue()
    {
        TouchPointIsOverButton = true;
        //Debug.Log("yes");
    }
    public void SetTouchPointIsOverButtonFalse()
    {
        TouchPointIsOverButton = false;
        //Debug.Log("no");
    }
}
