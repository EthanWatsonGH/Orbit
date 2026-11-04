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
            CameraViewManager.Instance?.DeactivateWorldCameras();
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

    [Header("World HUD")]
    [SerializeField] GameObject levelEditorHUD;
    [SerializeField] GameObject playerHUD;

    [Header("Modal Menus")]
    [SerializeField] LevelSelectionMenu gameLevelSelectionMenu;
    [SerializeField] LevelSelectionMenu playerLevelSelectionMenu;
    [SerializeField] LevelSelectionMenu downloadedLevelSelectionMenu;
    [SerializeField] PrefabLibraryMenu prefabLibraryPrefab;
    [SerializeField] LevelManagementMenu levelManagementPrefab;

    LevelSelectionMenu activeMenu;
    PrefabLibraryMenu prefabLibrary;
    bool prefabLibraryIsOpen;
    LevelManagementMenu levelManagement;
    bool levelManagementIsOpen;
    LevelEditor levelEditor;
    WorldMode menuReturnMode;
    int levelRevisionWhenMenuOpened = -1;

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

        if (currentMode == WorldMode.LevelEditor)
            GetLevelEditor()?.RememberStateBeforeLeavingEditor();

        EventManager.Instance.UnselectObject();
        EventManager.Instance.HidePlayerInWorldUiElements();
        HideWorldHud();

        SetWorldModeActive(currentMode, false);
        SetWorldModeActive(targetMode, true);
        ShowWorldModeUi(targetMode);

        if (targetMode == WorldMode.LevelEditor &&
            GetLevelEditor()?.RestoreStateAfterReturningToEditor() == true)
        {
            // The editor camera has just become active, so this recenters the correct
            // camera when a different level was loaded during play mode.
            EventManager.Instance.RecenterCamera();
        }
    }

    public void HideAllUI()
    {
        HideActiveMenuWithoutRestoringWorldMode();
        HideAllLevelSelectionMenus();
        HideWorldHud();
        EventManager.Instance.UnselectObject();
        EventManager.Instance.HidePlayerInWorldUiElements();
    }

    void ShowLevelSelectionMenu(LevelSource source)
    {
        if (prefabLibraryIsOpen || levelManagementIsOpen)
            return;
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
        levelRevisionWhenMenuOpened = LevelManager.Instance != null
            ? LevelManager.Instance.CurrentLevelRevision
            : -1;
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

    public void ShowPrefabLibrary()
    {
        if (activeMenu != null || prefabLibraryIsOpen || levelManagementIsOpen || GetActiveWorldMode() != WorldMode.LevelEditor)
            return;

        LevelEditor editor = GetLevelEditor();
        EditorHUD hud = levelEditorHUD != null ? levelEditorHUD.GetComponent<EditorHUD>() : null;
        if (editor == null || hud == null || hud.PrefabBar == null || prefabLibraryPrefab == null ||
            gameLevelSelectionMenu == null || gameLevelSelectionMenu.transform.parent == null)
        {
            Debug.LogError("UIManager is missing a prefab library, editor HUD, or modal menu reference.", this);
            return;
        }
        if (prefabLibrary == null)
            prefabLibrary = Instantiate(prefabLibraryPrefab, gameLevelSelectionMenu.transform.parent, false);
        if (CameraViewManager.Instance == null || !CameraViewManager.Instance.ActivateMenuCameraFromCurrentView())
            return;

        editor.RememberStateBeforeLeavingEditor(true);
        menuReturnMode = WorldMode.LevelEditor;
        prefabLibraryIsOpen = true;
        HideWorldHud();
        SetWorldModeActive(WorldMode.LevelEditor, false);
        prefabLibrary.Show(editor, hud.PrefabBar);
    }

    public void ShowLevelManagement()
    {
        if (activeMenu != null || prefabLibraryIsOpen || levelManagementIsOpen || GetActiveWorldMode() != WorldMode.LevelEditor)
            return;

        LevelEditor editor = GetLevelEditor();
        if (editor == null || levelManagementPrefab == null || gameLevelSelectionMenu == null || gameLevelSelectionMenu.transform.parent == null)
        {
            Debug.LogError("UIManager is missing the level management, editor, or modal menu reference.", this);
            return;
        }
        if (levelManagement == null)
            levelManagement = Instantiate(levelManagementPrefab, gameLevelSelectionMenu.transform.parent, false);
        if (CameraViewManager.Instance == null || !CameraViewManager.Instance.ActivateMenuCameraFromCurrentView())
            return;

        editor.RememberStateBeforeLeavingEditor(true);
        menuReturnMode = WorldMode.LevelEditor;
        levelManagementIsOpen = true;
        HideWorldHud();
        SetWorldModeActive(WorldMode.LevelEditor, false);
        levelManagement.Show();
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
        if (levelManagementIsOpen)
        {
            levelManagement.Hide();
            levelManagementIsOpen = false;
            CameraViewManager.Instance.DeactivateMenuCamera();
            SetWorldModeActive(WorldMode.LevelEditor, true);
            ShowWorldModeUi(WorldMode.LevelEditor);
            if (GetLevelEditor()?.RestoreStateAfterReturningToEditor() == true)
                EventManager.Instance.RecenterCamera();
            menuReturnMode = WorldMode.None;
            return;
        }
        if (prefabLibraryIsOpen)
        {
            prefabLibrary.Hide();
            prefabLibraryIsOpen = false;
            CameraViewManager.Instance.DeactivateMenuCamera();
            SetWorldModeActive(WorldMode.LevelEditor, true);
            ShowWorldModeUi(WorldMode.LevelEditor);
            GetLevelEditor()?.RestoreStateAfterReturningToEditor();
            menuReturnMode = WorldMode.None;
            return;
        }
        if (activeMenu == null)
            return;

        bool levelChangedWhileMenuWasOpen = LevelManager.Instance != null &&
                                            LevelManager.Instance.CurrentLevelRevision != levelRevisionWhenMenuOpened;
        activeMenu.Hide();
        activeMenu = null;
        CameraViewManager.Instance.DeactivateMenuCamera();
        SetWorldModeActive(menuReturnMode, true);
        ShowWorldModeUi(menuReturnMode);

        if (levelChangedWhileMenuWasOpen)
        {
            // Loading from a menu happens while the menu camera is active. Recenter
            // after the return world camera has been activated instead.
            EventManager.Instance.RecenterCamera();
        }

        menuReturnMode = WorldMode.None;
        levelRevisionWhenMenuOpened = -1;
    }

    void HideActiveMenuWithoutRestoringWorldMode()
    {
        if (levelManagementIsOpen && levelManagement != null)
            levelManagement.Hide();
        levelManagementIsOpen = false;
        if (prefabLibraryIsOpen && prefabLibrary != null)
            prefabLibrary.Hide();
        prefabLibraryIsOpen = false;
        if (activeMenu != null)
            activeMenu.Hide();

        activeMenu = null;
        menuReturnMode = WorldMode.None;
        levelRevisionWhenMenuOpened = -1;

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

    LevelEditor GetLevelEditor()
    {
        if (levelEditor == null && levelEditorModeRoot != null)
            levelEditor = levelEditorModeRoot.GetComponent<LevelEditor>();

        return levelEditor;
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
            if (CameraViewManager.Instance != null)
                CameraViewManager.Instance.ActivatePlayerCamera();
            if (playerHUD != null)
                playerHUD.SetActive(true);
            EventManager.Instance.ShowPlayerInWorldUiElements();
        }
        else if (mode == WorldMode.LevelEditor)
        {
            if (CameraViewManager.Instance != null)
                CameraViewManager.Instance.ActivateLevelEditorCamera();
            if (levelEditorHUD != null)
                levelEditorHUD.SetActive(true);
        }
    }
}
