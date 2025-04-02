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
    [SerializeField] GameObject playerLevelSelectionMenu;
    //[SerializeField] GameObject gameLevelSelectionMenu;
    [SerializeField] GameObject levelEditorHUD;
    [SerializeField] GameObject playerHUD;

    GameObject lastActiveUiBeforeOpeningMenu;

    public bool IsInControlBlockingMenu = false;

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

    public void ShowLastActiveUiBeforeOpeningMainMenu()
    {
        HideAllUI();
        lastActiveUiBeforeOpeningMenu.SetActive(true);
    }

    public void HideAllUI()
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
        StartCoroutine(LevelManager.Instance.LoadLevelPreviews("player"));
    }

    public void ShowPlayerHUD()
    {
        HideAllUI();
        playerHUD.gameObject.SetActive(true);
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

        IsInControlBlockingMenu = playerLevelSelectionMenu.activeSelf;
    }
}
