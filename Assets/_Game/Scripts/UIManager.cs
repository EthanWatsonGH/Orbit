using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject pauseMenu;

    bool isPaused = false;

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
        if (Input.GetKeyDown(KeyCode.Escape))
            isPaused = !isPaused;

        pauseMenu.SetActive(isPaused);
    }
}
