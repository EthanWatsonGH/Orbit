using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    #region Singleton Setup
    private static UIManager instance;

    public static UIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<UIManager>();

                if (instance == null)
                {
                    GameObject newUIManager = new GameObject("UIManager");
                    instance = newUIManager.AddComponent<UIManager>();
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
            //Destroy(gameObject);
        }
    }
    #endregion

    [Header("World Object References")]
    [SerializeField] GameObject levelEditorHUD;
    [SerializeField] GameObject playerHUD;

    GameObject lastActiveUiBeforeOpeningMenu;

    public bool IsInControlBlockingMenu = false;

    void FindLastActiveUiBeforeOpeningMainMenu()
    {
        if (levelEditorHUD.activeSelf)
            lastActiveUiBeforeOpeningMenu = levelEditorHUD;
        if (playerHUD.activeSelf)
            lastActiveUiBeforeOpeningMenu = playerHUD;
    }

    public void ShowLastActiveUiBeforeOpeningMainMenu()
    {
        HideAllUI();
        lastActiveUiBeforeOpeningMenu.SetActive(true);
    }

    private IEnumerator HideInWorldUI()
    {
        EventManager.Instance.UnselectObject();
        yield return null;
        EventManager.Instance.HidePlayerInWorldUiElements();
    }

    public void HideAllUI()
    {
        StartCoroutine(HideInWorldUI());

        LevelManager.Instance.HideAllLevelSelectionMenus();
        levelEditorHUD.SetActive(false);
        playerHUD.SetActive(false);
    }

    void ShowLevelPreviewPanel(LevelSource source)
    {
        FindLastActiveUiBeforeOpeningMainMenu();
        HideAllUI();
        LevelManager.Instance.ShowLevelSelectionMenu(source);
    }

    public void ShowPlayerLevelSelectionMenu()
    {
        ShowLevelPreviewPanel(LevelSource.PlayerLevels);
    }

    public void ShowGameLevelSelectionMenu()
    {
        ShowLevelPreviewPanel(LevelSource.Game);
    }

    public void ShowDownloadedLevelSelectionMenu()
    {
        ShowLevelPreviewPanel(LevelSource.DownloadedLevels);
    }

    public void ShowPlayerHUD()
    {
        HideAllUI();
        playerHUD.gameObject.SetActive(true);
        EventManager.Instance.ShowPlayerInWorldUiElements();
    }

    void Update()
    {
        IsInControlBlockingMenu = LevelManager.Instance.IsAnyLevelSelectionMenuOpen;
    }
}
