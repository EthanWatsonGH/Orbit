using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Prefab References")]
    [SerializeField] GameObject levelPreviewPrefab;

    [Header("World Object References")]
    [SerializeField] GameObject playerLevelSelectionMenu;
    //[SerializeField] GameObject gameLevelSelectionMenu;
    [SerializeField] GameObject levelEditorHUD;
    [SerializeField] GameObject playerHUD;

    GameObject lastActiveUiBeforeOpeningMenu;

    void Start()
    {
        
    }

    void FindLastActiveUiBeforeOpeningMainMenu()
    {
        if (levelEditorHUD.activeSelf)
            lastActiveUiBeforeOpeningMenu = levelEditorHUD;
        if (playerHUD.activeSelf)
            lastActiveUiBeforeOpeningMenu = playerHUD;
    }

    void ShowLastActiveUiBeforeOpeningMainMenu()
    {
        HideAllUI();
        lastActiveUiBeforeOpeningMenu.SetActive(true);
    }

    void HideAllUI()
    {
        playerLevelSelectionMenu.SetActive(false);
        //gameLevelSelectionMenu.SetActive(false);
        levelEditorHUD.SetActive(false);
        playerHUD.SetActive(false);
    }

    void ShowPlayerLevelSelectionMenu()
    {
        HideAllUI();
        playerLevelSelectionMenu.SetActive(true);
        // LoadLevelPreviews();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && (levelEditorHUD.activeSelf || playerHUD.activeSelf))
        {
            FindLastActiveUiBeforeOpeningMainMenu();
            // TODO: make this just the root main menu when i have that
            ShowPlayerLevelSelectionMenu();
        }
        else if (Input.GetKeyDown(KeyCode.Escape) && !(levelEditorHUD.activeSelf || playerHUD.activeSelf))
        {
            ShowLastActiveUiBeforeOpeningMainMenu();
        }
    }
}
