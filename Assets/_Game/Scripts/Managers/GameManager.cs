using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    #region Singleton Setup
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Duplicate GameManager in the scene.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    #endregion

    [Header("Player Preferences Fields")]
    // TODO: make menus to change these. make them save to / load from file(s) to keep between restarts.
    public float UIScale = 2.25f;
    public float ObjectTransformControlsOffsetMultiplier = 1.25f;
    public float DefaultCameraZoom = 10f;
    public float MaxCameraZoom = 1000f;
    public float MinCameraZoom = 1f;
    public float ScrollZoomIncrement = 10f;
    public float KeyboardPanSpeed = 10f;
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
