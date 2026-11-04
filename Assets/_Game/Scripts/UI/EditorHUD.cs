using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-150)]
public class EditorHUD : MonoBehaviour
{
    [SerializeField] LevelEditor levelEditor;
    [SerializeField] GameObject deleteSelectedObjectsButton;
    [SerializeField] RectTransform objectSelectionBar;
    [SerializeField] Button savePrefabButton;
    [SerializeField] PrefabBar prefabBar;

    public PrefabBar PrefabBar => prefabBar;

    readonly List<RaycastResult> modifierHits = new List<RaycastResult>();
    readonly HashSet<EditorModifierButton> hoveredModifierButtons = new HashSet<EditorModifierButton>();
    EditorModifierButton[] modifierButtons;
    EventSystem modifierEventSystem;
    PointerEventData modifierPointer;
    bool waitForMouseAfterTouch;
    Vector2 lastMousePosition;

    void Awake()
    {
        modifierButtons = GetComponentsInChildren<EditorModifierButton>(true);
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
        UpdateScreenModifiers();
        RefreshSelectionButtons();
    }

    void OnDisable()
    {
        if (levelEditor != null) levelEditor.SetScreenModifiers(EditorModifier.None);
        hoveredModifierButtons.Clear();
        if (modifierButtons != null)
            foreach (EditorModifierButton button in modifierButtons)
                if (button != null) button.SetPointerOver(false);
        if (Input.touchCount > 0) waitForMouseAfterTouch = true;
        lastMousePosition = Input.mousePosition;
    }

    void UpdateScreenModifiers()
    {
        if (levelEditor == null) return;
        EditorModifier modifiers = EditorModifier.None;
        hoveredModifierButtons.Clear();
        if (Application.isFocused)
        {
            if (Input.touchCount > 0)
            {
                // Touch-to-mouse emulation can leave the mouse at the last touch position
                // after release. Wait for a real mouse movement or click before using it.
                waitForMouseAfterTouch = true;
                lastMousePosition = Input.mousePosition;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                        modifiers |= ModifiersAt(touch.position, touch.fingerId);
                }
            }
            else if (Input.mousePresent)
            {
                Vector2 mousePosition = Input.mousePosition;
                if (mousePosition != lastMousePosition || Input.GetMouseButtonDown(0) ||
                    Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                    waitForMouseAfterTouch = false;
                lastMousePosition = mousePosition;
                if (!waitForMouseAfterTouch)
                    modifiers |= ModifiersAt(mousePosition, -1);
            }
        }
        levelEditor.SetScreenModifiers(modifiers);
        foreach (EditorModifierButton button in modifierButtons)
            if (button != null) button.SetPointerOver(hoveredModifierButtons.Contains(button));
    }

    EditorModifier ModifiersAt(Vector2 position, int pointerId)
    {
        EventSystem current = EventSystem.current;
        if (current == null) return EditorModifier.None;
        if (modifierEventSystem != current || modifierPointer == null)
        {
            modifierEventSystem = current;
            modifierPointer = new PointerEventData(current);
        }
        modifierPointer.position = position;
        modifierPointer.pointerId = pointerId;
        modifierHits.Clear();
        current.RaycastAll(modifierPointer, modifierHits);
        foreach (RaycastResult hit in modifierHits)
        {
            // Respect UI drawn above the modifier instead of activating a hidden button.
            if (!(hit.module is GraphicRaycaster) || hit.gameObject == null) continue;
            EditorModifierButton button = hit.gameObject.GetComponentInParent<EditorModifierButton>();
            if (button == null) return EditorModifier.None;
            EditorModifier modifiers = button.Modifiers;
            if (modifiers != EditorModifier.None) hoveredModifierButtons.Add(button);
            return modifiers;
        }
        return EditorModifier.None;
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
}
