using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject pauseMenu;

    

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        HandleTogglePauseMenu();
    }

    void HandleTogglePauseMenu()
    {
        // toggle paused variable on pressing pause button
        if (Input.GetKeyDown(KeyCode.Escape))
            GameManager.Instance.isPaused = !GameManager.Instance.isPaused;

        // only show pause menu if game is paused
        pauseMenu.SetActive(GameManager.Instance.isPaused);
    }
}
