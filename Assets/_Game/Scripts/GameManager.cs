using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // singleton setup
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
                    GameObject gameManagerObject = new GameObject("GameManager");
                    instance = gameManagerObject.AddComponent<GameManager>();
                }
            }
            return instance;
        }
    }

    //variables
    public bool isPaused = false;
    public string gameMode = "play";

    public bool quickRetry = false;
    public bool quickLaunch = false;

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
