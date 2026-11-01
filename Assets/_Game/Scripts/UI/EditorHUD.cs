using UnityEngine;

public class EditorHUD : MonoBehaviour
{
    [SerializeField] LevelEditor levelEditor;
    [SerializeField] GameObject deleteSelectedObjectsButton;
    [SerializeField] RectTransform objectSelectionBar;

    void Awake()
    {
        RefreshDeleteSelectedObjectsButton();
    }

    void OnEnable()
    {
        if (levelEditor != null)
            levelEditor.SetObjectSelectionBar(objectSelectionBar);
        RefreshDeleteSelectedObjectsButton();
    }

    void Update()
    {
        RefreshDeleteSelectedObjectsButton();
    }

    void RefreshDeleteSelectedObjectsButton()
    {
        if (deleteSelectedObjectsButton == null)
            return;

        bool show = levelEditor != null && levelEditor.CanDeleteSelectedObjects;
        if (deleteSelectedObjectsButton.activeSelf != show)
            deleteSelectedObjectsButton.SetActive(show);
    }

    public void Bind(LevelEditor targetLevelEditor)
    {
        levelEditor = targetLevelEditor;
        if (levelEditor != null && isActiveAndEnabled)
            levelEditor.SetObjectSelectionBar(objectSelectionBar);
        RefreshDeleteSelectedObjectsButton();
    }

    public void CopyLevelCodeToClipboard() => levelEditor?.CopyLevelCodeToClipboard();
    public void SaveLevel() => levelEditor?.SaveLevel();
    public void SnapSelectedObjectToLastHorizontal() => levelEditor?.SnapSelectedObjectToLastHorizontal();
    public void SnapSelectedObjectToLastVertical() => levelEditor?.SnapSelectedObjectToLastVertical();
    public void DeselectObject() => levelEditor?.DeselectObject();
    public void DeleteSelectedObjects() => levelEditor?.DeleteSelectedObjects();
    public void CreatePersistentGroup() => levelEditor?.CreatePersistentGroup();
    public void UngroupSelectedPersistentGroup() => levelEditor?.UngroupSelectedPersistentGroup();
    public void LoadLevelFromClipboard() => levelEditor?.LoadLevelFromClipboard();
    public void DeleteAllLevelObjects() => levelEditor?.DeleteAllLevelObjects();
    public void SwitchToPlayMode() => levelEditor?.SwitchToPlayMode();
    public void PlaceBooster() => levelEditor?.PlaceBooster();
    public void PlaceBouncyWall() => levelEditor?.PlaceBouncyWall();
    public void PlaceConstantPuller() => levelEditor?.PlaceConstantPuller();
    public void PlaceConstantPusher() => levelEditor?.PlaceConstantPusher();
    public void PlaceFinish() => levelEditor?.PlaceFinish();
    public void PlaceKillCircle() => levelEditor?.PlaceKillCircle();
    public void PlaceKillWall() => levelEditor?.PlaceKillWall();
    public void PlaceMomentumRedirector() => levelEditor?.PlaceMomentumRedirector();
    public void PlacePuller() => levelEditor?.PlacePuller();
    public void PlacePusher() => levelEditor?.PlacePusher();
    public void PlaceSlipperyWall() => levelEditor?.PlaceSlipperyWall();
    public void SetModifierButtonHeld(EditorModifier modifier, bool isHeld) => levelEditor?.SetModifierButtonHeld(modifier, isHeld);
}
