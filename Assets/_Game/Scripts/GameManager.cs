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

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        HandlePauseTimeScale();
    }

    void HandlePauseTimeScale()
    {
        if (isPaused)
            Time.timeScale = 0.0f;
        else if (!isPaused)
            Time.timeScale = 1.0f;
    }

    // TODO: make it so these cant desync from what the toggle shows, if that becomes an issue
    public void ToggleQuickRetry()
    {
        quickRetry = !quickRetry;
    }

    public void ToggleQuickLaunch()
    {
        quickLaunch = !quickLaunch;
    }
}
