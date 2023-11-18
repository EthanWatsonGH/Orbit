using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // singleton setup
    public static GameManager Instance { get; private set; }

    //variables
    public bool isPaused = false;
    public string gameMode = "play";

    public bool quickRetry = false;
    public bool quickLaunch = false;

    private void Awake()
    {
        // singleton setup
        Instance = this;
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void PauseTimeScale()
    {
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void ResumeTimeScale()
    {
        Time.timeScale = 1f;
        isPaused = false;
    }

    // TODO: make it so these cant desync from what the toggle shows
    public void ToggleQuickRetry()
    {
        quickRetry = !quickRetry;
    }

    public void ToggleQuickLaunch()
    {
        quickLaunch = !quickLaunch;
    }
}
