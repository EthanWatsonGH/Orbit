using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] GameObject pauseMenu;
    [SerializeField] GameObject playerHUD;
    [SerializeField] GameObject levelEditorUI;

    

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

    public void HideAllPanels()
    {
        // hide player hud
        // hide level editor ui
        // hide pause menu
    }

    public void ShowPauseMenu()
    {

    }

    public void HidePauseMenu()
    {
        pauseMenu.SetActive(false);
        switch (GameManager.Instance.gameMode)
        {
            case "play":
                playerHUD.SetActive(true);
                break;
        }


    }
}
