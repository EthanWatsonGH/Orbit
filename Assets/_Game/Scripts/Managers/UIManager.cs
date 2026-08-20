using UnityEngine;

[DefaultExecutionOrder(-100)]
public class UIManager : MonoBehaviour
{
    #region Singleton Setup
    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Duplicate UIManager in the scene.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    #endregion

    void Start()
    {
        // HUD prefabs stay disabled by default so they do not clutter the editor.
        // Apply the active world mode once the scene has finished enabling its objects.
        WorldMode initialMode = GetActiveWorldMode();
        if (initialMode == WorldMode.None)
        {
            HideWorldHud();
            return;
        }

        ShowWorldModeUi(initialMode);
    }

    enum WorldMode
    {
        None,
        Player,
        LevelEditor
    }

    [Header("World Mode Roots")]
    [SerializeField] GameObject playerModeRoot;
    [SerializeField] GameObject levelEditorModeRoot;

    [Header("Persistent UI")]
    [SerializeField] GameObject uiRoot;

    [Header("World HUD")]
    [SerializeField] GameObject levelEditorHUD;
    [SerializeField] GameObject playerHUD;

    [Header("Modal Menus")]
    [SerializeField] LevelSelectionMenu gameLevelSelectionMenu;
    [SerializeField] LevelSelectionMenu playerLevelSelectionMenu;
    [SerializeField] LevelSelectionMenu downloadedLevelSelectionMenu;

    LevelSelectionMenu activeMenu;
    WorldMode menuReturnMode;
    bool isUiHiddenForPreviewCapture;
    bool uiRootWasActive;

    public void ShowLastActiveUiBeforeOpeningMainMenu()
    {
        CloseActiveMenu();
    }

    public void SwitchToPlayerMode()
    {
        SwitchWorldMode(WorldMode.Player);
    }

    public void SwitchToLevelEditorMode()
    {
        SwitchWorldMode(WorldMode.LevelEditor);
    }

    void SwitchWorldMode(WorldMode targetMode)
    {
        HideActiveMenuWithoutRestoringWorldMode();

        WorldMode currentMode = GetActiveWorldMode();
        if (currentMode == targetMode)
        {
            ShowWorldModeUi(targetMode);
            return;
        }

        EventManager.Instance.UnselectObject();
        EventManager.Instance.HidePlayerInWorldUiElements();
        HideWorldHud();

        SetWorldModeActive(currentMode, false);
        SetWorldModeActive(targetMode, true);
        ShowWorldModeUi(targetMode);
    }

    public void HideAllUI()
    {
        HideAllLevelSelectionMenus();
        HideWorldHud();
        EventManager.Instance.UnselectObject();
        EventManager.Instance.HidePlayerInWorldUiElements();
    }

    public void HideUiForPreviewCapture()
    {
        if (isUiHiddenForPreviewCapture)
            return;

        isUiHiddenForPreviewCapture = true;
        uiRootWasActive = uiRoot != null && uiRoot.activeSelf;

        // All screen UI, including both HUDs and modal menus, now lives below UIRoot.
        if (uiRoot != null)
            uiRoot.SetActive(false);
    }

    public void RestoreUiAfterPreviewCapture()
    {
        if (!isUiHiddenForPreviewCapture)
            return;

        if (uiRoot != null)
            uiRoot.SetActive(uiRootWasActive);

        isUiHiddenForPreviewCapture = false;
    }

    void ShowLevelSelectionMenu(LevelSource source)
    {
        LevelSelectionMenu menu = GetLevelSelectionMenu(source);
        if (menu == null)
        {
            Debug.LogError("UIManager is missing the " + source + " level selection menu reference.", this);
            return;
        }

        if (activeMenu != null)
        {
            if (activeMenu != menu)
            {
                activeMenu.Hide();
                activeMenu = menu;
                activeMenu.Show();
            }

            return;
        }

        WorldMode currentMode = GetActiveWorldMode();
        if (currentMode == WorldMode.None)
        {
            Debug.LogError("UIManager cannot open a menu because neither world mode is active.", this);
            return;
        }

        if (CameraViewManager.Instance == null || !CameraViewManager.Instance.ActivateMenuCameraFromCurrentView())
            return;

        menuReturnMode = currentMode;
        EventManager.Instance.UnselectObject();
        EventManager.Instance.HidePlayerInWorldUiElements();
        HideWorldHud();
        SetWorldModeActive(currentMode, false);

        activeMenu = menu;
        activeMenu.Show();
    }

    public void ShowPlayerLevelSelectionMenu()
    {
        ShowLevelSelectionMenu(LevelSource.PlayerLevels);
    }

    public void ShowGameLevelSelectionMenu()
    {
        ShowLevelSelectionMenu(LevelSource.Game);
    }

    public void ShowDownloadedLevelSelectionMenu()
    {
        ShowLevelSelectionMenu(LevelSource.DownloadedLevels);
    }

    public void ShowPlayerHUD()
    {
        ShowWorldModeUi(WorldMode.Player);
    }

    public void MarkLevelSourceDirty(LevelSource source)
    {
        LevelSelectionMenu menu = GetLevelSelectionMenu(source);
        if (menu != null)
            menu.MarkDirty();
    }

    void CloseActiveMenu()
    {
        if (activeMenu == null)
            return;

        activeMenu.Hide();
        activeMenu = null;
        CameraViewManager.Instance.DeactivateMenuCamera();
        SetWorldModeActive(menuReturnMode, true);
        ShowWorldModeUi(menuReturnMode);
        menuReturnMode = WorldMode.None;
    }

    void HideActiveMenuWithoutRestoringWorldMode()
    {
        if (activeMenu != null)
            activeMenu.Hide();

        activeMenu = null;
        menuReturnMode = WorldMode.None;

        if (CameraViewManager.Instance != null)
            CameraViewManager.Instance.DeactivateMenuCamera();
    }

    void HideAllLevelSelectionMenus()
    {
        if (gameLevelSelectionMenu != null)
            gameLevelSelectionMenu.Hide();
        if (playerLevelSelectionMenu != null)
            playerLevelSelectionMenu.Hide();
        if (downloadedLevelSelectionMenu != null)
            downloadedLevelSelectionMenu.Hide();
    }

    LevelSelectionMenu GetLevelSelectionMenu(LevelSource source)
    {
        switch (source)
        {
            case LevelSource.Game:
                return gameLevelSelectionMenu;
            case LevelSource.PlayerLevels:
                return playerLevelSelectionMenu;
            case LevelSource.DownloadedLevels:
                return downloadedLevelSelectionMenu;
            default:
                return null;
        }
    }

    WorldMode GetActiveWorldMode()
    {
        if (playerModeRoot != null && playerModeRoot.activeInHierarchy)
            return WorldMode.Player;
        if (levelEditorModeRoot != null && levelEditorModeRoot.activeInHierarchy)
            return WorldMode.LevelEditor;

        return WorldMode.None;
    }

    void SetWorldModeActive(WorldMode mode, bool isActive)
    {
        switch (mode)
        {
            case WorldMode.Player:
                if (playerModeRoot != null)
                    playerModeRoot.SetActive(isActive);
                break;
            case WorldMode.LevelEditor:
                if (levelEditorModeRoot != null)
                    levelEditorModeRoot.SetActive(isActive);
                break;
        }
    }

    void HideWorldHud()
    {
        if (levelEditorHUD != null)
            levelEditorHUD.SetActive(false);
        if (playerHUD != null)
            playerHUD.SetActive(false);
    }

    void ShowWorldModeUi(WorldMode mode)
    {
        HideWorldHud();

        if (mode == WorldMode.Player)
        {
            if (playerHUD != null)
                playerHUD.SetActive(true);
            EventManager.Instance.ShowPlayerInWorldUiElements();
        }
        else if (mode == WorldMode.LevelEditor && levelEditorHUD != null)
        {
            levelEditorHUD.SetActive(true);
        }
    }
}
