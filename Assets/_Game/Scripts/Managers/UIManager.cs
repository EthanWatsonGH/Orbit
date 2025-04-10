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

    [Header("Prefab References")]
    [SerializeField] GameObject levelPreviewPrefab;

    [Header("World Object References")]
    [SerializeField] GameObject levelSelectionMenu;
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

        levelSelectionMenu.SetActive(false);
        levelEditorHUD.SetActive(false);
        playerHUD.SetActive(false);
    }

    void ShowLevelPreviewPanel()
    {
        FindLastActiveUiBeforeOpeningMainMenu();
        HideAllUI();
        levelSelectionMenu.SetActive(true);
    }

    public void ShowPlayerLevelSelectionMenu()
    {
        ShowLevelPreviewPanel();
        StartCoroutine(LevelManager.Instance.LoadLevelPreviews("player"));
    }

    public void ShowGameLevelSelectionMenu()
    {
        ShowLevelPreviewPanel();
        StartCoroutine(LevelManager.Instance.LoadLevelPreviews("game"));
    }

    public void ShowPlayerHUD()
    {
        HideAllUI();
        playerHUD.gameObject.SetActive(true);
        EventManager.Instance.ShowPlayerInWorldUiElements();
    }

    void Update()
    {
        IsInControlBlockingMenu = levelSelectionMenu.activeSelf;
    }
}
