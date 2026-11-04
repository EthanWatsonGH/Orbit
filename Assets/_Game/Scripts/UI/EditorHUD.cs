using UnityEngine;
using UnityEngine.UI;

public class EditorHUD : MonoBehaviour
{
    [SerializeField] LevelEditor levelEditor;
    [SerializeField] GameObject deleteSelectedObjectsButton;
    [SerializeField] RectTransform objectSelectionBar;
    [SerializeField] Button savePrefabButton;
    [SerializeField] PrefabBar prefabBar;

    public PrefabBar PrefabBar => prefabBar;

    void Awake()
    {
        RefreshSelectionButtons();
    }

    void OnEnable()
    {
        if (levelEditor != null)
            levelEditor.SetObjectSelectionBar(objectSelectionBar);
        if (prefabBar != null)
            prefabBar.Bind(levelEditor);
        RefreshSelectionButtons();
    }

    void Update()
    {
        RefreshSelectionButtons();
    }

    void RefreshSelectionButtons()
    {
        if (deleteSelectedObjectsButton != null)
        {
            bool show = levelEditor != null && levelEditor.CanDeleteSelectedObjects;
            if (deleteSelectedObjectsButton.activeSelf != show)
                deleteSelectedObjectsButton.SetActive(show);
        }
        if (savePrefabButton != null)
            savePrefabButton.interactable = levelEditor != null && levelEditor.CanSaveSelectedPrefab;
    }

    public void Bind(LevelEditor targetLevelEditor)
    {
        levelEditor = targetLevelEditor;
        if (levelEditor != null && isActiveAndEnabled)
            levelEditor.SetObjectSelectionBar(objectSelectionBar);
        if (prefabBar != null && isActiveAndEnabled)
            prefabBar.Bind(levelEditor);
        RefreshSelectionButtons();
    }

    public void SnapSelectedObjectToLastHorizontal() => levelEditor?.SnapSelectedObjectToLastHorizontal();
    public void SnapSelectedObjectToLastVertical() => levelEditor?.SnapSelectedObjectToLastVertical();
    public void DeselectObject() => levelEditor?.DeselectObject();
    public void OpenPrefabLibrary() => UIManager.Instance?.ShowPrefabLibrary();
    public void OpenLevelManagement() => UIManager.Instance?.ShowLevelManagement();
    public void DeleteSelectedObjects() => levelEditor?.DeleteSelectedObjects();
    public void CreatePersistentGroup() => levelEditor?.CreatePersistentGroup();
    public void UngroupSelectedPersistentGroup() => levelEditor?.UngroupSelectedPersistentGroup();
    public void SaveSelectedPrefab()
    {
        string name = "Custom Prefab " + System.DateTime.Now.ToString("MMM dd yyyy h-mm-sstt");
        if (levelEditor != null && levelEditor.TrySaveSelectedPrefab(name, out string id))
        {
            if (prefabBar != null)
                prefabBar.MarkDirty();
            Debug.Log("Saved prefab: " + id);
        }
        else
            Debug.LogWarning("Prefab could not be saved from the current selection.");
    }
    public void DeleteAllLevelObjects() => levelEditor?.DeleteAllLevelObjects();
    public void SwitchToPlayMode() => levelEditor?.SwitchToPlayMode();
    public void SetModifierButtonHeld(EditorModifier modifier, bool isHeld) => levelEditor?.SetModifierButtonHeld(modifier, isHeld);
}
