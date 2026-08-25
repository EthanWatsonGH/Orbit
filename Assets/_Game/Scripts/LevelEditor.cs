using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelEditor : MonoBehaviour
{

    public static bool IsRotationInvariantCircularObject(GameObject levelObject)
    {
        if (levelObject == null)
            return false;

        string objectName = levelObject.name;
        return objectName.Contains("KillCircle") ||
               objectName.Contains("Puller") ||
               objectName.Contains("Pusher");
    }

    // These level objects are circular in both appearance and behavior. Their own
    // rotation carries no information, so keep their serialized transform canonical.
    public static void NormalizeRotationInvariantCircularObjectRotations(Transform root)
    {
        if (root == null)
            return;

        if (IsRotationInvariantCircularObject(root.gameObject))
            root.localRotation = Quaternion.identity;

        foreach (Transform child in root)
            NormalizeRotationInvariantCircularObjectRotations(child);
    }

    // self object references
    [SerializeField] Rigidbody2D rb;
    [SerializeField] GameObject prefabToPlace;
    [SerializeField] GameObject startLocationIcon;
    [SerializeField] GameObject deselectObjectButton;
    [SerializeField] GameObject snapVerticalButton;
    [SerializeField] GameObject snapHorizontalButton;
    [SerializeField] GameObject createPersistentGroupButton;
    [SerializeField] LineRenderer verticalLine;
    [SerializeField] LineRenderer horizontalLine;
    [SerializeField] LineRenderer rotationLine;
    [SerializeField] TMP_Dropdown scaleIncrementDropdown;
    [SerializeField] TMP_Dropdown rotateIncrementDropdown;
    [SerializeField] TMP_Dropdown moveIncrementDropdown;

    // world object references
    [Header("World Objects")]
    [SerializeField] GameObject levelObjectsCollection;
    [SerializeField] GameObject playerStartPoint;

    [Header("Screen Space UI")]
    [SerializeField] ToggleButton worldTransformToggle;
    [SerializeField] SelectionControlsUI selectionControlsUI;
    [SerializeField] ScaleFromEdgeControlsUI scaleFromEdgeControlsUI;
    [SerializeField] RectTransform boxSelectionVisual;

    bool isTryingToPlace = false;
    GameObject objectCurrentlyTryingToPlace = null;
    bool pointerIsOverObjectSelectionBar = false;
    GameObject selectedObject = null;
    GameObject lastSelectedObject = null;
    // A deselection is not a new selection yet. Keep it briefly so reselecting the
    // same object preserves the previous snap target, while selecting another object
    // promotes this object to the last selected object.
    GameObject deselectedObjectAwaitingReplacement = null;
    bool IsWorldTransform => worldTransformToggle == null || worldTransformToggle.IsOn;
    bool IsSelectedPersistentGroup => IsPersistentGroup(selectedObject);
    bool HasMultipleSelection => selectedSelectionRoots.Count > 1;
    bool IsSelectedGroup => HasMultipleSelection || IsSelectedPersistentGroup;

    // object selection
    const float MINIMUM_DRAG_DISTANCE_PIXELS = 25f;
    bool hasSelectionDragExceededThreshold = false;
    // The editor owns the interpretation of a world pointer gesture. PointerInput
    // only reports pointer facts, so UI interactions cannot leak into selection
    // through a shared global "consumed" flag.
    enum EditorPointerInteraction
    {
        None,
        WorldSelection,
        BoxSelection,
        PlaceObject,
        Transform
    }

    EditorPointerInteraction pointerInteraction;
    Vector3 boxSelectionStartWorldPosition;
    Vector3 boxSelectionCurrentWorldPosition;
    bool hasBoxSelectionWorldPositions;
    readonly List<Transform> selectedSelectionRoots = new List<Transform>();
    GameObject selectionPivot;
    readonly List<GameObject> suspendedSelectionObjects = new List<GameObject>();
    readonly List<GameObject> selectionObjectsBeforeLeavingEditor = new List<GameObject>();
    bool isSelectionSuspendedForSave;
    int levelRevisionWhenEditorWasLeft = -1;
    bool hasRememberedEditorState;
    Canvas boxSelectionCanvas;
    RectTransform boxSelectionVisualParent;

    struct SelectionTransformState
    {
        public Transform transform;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public Vector3 localScale;
    }

    public struct ScaleFromEdgeFrame
    {
        public Vector3 pivot;
        public Vector3 right;
        public Vector3 up;
        public float minX;
        public float maxX;
        public float minY;
        public float maxY;

        public Vector3 Center => GetPoint((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        public float Width => maxX - minX;
        public float Height => maxY - minY;

        public Vector3 GetHandlePosition(ScaleFromEdgeHandle handle)
        {
            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;

            return handle switch
            {
                ScaleFromEdgeHandle.Up => GetPoint(centerX, maxY),
                ScaleFromEdgeHandle.Down => GetPoint(centerX, minY),
                ScaleFromEdgeHandle.Left => GetPoint(minX, centerY),
                ScaleFromEdgeHandle.Right => GetPoint(maxX, centerY),
                ScaleFromEdgeHandle.UpLeft => GetPoint(minX, maxY),
                ScaleFromEdgeHandle.UpRight => GetPoint(maxX, maxY),
                ScaleFromEdgeHandle.DownLeft => GetPoint(minX, minY),
                ScaleFromEdgeHandle.DownRight => GetPoint(maxX, minY),
                _ => pivot
            };
        }

        Vector3 GetPoint(float x, float y)
        {
            return pivot + right * x + up * y;
        }
    }

    struct SelectionControlAvailability
    {
        public bool canDuplicate;
        public bool canScaleBoth;
        public bool canScaleHorizontally;
        public bool canScaleVertically;
        public bool canRotate;

        public static SelectionControlAvailability ForObject(GameObject levelObject)
        {
            bool isPlayerStartPoint = levelObject.name == "PlayerStartPoint";
            bool supportsIndependentScaleAndRotation = !isPlayerStartPoint && !IsRotationInvariantCircularObject(levelObject);

            return new SelectionControlAvailability
            {
                canDuplicate = !isPlayerStartPoint,
                canScaleBoth = !isPlayerStartPoint,
                canScaleHorizontally = supportsIndependentScaleAndRotation,
                canScaleVertically = supportsIndependentScaleAndRotation,
                canRotate = supportsIndependentScaleAndRotation
            };
        }

        public void IntersectWith(SelectionControlAvailability other)
        {
            canDuplicate &= other.canDuplicate;
            canScaleBoth &= other.canScaleBoth;
            canScaleHorizontally &= other.canScaleHorizontally;
            canScaleVertically &= other.canScaleVertically;
            canRotate &= other.canRotate;
        }
    }

    struct ScaleFromEdgeGesture
    {
        public ScaleFromEdgeHandle handle;
        public bool scalesFromBothSides;
        public bool scalesIndependently;
        public ScaleFromEdgeFrame frameAtStart;
        public Vector3 selectedLocalScaleAtStart;
        public Vector3 selectedWorldPositionAtStart;
        public Vector3 pointerWorldPositionAtStart;
        public float minimumXFactor;
        public float maximumXFactor;
        public float minimumYFactor;
        public float maximumYFactor;
    }

    // object movement
    enum ShiftMoveConstraint
    {
        None,
        AlongRight,
        AlongUp
    }

    // Exactly one transform may own a pointer at a time. The individual move,
    // rotate, and scale gesture data remains separate, but this is the single
    // source of truth for whether the editor is currently transforming.
    enum ActiveTransform
    {
        None,
        Move,
        Rotate,
        ScaleFromEdge
    }

    const float MOVE_GUIDE_LINE_HALF_LENGTH = 9999f;
    Vector3 moveOffset;
    Vector3 selectedObjectPositionAtStartMove;
    Vector3 pointerPositionAtStartMove;
    readonly List<SelectionTransformState> activeSelectionTransformStates = new List<SelectionTransformState>();
    float moveIncrement = 0f;
    Vector3 moveIncrementOffset = new Vector3(0f, 0f, 0f);
    bool wasShiftModeHeldDuringMove;
    Vector3 shiftMoveGuideOrigin;
    Vector3 shiftMoveGuideRight;
    Vector3 shiftMoveGuideUp;
    ShiftMoveConstraint activeShiftMoveConstraint;

    // object rotation
    float selectedObjectRotationAtStartRotate;
    float angleToPointerAtStartRotate;
    Quaternion selectionPivotRotationAtStartRotate;
    Vector3 selectionPivotPositionAtStartRotate;
    float rotateIncrement = 0f;
    float rotationIncrementOffset = 0f;

    // object scaling
    float maximumScale = 999999f;
    float scaleIncrement = 0f;
    ScaleFromEdgeGesture activeScaleFromEdgeGesture;
    bool isShiftModeButtonHeld;
    bool isCtrlModeButtonHeld;

    ActiveTransform activeTransform;
    ObjectTransformControl activeMoveControl;
    int activeTransformPointerId = int.MinValue;
    Vector2 activeTransformPressScreenPosition;

    void Awake()
    {
        if (PointerInput.Instance == null)
        {
            Debug.LogError("LevelEditor requires one PointerInput component in the scene.", this);
            enabled = false;
            return;
        }

        if (worldTransformToggle == null)
            Debug.LogError("LevelEditor is missing its World Transform Toggle reference. World transform mode will be used until it is assigned.", this);

        if (selectionControlsUI == null)
        {
            SelectionControlsUI[] availableControls = FindObjectsByType<SelectionControlsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (availableControls.Length == 1)
                selectionControlsUI = availableControls[0];
            else if (availableControls.Length > 1)
                Debug.LogError("LevelEditor found more than one SelectionControlsUI. Assign the intended controls in the inspector.", this);
        }

        if (selectionControlsUI == null)
        {
            Debug.LogError("LevelEditor requires one SelectionControlsUI in the scene.", this);
            enabled = false;
            return;
        }

        selectionControlsUI.Initialize(this);

        if (scaleFromEdgeControlsUI == null)
        {
            ScaleFromEdgeControlsUI[] availableControls = FindObjectsByType<ScaleFromEdgeControlsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (availableControls.Length == 1)
                scaleFromEdgeControlsUI = availableControls[0];
            else if (availableControls.Length > 1)
                Debug.LogError("LevelEditor found more than one ScaleFromEdgeControlsUI. Assign the intended controls in the inspector.", this);
        }

        if (scaleFromEdgeControlsUI != null)
            scaleFromEdgeControlsUI.Initialize(this);

        // Scene-authored levels do not necessarily pass through LevelManager's load
        // path, so canonicalize their existing circular roots here as well.
        if (levelObjectsCollection != null)
            NormalizeRotationInvariantCircularObjectRotations(levelObjectsCollection.transform);

        if (boxSelectionVisual != null)
        {
            boxSelectionCanvas = boxSelectionVisual.GetComponentInParent<Canvas>();
            boxSelectionVisualParent = boxSelectionVisual.parent as RectTransform;

            if (boxSelectionCanvas == null || boxSelectionVisualParent == null)
                Debug.LogError("LevelEditor requires Box Selection Visual to be inside a UI Canvas with a RectTransform parent.", boxSelectionVisual);

            // The visual appears beneath the pointer while dragging, so it must never
            // cause PointerInput to treat the release as a UI interaction.
            foreach (Graphic graphic in boxSelectionVisual.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            SetBoxSelectionVisualVisible(false);
        }

        // ensure toggleable elements are at proper default show/hide
        deselectObjectButton.SetActive(false);
        snapVerticalButton.SetActive(false);
        snapHorizontalButton.SetActive(false);
        if (createPersistentGroupButton != null)
            createPersistentGroupButton.SetActive(false);
    }

    void Start()
    {
        // ensure level editor object and all of its visuals are disabled before starting game
        gameObject.SetActive(false);

        // setup increment dropdown listeners
        moveIncrementDropdown.onValueChanged.AddListener(OnMoveIncrementDropdownChanged);
        rotateIncrementDropdown.onValueChanged.AddListener(OnRotateIncrementDropdownChanged);
        scaleIncrementDropdown.onValueChanged.AddListener(OnScaleIncrementDropdownChanged);
    }

    void Update()
    {
        UpdateBoxSelectIntentFromPointerDrag();
        HandlePlacePrefab();
        HandleSelectObject();
        RefreshSelectionControls();
    }

    void LateUpdate()
    {
        UpdateActiveScreenSpaceTransformControl();
        RefreshBoxSelectionVisualFromWorld();
    }

    private void OnEnable()
    {
        EventManager.Instance.UnselectObjectEvent.AddListener(UnselectObject);
    }

    private void OnDisable()
    {
        CancelActiveTransform();
        pointerInteraction = EditorPointerInteraction.None;
        hasBoxSelectionWorldPositions = false;
        isShiftModeButtonHeld = false;
        isCtrlModeButtonHeld = false;
        SetBoxSelectionVisualVisible(false);
        SetMoveGuideLinesVisible(false);
        UnselectObject();
        EventManager.Instance.UnselectObjectEvent.RemoveListener(UnselectObject);
    }

    
    #region Increment Dropdown Listeners

    string SanitizeSelectedValue(string selected)
    {
        selected = selected.Replace("°", "");
        if (selected.Length > 0 && (char.IsDigit(selected[0]) || selected[0] == '.'))
            selected = selected.Split(' ')[0];
        else
            selected = "0";
        return selected;
    }

    void OnMoveIncrementDropdownChanged(int index)
    {
        string selected = SanitizeSelectedValue(moveIncrementDropdown.options[index].text);
        moveIncrement = float.Parse(selected);
    }

    void OnRotateIncrementDropdownChanged(int index)
    {
        string selected = SanitizeSelectedValue(rotateIncrementDropdown.options[index].text);
        rotateIncrement = float.Parse(selected);
    }

    void OnScaleIncrementDropdownChanged(int index)
    {
        string selected = SanitizeSelectedValue(scaleIncrementDropdown.options[index].text);
        scaleIncrement = float.Parse(selected);
    }

    #endregion

    #region Snap Selected Object To Last Selected Functions

    public void SnapSelectedObjectToLastHorizontal()
    {
        if (selectedObject != null && lastSelectedObject != null && selectedObject != lastSelectedObject)
        {
            Vector3 oldPosition = selectedObject.transform.position;
            Vector3 newPosition = new Vector3(lastSelectedObject.transform.position.x, oldPosition.y, 0f);
            selectedObject.transform.position = newPosition;
            MoveMultipleSelectionRootsBy(newPosition - oldPosition);
        }
    }

    public void SnapSelectedObjectToLastVertical()
    {
        if (selectedObject != null && lastSelectedObject != null && selectedObject != lastSelectedObject)
        {
            Vector3 oldPosition = selectedObject.transform.position;
            Vector3 newPosition = new Vector3(oldPosition.x, lastSelectedObject.transform.position.y, 0f);
            selectedObject.transform.position = newPosition;
            MoveMultipleSelectionRootsBy(newPosition - oldPosition);
        }
    }

    #endregion

    void UpdateBoxSelectIntentFromPointerDrag()
    {
        PointerInput pointerInput = PointerInput.Instance;
        if (pointerInput.WasPressedThisFrame)
        {
            hasSelectionDragExceededThreshold = false;
            hasBoxSelectionWorldPositions = false;
            SetBoxSelectionVisualVisible(false);

            if (pointerInteraction == EditorPointerInteraction.None &&
                activeTransform == ActiveTransform.None &&
                !isTryingToPlace &&
                !UiHitTest.IsScreenPositionOverUi(pointerInput.PressStartScreenPosition))
            {
                pointerInteraction = EditorPointerInteraction.WorldSelection;
                // Keep this first corner fixed in the level. The second finger
                // can then move the camera without dragging the box through space.
                hasBoxSelectionWorldPositions =
                    pointerInput.TryGetWorldPositionNoDepth(
                        pointerInput.PressStartScreenPosition,
                        out boxSelectionStartWorldPosition);
                boxSelectionCurrentWorldPosition = boxSelectionStartWorldPosition;
            }
        }

        if ((pointerInteraction != EditorPointerInteraction.WorldSelection &&
             pointerInteraction != EditorPointerInteraction.BoxSelection) ||
            activeTransform != ActiveTransform.None ||
            !pointerInput.IsHeld ||
            pointerInput.WasCanceledThisFrame)
        {
            SetBoxSelectionVisualVisible(false);
            return;
        }

        // A second touch before the drag becomes a box is a camera gesture, not
        // an editor selection. Once a box has started, keep it alive so that
        // same second touch can pan or zoom while selecting.
        if (pointerInteraction == EditorPointerInteraction.WorldSelection &&
            pointerInput.HadMultiplePointersDuringCurrentGesture)
        {
            pointerInteraction = EditorPointerInteraction.None;
            hasBoxSelectionWorldPositions = false;
            SetBoxSelectionVisualVisible(false);
            return;
        }

        if (pointerInteraction == EditorPointerInteraction.WorldSelection)
        {
            float dragDistanceInPixels = pointerInput.DragDistancePixels;
            if (dragDistanceInPixels >= MINIMUM_DRAG_DISTANCE_PIXELS)
            {
                hasSelectionDragExceededThreshold = true;
                pointerInteraction = EditorPointerInteraction.BoxSelection;
            }
        }

        if (hasSelectionDragExceededThreshold)
            RefreshBoxSelectionVisualFromWorld();
    }

    void HandlePlacePrefab()
    {
        if (objectCurrentlyTryingToPlace != null &&
            isTryingToPlace &&
            pointerInteraction == EditorPointerInteraction.PlaceObject)
        {
            // make the object the player is currently trying to place follow the pointer
            if (PointerInput.Instance.TryGetCurrentWorldPosition(out Vector3 pointerWorldPosition))
                objectCurrentlyTryingToPlace.transform.position = pointerWorldPosition;

            // dont show object that is currently trying to be placed when over object selection bar
            if (pointerIsOverObjectSelectionBar)
                objectCurrentlyTryingToPlace.SetActive(false);
            else
                objectCurrentlyTryingToPlace.SetActive(true);

            // stop following pointer and finish placing object
            if (PointerInput.Instance.WasReleasedThisFrame && isTryingToPlace)
            {
                isTryingToPlace = false;

                if (pointerIsOverObjectSelectionBar) // if player tries to place object over object selection bar, delete the object to cancel placement
                {
                    Destroy(objectCurrentlyTryingToPlace);
                }
                else // place object
                {
                    SelectObject(objectCurrentlyTryingToPlace);
                    ConfigureSelectionControlsForSelectedObject();
                }

                objectCurrentlyTryingToPlace = null;
            }
        }
    }

    void HandleSelectObject()
    {
        // set the object the player clicks as selected if it's allowed to be selected
        PointerInput pointerInput = PointerInput.Instance;
        if (pointerInput.WasReleasedThisFrame)
        {
            EditorPointerInteraction interactionAtRelease = pointerInteraction;
            hasSelectionDragExceededThreshold = false;
            SetBoxSelectionVisualVisible(false);

            // A transform may be driven by a second touch after the primary
            // gameplay pointer releases. Keep its ownership until that control
            // itself has finished, rather than letting a later touch select.
            if (interactionAtRelease == EditorPointerInteraction.Transform &&
                activeTransform != ActiveTransform.None)
            {
                hasBoxSelectionWorldPositions = false;
                return;
            }

            pointerInteraction = EditorPointerInteraction.None;

            if (pointerInput.WasCanceledThisFrame ||
                interactionAtRelease == EditorPointerInteraction.None ||
                interactionAtRelease == EditorPointerInteraction.PlaceObject ||
                interactionAtRelease == EditorPointerInteraction.Transform)
            {
                hasBoxSelectionWorldPositions = false;
                return;
            }

            bool shouldRemoveFromSelection = IsRemoveSelectionModifierHeld();
            bool shouldAddToSelection = !shouldRemoveFromSelection && IsAddSelectionModifierHeld();
            if (interactionAtRelease == EditorPointerInteraction.BoxSelection)
            {
                // The first finger owns this box. A second finger may pan or pinch
                // the camera without changing the box-selection interaction.
                SelectObjectsInsideBox(shouldAddToSelection, shouldRemoveFromSelection);
            }
            else if (interactionAtRelease == EditorPointerInteraction.WorldSelection &&
                     !pointerInput.HadMultiplePointersDuringCurrentGesture)
            {
                Ray ray = Camera.main.ScreenPointToRay(pointerInput.ScreenPosition);
                RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

                if (hit.collider != null) // object hit
                {
                    if (shouldRemoveFromSelection)
                    {
                        RemoveObjectsFromSelection(new[] { hit.collider.gameObject });
                    }
                    else if (shouldAddToSelection)
                    {
                        AddObjectsToSelection(new[] { hit.collider.gameObject });
                    }
                    else
                    {
                        SelectObject(hit.collider.gameObject);
                        ConfigureSelectionControlsForSelectedObject();
                    }
                }
                else if (!shouldAddToSelection && !shouldRemoveFromSelection) // no object hit
                {
                    // TODO: circle-collision fallback selection flow should be initiated here.
                    UnselectObject();
                }
            }

            hasBoxSelectionWorldPositions = false;
        }
    }

    void SelectObject(GameObject objectToSelect, bool rememberPreviousSelection = true)
    {
        if (objectToSelect == null)
        {
            UnselectObject();
            return;
        }

        if (HasMultipleSelection)
            UnselectObject();

        ClearSelectedSelectionRoots();
        DestroySelectionPivot();
        selectedSelectionRoots.Add(objectToSelect.transform);

        SetSelectedObject(objectToSelect, rememberPreviousSelection);

        if (selectionControlsUI != null)
            selectionControlsUI.SetSelectedTransform(selectedObject.transform);
    }

    public void SelectObjects(IEnumerable<GameObject> objectsToSelect)
    {
        List<Transform> selectionRoots = GetSelectionRoots(objectsToSelect);
        RemovePlayerStartPointFromMultiSelection(selectionRoots);
        UnselectObject();

        if (selectionRoots.Count == 0)
            return;

        if (selectionRoots.Count == 1)
        {
            SelectObject(selectionRoots[0].gameObject);
            ConfigureSelectionControlsForSelectedObject();
            return;
        }

        SelectMultipleObjects(selectionRoots);
        ConfigureSelectionControlsForSelectedObject();
    }

    void AddObjectsToSelection(IEnumerable<GameObject> objectsToAdd)
    {
        List<Transform> rootsToAdd = GetSelectionRoots(objectsToAdd);
        if (rootsToAdd.Count == 0)
            return;

        bool playerStartPointIsSelected = playerStartPoint != null && selectedObject == playerStartPoint;
        bool isTryingToAddPlayerStartPoint = playerStartPoint != null && rootsToAdd.Contains(playerStartPoint.transform);
        bool isTryingToAddAnotherObject = rootsToAdd.Exists(selectionRoot =>
            playerStartPoint == null || selectionRoot != playerStartPoint.transform);

        if (playerStartPointIsSelected && isTryingToAddAnotherObject)
        {
            LogPlayerStartPointCanOnlyBeSelectedAlone();
            return;
        }

        if (!playerStartPointIsSelected && selectedObject != null && isTryingToAddPlayerStartPoint)
        {
            rootsToAdd.Remove(playerStartPoint.transform);
            LogPlayerStartPointCanOnlyBeSelectedAlone();
            if (rootsToAdd.Count == 0)
                return;
        }

        List<GameObject> combinedSelection = new List<GameObject>();
        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot != null)
                combinedSelection.Add(selectionRoot.gameObject);
        }

        foreach (Transform selectionRoot in rootsToAdd)
            combinedSelection.Add(selectionRoot.gameObject);

        SelectObjects(combinedSelection);
    }

    void RemoveObjectsFromSelection(IEnumerable<GameObject> objectsToRemove)
    {
        if (selectedObject == null)
            return;

        List<Transform> rootsToRemove = GetSelectionRoots(objectsToRemove);
        if (rootsToRemove.Count == 0)
            return;

        List<GameObject> remainingSelection = new List<GameObject>();
        bool removedAnObject = false;

        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot == null)
                continue;

            if (IsSelectionRootIncludedIn(rootsToRemove, selectionRoot))
                removedAnObject = true;
            else
                remainingSelection.Add(selectionRoot.gameObject);
        }

        if (removedAnObject)
            SelectObjects(remainingSelection);
    }

    bool IsSelectionRootIncludedIn(List<Transform> selectionRoots, Transform candidate)
    {
        foreach (Transform selectionRoot in selectionRoots)
        {
            if (candidate == selectionRoot ||
                candidate.IsChildOf(selectionRoot) ||
                selectionRoot.IsChildOf(candidate))
                return true;
        }

        return false;
    }

    List<Transform> GetSelectionRoots(IEnumerable<GameObject> objectsToSelect)
    {
        List<Transform> selectionRoots = new List<Transform>();
        if (objectsToSelect == null)
            return selectionRoots;

        foreach (GameObject objectToSelect in objectsToSelect)
        {
            if (objectToSelect == null)
                continue;

            Transform candidate = objectToSelect.transform;
            if (playerStartPoint != null && candidate.IsChildOf(playerStartPoint.transform))
                candidate = playerStartPoint.transform;

            if (!IsEditorSelectableObject(candidate))
                continue;

            bool isAlreadyCoveredBySelectionRoot = false;
            for (int index = selectionRoots.Count - 1; index >= 0; index--)
            {
                Transform selectionRoot = selectionRoots[index];
                if (candidate.IsChildOf(selectionRoot))
                {
                    isAlreadyCoveredBySelectionRoot = true;
                    break;
                }

                if (selectionRoot.IsChildOf(candidate))
                    selectionRoots.RemoveAt(index);
            }

            if (!isAlreadyCoveredBySelectionRoot && !selectionRoots.Contains(candidate))
                selectionRoots.Add(candidate);
        }

        return selectionRoots;
    }

    void RemovePlayerStartPointFromMultiSelection(List<Transform> selectionRoots)
    {
        if (playerStartPoint != null &&
            selectionRoots.Count > 1 &&
            selectionRoots.Remove(playerStartPoint.transform))
        {
            LogPlayerStartPointCanOnlyBeSelectedAlone();
        }
    }

    void LogPlayerStartPointCanOnlyBeSelectedAlone()
    {
        Debug.Log("Player start point can only be selected alone.");
    }

    bool IsEditorSelectableObject(Transform candidate)
    {
        if (candidate == null)
            return false;

        bool isPlayerStartPoint = playerStartPoint != null && candidate == playerStartPoint.transform;
        return isPlayerStartPoint ||
               (levelObjectsCollection != null && candidate.IsChildOf(levelObjectsCollection.transform));
    }

    void SelectMultipleObjects(List<Transform> selectionRoots)
    {
        EnsureSelectionPivot();
        selectedSelectionRoots.Clear();
        foreach (Transform selectionRoot in selectionRoots)
            selectedSelectionRoots.Add(selectionRoot);

        selectionPivot.transform.SetPositionAndRotation(GetSelectionPivot(selectionRoots), Quaternion.identity);
        selectionPivot.transform.localScale = Vector3.one;

        // The pivot is editor-only: it presents the shared controls and transform
        // origin without becoming part of the level hierarchy.
        SetSelectedObject(selectionPivot, false);
    }

    public void CreatePersistentGroup()
    {
        if (activeTransform != ActiveTransform.None || !CanCreatePersistentGroup())
            return;

        // Multi-selection never changes the level hierarchy. Grouping is the one
        // deliberate hierarchy operation: create a real Group at the selection pivot
        // and reparent the selected roots while preserving their world transforms.
        GameObject persistentGroup = new GameObject(LevelManager.GroupObjectType);
        persistentGroup.transform.SetParent(levelObjectsCollection.transform, false);
        persistentGroup.transform.SetPositionAndRotation(selectionPivot.transform.position, selectionPivot.transform.rotation);
        persistentGroup.transform.localScale = selectionPivot.transform.localScale;

        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot != null)
                selectionRoot.SetParent(persistentGroup.transform, true);
        }

        ClearSelectedSelectionRoots();
        DestroySelectionPivot();
        selectedSelectionRoots.Add(persistentGroup.transform);
        SetSelectedObject(persistentGroup, false);

        ConfigureSelectionControlsForSelectedObject();
    }

    bool CanCreatePersistentGroup()
    {
        return HasMultipleSelection;
    }

    void EnsureSelectionPivot()
    {
        if (selectionPivot != null)
            return;

        selectionPivot = new GameObject("EditorSelectionPivot");
        selectionPivot.hideFlags = HideFlags.HideInHierarchy;
        selectionPivot.transform.SetParent(transform, false);
    }

    void DestroySelectionPivot()
    {
        if (selectionPivot == null)
            return;

        Destroy(selectionPivot);
        selectionPivot = null;
    }

    void ClearSelectedSelectionRoots()
    {
        selectedSelectionRoots.Clear();
    }

    static bool IsPersistentGroup(GameObject levelObject)
    {
        return levelObject != null && levelObject.name == LevelManager.GroupObjectType;
    }

    Vector3 GetSelectionPivot(List<Transform> selectionRoots)
    {
        Vector3 averagePosition = Vector3.zero;

        foreach (Transform selectionRoot in selectionRoots)
            averagePosition += selectionRoot.position;

        if (TryGetWorldBounds(selectionRoots, out Bounds selectionBounds))
            return selectionBounds.center;

        return averagePosition / selectionRoots.Count;
    }

    bool TryGetWorldBounds(IEnumerable<Transform> transforms, out Bounds worldBounds)
    {
        bool hasBounds = false;
        worldBounds = default;

        foreach (Transform transformToMeasure in transforms)
        {
            if (transformToMeasure == null)
                continue;

            // Colliders represent the playable object bounds. Renderers are included
            // too so objects such as text can participate before they have colliders.
            foreach (Collider2D collider in transformToMeasure.GetComponentsInChildren<Collider2D>())
                EncapsulateWorldBounds(collider.bounds, ref hasBounds, ref worldBounds);

            foreach (Renderer renderer in transformToMeasure.GetComponentsInChildren<Renderer>())
                EncapsulateWorldBounds(renderer.bounds, ref hasBounds, ref worldBounds);
        }

        return hasBounds;
    }

    bool TryGetScaleFromEdgeFrame(out ScaleFromEdgeFrame frame)
    {
        frame = default;
        if (selectedObject == null)
            return false;

        Vector3 right = selectedObject.transform.right;
        Vector3 up = selectedObject.transform.up;
        right.z = 0f;
        up.z = 0f;
        if (right.sqrMagnitude <= Mathf.Epsilon || up.sqrMagnitude <= Mathf.Epsilon)
            return false;

        frame.pivot = selectedObject.transform.position;
        frame.right = right.normalized;
        frame.up = up.normalized;
        frame.minX = float.PositiveInfinity;
        frame.maxX = float.NegativeInfinity;
        frame.minY = float.PositiveInfinity;
        frame.maxY = float.NegativeInfinity;

        bool hasMeasuredPoint = false;
        foreach (Transform selectionRoot in GetScaleConstraintTransforms())
            AddScaleFromEdgeFramePoints(selectionRoot, ref frame, ref hasMeasuredPoint);

        return hasMeasuredPoint && frame.Width > Mathf.Epsilon && frame.Height > Mathf.Epsilon;
    }

    void AddScaleFromEdgeFramePoints(Transform selectionRoot, ref ScaleFromEdgeFrame frame, ref bool hasMeasuredPoint)
    {
        if (selectionRoot == null)
            return;

        bool hasMeasuredPointForRoot = false;

        foreach (Collider2D collider in selectionRoot.GetComponentsInChildren<Collider2D>())
        {
            if (collider is BoxCollider2D boxCollider)
            {
                Bounds localBounds = new Bounds(boxCollider.offset, boxCollider.size);
                AddLocalBoundsCornersToScaleFromEdgeFrame(boxCollider.transform, localBounds, ref frame, ref hasMeasuredPointForRoot);
            }
            else
            {
                AddWorldBoundsCornersToScaleFromEdgeFrame(collider.bounds, ref frame, ref hasMeasuredPointForRoot);
            }
        }

        foreach (Renderer renderer in selectionRoot.GetComponentsInChildren<Renderer>())
            AddLocalBoundsCornersToScaleFromEdgeFrame(renderer.transform, renderer.localBounds, ref frame, ref hasMeasuredPointForRoot);

        // Objects without a collider or renderer still need a stable frame for their
        // UI controls. Their transform position is the best available fallback.
        if (!hasMeasuredPointForRoot)
            AddScaleFromEdgeFramePoint(selectionRoot.position, ref frame, ref hasMeasuredPointForRoot);

        hasMeasuredPoint |= hasMeasuredPointForRoot;
    }

    static void AddLocalBoundsCornersToScaleFromEdgeFrame(
        Transform boundsTransform,
        Bounds localBounds,
        ref ScaleFromEdgeFrame frame,
        ref bool hasMeasuredPoint)
    {
        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;
        AddScaleFromEdgeFramePoint(boundsTransform.TransformPoint(new Vector3(min.x, min.y, localBounds.center.z)), ref frame, ref hasMeasuredPoint);
        AddScaleFromEdgeFramePoint(boundsTransform.TransformPoint(new Vector3(min.x, max.y, localBounds.center.z)), ref frame, ref hasMeasuredPoint);
        AddScaleFromEdgeFramePoint(boundsTransform.TransformPoint(new Vector3(max.x, min.y, localBounds.center.z)), ref frame, ref hasMeasuredPoint);
        AddScaleFromEdgeFramePoint(boundsTransform.TransformPoint(new Vector3(max.x, max.y, localBounds.center.z)), ref frame, ref hasMeasuredPoint);
    }

    static void AddWorldBoundsCornersToScaleFromEdgeFrame(
        Bounds worldBounds,
        ref ScaleFromEdgeFrame frame,
        ref bool hasMeasuredPoint)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        float z = worldBounds.center.z;
        AddScaleFromEdgeFramePoint(new Vector3(min.x, min.y, z), ref frame, ref hasMeasuredPoint);
        AddScaleFromEdgeFramePoint(new Vector3(min.x, max.y, z), ref frame, ref hasMeasuredPoint);
        AddScaleFromEdgeFramePoint(new Vector3(max.x, min.y, z), ref frame, ref hasMeasuredPoint);
        AddScaleFromEdgeFramePoint(new Vector3(max.x, max.y, z), ref frame, ref hasMeasuredPoint);
    }

    static void AddScaleFromEdgeFramePoint(
        Vector3 worldPoint,
        ref ScaleFromEdgeFrame frame,
        ref bool hasMeasuredPoint)
    {
        Vector3 fromPivot = worldPoint - frame.pivot;
        float x = Vector3.Dot(fromPivot, frame.right);
        float y = Vector3.Dot(fromPivot, frame.up);
        frame.minX = Mathf.Min(frame.minX, x);
        frame.maxX = Mathf.Max(frame.maxX, x);
        frame.minY = Mathf.Min(frame.minY, y);
        frame.maxY = Mathf.Max(frame.maxY, y);
        hasMeasuredPoint = true;
    }

    static void EncapsulateWorldBounds(Bounds boundsToAdd, ref bool hasBounds, ref Bounds worldBounds)
    {
        if (!hasBounds)
        {
            worldBounds = boundsToAdd;
            hasBounds = true;
        }
        else
        {
            worldBounds.Encapsulate(boundsToAdd);
        }
    }

    void UnselectObject()
    {
        CancelActiveTransform();

        // A multi-selection is editor-only data, so deselecting simply discards that
        // data. It never needs to repair the level hierarchy.
        bool wasMultipleSelection = HasMultipleSelection;
        ClearSelectedSelectionRoots();
        DestroySelectionPivot();
        SetSelectedObject(null, !wasMultipleSelection);

        HideSelectionControls();
    }

    // This is the sole owner of selected/previous-selection state. A deselection is
    // provisional: it only replaces the snap target if the next selection is a
    // different object. That lets reselecting an object retain the earlier target.
    void SetSelectedObject(GameObject nextSelectedObject, bool updateSelectionHistory = true)
    {
        if (selectedObject == nextSelectedObject)
            return;

        if (!updateSelectionHistory)
        {
            selectedObject = nextSelectedObject;
            return;
        }

        if (nextSelectedObject == null)
        {
            if (selectedObject != null)
                deselectedObjectAwaitingReplacement = selectedObject;
        }
        else if (selectedObject != null)
        {
            lastSelectedObject = selectedObject;
            deselectedObjectAwaitingReplacement = null;
        }
        else if (deselectedObjectAwaitingReplacement != null &&
                 deselectedObjectAwaitingReplacement != nextSelectedObject)
        {
            lastSelectedObject = deselectedObjectAwaitingReplacement;
            deselectedObjectAwaitingReplacement = null;
        }
        else
        {
            // Reselecting the object we just deselected: retain the prior snap target.
            deselectedObjectAwaitingReplacement = null;
        }

        selectedObject = nextSelectedObject;
    }

    void ClearSelectionHistory()
    {
        lastSelectedObject = null;
        deselectedObjectAwaitingReplacement = null;
    }

    void CancelSuspendedSelectionRestore()
    {
        isSelectionSuspendedForSave = false;
        suspendedSelectionObjects.Clear();
    }

    bool IsAddSelectionModifierHeld()
    {
        return IsShiftModeHeld;
    }

    bool IsRemoveSelectionModifierHeld()
    {
        return IsCtrlModeHeld;
    }

    void SelectObjectsInsideBox(bool addToCurrentSelection, bool removeFromCurrentSelection)
    {
        if (!hasBoxSelectionWorldPositions)
        {
            Debug.LogError("LevelEditor could not box-select objects because its world-space box coordinates are unavailable.", this);
            return;
        }

        Collider2D[] collidersInBox = Physics2D.OverlapAreaAll(
            boxSelectionStartWorldPosition,
            boxSelectionCurrentWorldPosition);
        List<GameObject> objectsToSelect = new List<GameObject>(collidersInBox.Length);
        foreach (Collider2D collider in collidersInBox)
            objectsToSelect.Add(collider.gameObject);

        if (removeFromCurrentSelection)
            RemoveObjectsFromSelection(objectsToSelect);
        else if (addToCurrentSelection)
            AddObjectsToSelection(objectsToSelect);
        else
            SelectObjects(objectsToSelect);
    }

    void RefreshBoxSelectionVisualFromWorld()
    {
        if (!hasSelectionDragExceededThreshold ||
            !hasBoxSelectionWorldPositions ||
            pointerInteraction != EditorPointerInteraction.BoxSelection ||
            PointerInput.Instance == null ||
            !PointerInput.Instance.IsHeld)
        {
            return;
        }

        if (!PointerInput.Instance.TryGetWorldPositionNoDepth(
                PointerInput.Instance.ScreenPosition,
                out boxSelectionCurrentWorldPosition) ||
            CameraViewManager.Instance == null ||
            !CameraViewManager.Instance.TryGetActiveWorldCamera(out Camera activeWorldCamera))
        {
            SetBoxSelectionVisualVisible(false);
            return;
        }

        Vector3 startScreenPosition = activeWorldCamera.WorldToScreenPoint(boxSelectionStartWorldPosition);
        Vector3 currentScreenPosition = activeWorldCamera.WorldToScreenPoint(boxSelectionCurrentWorldPosition);
        UpdateBoxSelectionVisual(
            new Vector2(startScreenPosition.x, startScreenPosition.y),
            new Vector2(currentScreenPosition.x, currentScreenPosition.y));
    }

    void UpdateBoxSelectionVisual(Vector2 pressStartScreenPosition, Vector2 currentScreenPosition)
    {
        if (boxSelectionVisual == null || boxSelectionCanvas == null || boxSelectionVisualParent == null)
            return;

        Camera canvasCamera = boxSelectionCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : boxSelectionCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                boxSelectionVisualParent,
                pressStartScreenPosition,
                canvasCamera,
                out Vector2 pressStartLocalPosition) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                boxSelectionVisualParent,
                currentScreenPosition,
                canvasCamera,
                out Vector2 currentLocalPosition))
            return;

        boxSelectionVisual.anchorMin = new Vector2(0.5f, 0.5f);
        boxSelectionVisual.anchorMax = new Vector2(0.5f, 0.5f);
        boxSelectionVisual.pivot = new Vector2(0.5f, 0.5f);
        boxSelectionVisual.anchoredPosition = (pressStartLocalPosition + currentLocalPosition) * 0.5f;
        boxSelectionVisual.sizeDelta = new Vector2(
            Mathf.Abs(currentLocalPosition.x - pressStartLocalPosition.x),
            Mathf.Abs(currentLocalPosition.y - pressStartLocalPosition.y));
        SetBoxSelectionVisualVisible(true);
    }

    void SetBoxSelectionVisualVisible(bool shouldShow)
    {
        if (boxSelectionVisual != null && boxSelectionVisual.gameObject.activeSelf != shouldShow)
            boxSelectionVisual.gameObject.SetActive(shouldShow);
    }

    void HideSelectionControls()
    {
        if (selectionControlsUI != null)
        {
            selectionControlsUI.SetSelectedTransform(null);
            selectionControlsUI.SetVisible(false);
        }

        if (scaleFromEdgeControlsUI != null)
            scaleFromEdgeControlsUI.SetVisible(false);

        if (createPersistentGroupButton != null)
            createPersistentGroupButton.SetActive(false);
    }

    void SuspendSelectionForSave()
    {
        if (isSelectionSuspendedForSave)
            return;

        suspendedSelectionObjects.Clear();
        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot != null)
                suspendedSelectionObjects.Add(selectionRoot.gameObject);
        }

        ClearSelectedSelectionRoots();
        DestroySelectionPivot();
        SetSelectedObject(null, false);
        HideSelectionControls();
        isSelectionSuspendedForSave = suspendedSelectionObjects.Count > 0;
    }

    void RestoreSelectionAfterSave()
    {
        if (!isSelectionSuspendedForSave)
            return;

        isSelectionSuspendedForSave = false;

        List<GameObject> objectsToRestore = new List<GameObject>();
        foreach (GameObject selectedObjectBeforeSave in suspendedSelectionObjects)
        {
            if (selectedObjectBeforeSave != null)
                objectsToRestore.Add(selectedObjectBeforeSave);
        }
        suspendedSelectionObjects.Clear();

        if (objectsToRestore.Count == 0)
            return;

        SelectObjects(objectsToRestore);
    }

    // UIManager calls this before it disables the editor. Only real selected roots
    // are remembered; the editor-only selection pivot is recreated when returning.
    public void RememberStateBeforeLeavingEditor()
    {
        CancelSuspendedSelectionRestore();
        selectionObjectsBeforeLeavingEditor.Clear();

        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot != null)
                selectionObjectsBeforeLeavingEditor.Add(selectionRoot.gameObject);
        }

        levelRevisionWhenEditorWasLeft = LevelManager.Instance != null
            ? LevelManager.Instance.CurrentLevelRevision
            : -1;
        hasRememberedEditorState = true;
        ClearSelectionHistory();
    }

    // Returns true when a different level was loaded while the editor was inactive.
    // In that case old object references must not be restored.
    public bool RestoreStateAfterReturningToEditor()
    {
        if (!hasRememberedEditorState)
            return false;

        hasRememberedEditorState = false;
        bool levelChanged = LevelManager.Instance == null ||
                            LevelManager.Instance.CurrentLevelRevision != levelRevisionWhenEditorWasLeft;
        levelRevisionWhenEditorWasLeft = -1;

        if (levelChanged)
        {
            selectionObjectsBeforeLeavingEditor.Clear();
            ClearSelectionHistory();
            return true;
        }

        List<GameObject> objectsToRestore = new List<GameObject>(selectionObjectsBeforeLeavingEditor);
        selectionObjectsBeforeLeavingEditor.Clear();
        if (objectsToRestore.Count > 0)
            SelectObjects(objectsToRestore);

        return false;
    }

    void ConfigureSelectionControlsForSelectedObject()
    {
        if (!TryGetSelectionControlAvailability(out SelectionControlAvailability availability))
            return;

        selectionControlsUI.SetControlAvailability(
            availability.canDuplicate,
            availability.canRotate);

        if (scaleFromEdgeControlsUI != null)
        {
            scaleFromEdgeControlsUI.SetControlAvailability(
                availability.canScaleHorizontally,
                availability.canScaleVertically,
                availability.canScaleBoth);
        }
    }

    bool TryGetSelectionControlAvailability(out SelectionControlAvailability availability)
    {
        availability = default;
        if (selectedObject == null)
            return false;

        bool hasSelectionMember = false;
        if (HasMultipleSelection)
        {
            foreach (Transform selectionRoot in selectedSelectionRoots)
            {
                if (selectionRoot == null)
                    continue;

                SelectionControlAvailability memberAvailability = SelectionControlAvailability.ForObject(selectionRoot.gameObject);
                if (hasSelectionMember)
                    availability.IntersectWith(memberAvailability);
                else
                {
                    availability = memberAvailability;
                    hasSelectionMember = true;
                }
            }
        }
        else if (IsSelectedPersistentGroup)
        {
            // Persistent Groups use the same transform rules as a temporary multi-selection.
            availability = new SelectionControlAvailability
            {
                canDuplicate = true,
                canScaleBoth = true,
                canScaleHorizontally = false,
                canScaleVertically = false,
                canRotate = true
            };
            hasSelectionMember = true;
        }
        else
        {
            availability = SelectionControlAvailability.ForObject(selectedObject);
            hasSelectionMember = true;
        }

        if (!hasSelectionMember)
            return false;

        // Rotation affects the shared editor-only pivot rather than one member's
        // individual transform. That remains valid for circles and other roots that
        // intentionally hide their individual rotate control.
        if (HasMultipleSelection)
        {
            // Groups scale uniformly from their corners. Independent edge scaling is
            // intentionally limited to one selected object, where the affected axis
            // and fixed opposite edge remain unambiguous.
            availability.canScaleHorizontally = false;
            availability.canScaleVertically = false;
            availability.canRotate = true;
        }

        return true;
    }

    float RoundToIncrement(float val, float increment)
    {
        if (increment == 0)
            return val;
        else
        {
            return Mathf.Round(val / increment) * increment;
        }
    }

    // These are called by the screen-space selection controls. Keeping the transform work here
    // means both the controls and the editor continue to have one owner for object edits.
    public void BeginObjectTransformControl(ObjectTransformControl control, int pointerId, Vector2 screenPosition)
    {
        if (selectedObject == null || activeTransform != ActiveTransform.None)
            return;

        switch (control)
        {
            case ObjectTransformControl.MoveBoth:
            case ObjectTransformControl.Duplicate:
                if (TryBeginMoveSelectedObject(control, screenPosition))
                    BeginActiveTransform(ActiveTransform.Move, pointerId, screenPosition);
                break;
            case ObjectTransformControl.Rotate:
                if (TryBeginRotateSelectedObject(screenPosition))
                    BeginActiveTransform(ActiveTransform.Rotate, pointerId, screenPosition);
                break;
        }
    }

    public void EndObjectTransformControl(ObjectTransformControl control, int pointerId)
    {
        if (pointerId != activeTransformPointerId)
            return;

        if (activeTransform == ActiveTransform.Move && control == activeMoveControl)
            CompleteActiveTransform();
        else if (activeTransform == ActiveTransform.Rotate && control == ObjectTransformControl.Rotate)
            CompleteActiveTransform();
    }

    public void BeginScaleFromEdgeControl(ScaleFromEdgeHandle handle, int pointerId, Vector2 screenPosition)
    {
        if (selectedObject == null || activeTransform != ActiveTransform.None ||
            !TryGetSelectionControlAvailability(out SelectionControlAvailability availability) ||
            !IsScaleFromEdgeHandleAvailable(handle, availability))
            return;

        if (TryBeginScaleFromEdgeSelectedObject(handle, screenPosition))
            BeginActiveTransform(ActiveTransform.ScaleFromEdge, pointerId, screenPosition);
    }

    public void EndScaleFromEdgeControl(ScaleFromEdgeHandle handle, int pointerId)
    {
        if (pointerId != activeTransformPointerId ||
            activeTransform != ActiveTransform.ScaleFromEdge ||
            handle != activeScaleFromEdgeGesture.handle)
            return;

        CompleteActiveTransform();
    }

    // These combine the physical keyboard keys with the matching held screen button.
    // Keep consumers dependent on the modes rather than on either input source.
    public bool IsShiftModeHeld => isShiftModeButtonHeld ||
                                   Input.GetKey(KeyCode.LeftShift) ||
                                   Input.GetKey(KeyCode.RightShift);

    public bool IsCtrlModeHeld => isCtrlModeButtonHeld ||
                                  Input.GetKey(KeyCode.LeftControl) ||
                                  Input.GetKey(KeyCode.RightControl);

    public void SetModifierButtonHeld(EditorModifier modifier, bool isHeld)
    {
        switch (modifier)
        {
            case EditorModifier.Shift:
                isShiftModeButtonHeld = isHeld;
                break;
            case EditorModifier.Ctrl:
                isCtrlModeButtonHeld = isHeld;
                break;
        }
    }

    static bool IsScaleFromEdgeHandleAvailable(ScaleFromEdgeHandle handle, SelectionControlAvailability availability)
    {
        return handle switch
        {
            ScaleFromEdgeHandle.Left or ScaleFromEdgeHandle.Right => availability.canScaleHorizontally,
            ScaleFromEdgeHandle.Up or ScaleFromEdgeHandle.Down => availability.canScaleVertically,
            _ => availability.canScaleBoth
        };
    }

    void UpdateActiveScreenSpaceTransformControl()
    {
        PointerInput pointerInput = PointerInput.Instance;
        if (pointerInput == null || activeTransform == ActiveTransform.None || activeTransformPointerId == int.MinValue)
            return;

        if (!pointerInput.TryGetScreenPosition(activeTransformPointerId, out Vector2 pointerScreenPosition) ||
            !pointerInput.TryGetWorldPositionNoDepth(pointerScreenPosition, out Vector3 pointerWorldPosition))
        {
            CancelActiveTransform();
            return;
        }

        if (selectedObject == null)
        {
            CancelActiveTransform();
            return;
        }

        switch (activeTransform)
        {
            case ActiveTransform.Move:
                UpdateMoveSelectedObject(pointerWorldPosition, Vector2.Distance(activeTransformPressScreenPosition, pointerScreenPosition));
                break;
            case ActiveTransform.Rotate:
                UpdateRotateSelectedObject(pointerWorldPosition);
                break;
            case ActiveTransform.ScaleFromEdge:
                UpdateScaleFromEdgeSelectedObject(pointerWorldPosition);
                break;
        }
    }

    void BeginActiveTransform(ActiveTransform transform, int pointerId, Vector2 screenPosition)
    {
        // A transform owns its complete press-to-release interaction, even when its
        // active transform data ends before LevelEditor handles that release.
        hasSelectionDragExceededThreshold = false;
        SetBoxSelectionVisualVisible(false);
        pointerInteraction = EditorPointerInteraction.Transform;
        activeTransform = transform;
        activeTransformPointerId = pointerId;
        activeTransformPressScreenPosition = screenPosition;
    }

    void CompleteActiveTransform()
    {
        EndActiveTransform(deleteMovedObject: true);
    }

    void CancelActiveTransform()
    {
        EndActiveTransform(deleteMovedObject: false);
    }

    void EndActiveTransform(bool deleteMovedObject)
    {
        ActiveTransform transformToEnd = activeTransform;
        if (transformToEnd == ActiveTransform.None)
            return;

        activeTransform = ActiveTransform.None;
        activeTransformPointerId = int.MinValue;
        activeTransformPressScreenPosition = default;

        switch (transformToEnd)
        {
            case ActiveTransform.Move:
                EndMoveSelectedObject(deleteMovedObject);
                break;
            case ActiveTransform.Rotate:
                EndRotateSelectedObject();
                break;
            case ActiveTransform.ScaleFromEdge:
                EndScaleSelectedObject();
                break;
        }

        // For a transform finger that is not the primary gameplay pointer, the
        // primary pointer may still be held. It retains transform ownership until
        // it releases; otherwise this transform has fully finished its gesture.
        if (PointerInput.Instance == null || !PointerInput.Instance.IsHeld)
            pointerInteraction = EditorPointerInteraction.None;
    }

    bool TryBeginMoveSelectedObject(ObjectTransformControl control, Vector2 screenPosition)
    {
        if (!PointerInput.Instance.TryGetWorldPositionNoDepth(screenPosition, out pointerPositionAtStartMove))
        {
            Debug.LogError("LevelEditor could not begin moving an object because a valid pointer world position is unavailable.", this);
            return false;
        }

        if (control == ObjectTransformControl.Duplicate)
        {
            DuplicateSelectedObjects();
            ConfigureSelectionControlsForSelectedObject();
        }

        activeMoveControl = control;
        selectedObjectPositionAtStartMove = selectedObject.transform.position;
        CaptureSelectionTransformStates();
        wasShiftModeHeldDuringMove = false;
        activeShiftMoveConstraint = ShiftMoveConstraint.None;

        if (IsWorldTransform)
        {
            moveIncrementOffset = Vector3.zero;
        }
        else
        {
            moveIncrementOffset = new Vector3(
                selectedObjectPositionAtStartMove.x - RoundToIncrement(selectedObjectPositionAtStartMove.x, moveIncrement),
                selectedObjectPositionAtStartMove.y - RoundToIncrement(selectedObjectPositionAtStartMove.y, moveIncrement),
                0f);
        }

        moveOffset = selectedObject.transform.position - pointerPositionAtStartMove;
        return true;
    }

    void DuplicateSelectedObjects()
    {
        List<GameObject> duplicates = new List<GameObject>(selectedSelectionRoots.Count);
        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot == null)
                continue;

            GameObject duplicate = Instantiate(selectionRoot.gameObject, selectionRoot.parent);
            duplicate.name = duplicate.name.Replace("(Clone)", "");
            duplicates.Add(duplicate);
        }

        SelectObjects(duplicates);
    }

    void CaptureSelectionTransformStates()
    {
        activeSelectionTransformStates.Clear();
        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot == null)
                continue;

            activeSelectionTransformStates.Add(new SelectionTransformState
            {
                transform = selectionRoot,
                worldPosition = selectionRoot.position,
                worldRotation = selectionRoot.rotation,
                localScale = selectionRoot.localScale
            });
        }
    }

    void UpdateMoveSelectedObject(Vector3 pointerWorldPosition, float dragDistancePixels)
    {
        if (selectedObject == null)
            return;

        // A quick click duplicates in place. It starts moving only after the same threshold the
        // legacy world-space control used, so the two control systems feel the same.
        Vector3 desiredPosition;
        if (activeMoveControl == ObjectTransformControl.Duplicate && dragDistancePixels < MINIMUM_DRAG_DISTANCE_PIXELS)
        {
            desiredPosition = selectedObjectPositionAtStartMove;
        }
        else
        {
            float newX = RoundToIncrement(pointerWorldPosition.x + moveOffset.x, moveIncrement) + moveIncrementOffset.x;
            float newY = RoundToIncrement(pointerWorldPosition.y + moveOffset.y, moveIncrement) + moveIncrementOffset.y;
            desiredPosition = new Vector3(newX, newY, 0f);
        }

        UpdateShiftMoveGuides(ref desiredPosition);
        selectedObject.transform.position = desiredPosition;
        ApplyMoveToSelectedRoots(desiredPosition - selectedObjectPositionAtStartMove);

        SetSelectedRootsActive(!pointerIsOverObjectSelectionBar || !CanDeleteSelectedRoots());

    }

    void ApplyMoveToSelectedRoots(Vector3 movement)
    {
        if (!HasMultipleSelection)
            return;

        foreach (SelectionTransformState state in activeSelectionTransformStates)
        {
            if (state.transform != null)
                state.transform.position = state.worldPosition + movement;
        }
    }

    void MoveMultipleSelectionRootsBy(Vector3 movement)
    {
        if (!HasMultipleSelection)
            return;

        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot != null)
                selectionRoot.position += movement;
        }
    }

    bool CanDeleteSelectedRoots()
    {
        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot != null && selectionRoot.name == "PlayerStartPoint")
                return false;
        }

        return selectedSelectionRoots.Count > 0;
    }

    void SetSelectedRootsActive(bool active)
    {
        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot != null)
                selectionRoot.gameObject.SetActive(active);
        }
    }

    void UpdateShiftMoveGuides(ref Vector3 desiredPosition)
    {
        bool canUseShiftMoveGuides = activeMoveControl is ObjectTransformControl.MoveBoth or ObjectTransformControl.Duplicate;
        bool shouldShowGuides = canUseShiftMoveGuides && IsShiftModeHeld;
        if (!shouldShowGuides)
        {
            if (wasShiftModeHeldDuringMove)
                SetMoveGuideLinesVisible(false);

            wasShiftModeHeldDuringMove = false;
            activeShiftMoveConstraint = ShiftMoveConstraint.None;
            return;
        }

        if (!wasShiftModeHeldDuringMove)
            BeginShiftMoveGuides();

        // Decide using the same candidate position the object would have without
        // guides. That includes the grab offset and move increment, so the visual
        // object and the guide-selection point stay aligned.
        Vector3 candidateOffsetFromGuideOrigin = desiredPosition - shiftMoveGuideOrigin;
        float distanceToRightGuide = Mathf.Abs(Vector3.Dot(candidateOffsetFromGuideOrigin, shiftMoveGuideUp));
        float distanceToUpGuide = Mathf.Abs(Vector3.Dot(candidateOffsetFromGuideOrigin, shiftMoveGuideRight));

        // Keep the current guide when the pointer is exactly between them, avoiding
        // visual flicker while the user crosses the intersection point.
        if (distanceToRightGuide < distanceToUpGuide)
            activeShiftMoveConstraint = ShiftMoveConstraint.AlongRight;
        else if (distanceToUpGuide < distanceToRightGuide)
            activeShiftMoveConstraint = ShiftMoveConstraint.AlongUp;

        if (activeShiftMoveConstraint == ShiftMoveConstraint.AlongRight)
        {
            float movementAlongRight = Vector3.Dot(desiredPosition - shiftMoveGuideOrigin, shiftMoveGuideRight);
            desiredPosition = shiftMoveGuideOrigin + shiftMoveGuideRight * movementAlongRight;
        }
        else if (activeShiftMoveConstraint == ShiftMoveConstraint.AlongUp)
        {
            float movementAlongUp = Vector3.Dot(desiredPosition - shiftMoveGuideOrigin, shiftMoveGuideUp);
            desiredPosition = shiftMoveGuideOrigin + shiftMoveGuideUp * movementAlongUp;
        }
    }

    void BeginShiftMoveGuides()
    {
        wasShiftModeHeldDuringMove = true;
        shiftMoveGuideOrigin = GetMoveIncrementAlignedPosition(selectedObject.transform.position);

        shiftMoveGuideRight = IsWorldTransform ? Vector3.right : selectedObject.transform.right;
        shiftMoveGuideUp = IsWorldTransform ? Vector3.up : selectedObject.transform.up;
        shiftMoveGuideRight.z = 0f;
        shiftMoveGuideUp.z = 0f;
        shiftMoveGuideRight.Normalize();
        shiftMoveGuideUp.Normalize();
        activeShiftMoveConstraint = ShiftMoveConstraint.AlongRight;

        horizontalLine.SetPosition(0, shiftMoveGuideOrigin - shiftMoveGuideRight * MOVE_GUIDE_LINE_HALF_LENGTH);
        horizontalLine.SetPosition(1, shiftMoveGuideOrigin + shiftMoveGuideRight * MOVE_GUIDE_LINE_HALF_LENGTH);
        verticalLine.SetPosition(0, shiftMoveGuideOrigin - shiftMoveGuideUp * MOVE_GUIDE_LINE_HALF_LENGTH);
        verticalLine.SetPosition(1, shiftMoveGuideOrigin + shiftMoveGuideUp * MOVE_GUIDE_LINE_HALF_LENGTH);
        SetMoveGuideLinesVisible(true);
    }

    Vector3 GetMoveIncrementAlignedPosition(Vector3 position)
    {
        // This is the same grid used by the unconstrained move candidate above.
        // In local mode, moveIncrementOffset preserves the object's initial offset
        // from the world grid, so the guide remains on that same effective lattice.
        return new Vector3(
            RoundToIncrement(position.x - moveIncrementOffset.x, moveIncrement) + moveIncrementOffset.x,
            RoundToIncrement(position.y - moveIncrementOffset.y, moveIncrement) + moveIncrementOffset.y,
            0f);
    }

    void SetMoveGuideLinesVisible(bool visible)
    {
        verticalLine.gameObject.SetActive(visible);
        horizontalLine.gameObject.SetActive(visible);
    }

    void EndMoveSelectedObject(bool deleteMovedObject)
    {
        SetMoveGuideLinesVisible(false);
        wasShiftModeHeldDuringMove = false;
        activeShiftMoveConstraint = ShiftMoveConstraint.None;

        NormalizeSelectedRootRotations();

        if (deleteMovedObject && selectedObject != null &&
            pointerIsOverObjectSelectionBar && CanDeleteSelectedRoots())
        {
            foreach (Transform selectionRoot in selectedSelectionRoots)
            {
                if (selectionRoot != null)
                    Destroy(selectionRoot.gameObject);
            }

            UnselectObject();
            deselectObjectButton.gameObject.SetActive(false);
        }
        else if (selectedObject != null)
        {
            // A cancelled move must not leave an object hidden merely because the
            // pointer last passed over the delete area.
            SetSelectedRootsActive(true);
        }
    }

    void NormalizeSelectedRootRotations()
    {
        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot != null)
                NormalizeRotationInvariantCircularObjectRotations(selectionRoot);
        }
    }

    bool TryBeginRotateSelectedObject(Vector2 screenPosition)
    {
        if (!PointerInput.Instance.TryGetWorldPositionNoDepth(screenPosition, out Vector3 pointerWorldPosition))
        {
            Debug.LogError("LevelEditor could not begin rotating an object because a valid pointer world position is unavailable.", this);
            return false;
        }

        rotationLine.gameObject.SetActive(true);
        rotationLine.SetPosition(0, selectedObject.transform.position);

        selectedObjectRotationAtStartRotate = selectedObject.transform.localEulerAngles.z;
        selectionPivotRotationAtStartRotate = selectedObject.transform.rotation;
        selectionPivotPositionAtStartRotate = selectedObject.transform.position;
        CaptureSelectionTransformStates();
        Vector3 direction = pointerWorldPosition - selectedObject.transform.position;
        angleToPointerAtStartRotate = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        rotationIncrementOffset = IsWorldTransform
            ? 0f
            : selectedObjectRotationAtStartRotate - RoundToIncrement(selectedObjectRotationAtStartRotate, rotateIncrement);
        return true;
    }

    void UpdateRotateSelectedObject(Vector3 pointerWorldPosition)
    {
        if (selectedObject == null)
            return;

        Vector3 direction = pointerWorldPosition - selectedObject.transform.position;
        float currentAngleToPointer = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float deltaAngle = currentAngleToPointer - angleToPointerAtStartRotate;
        float newRotation = RoundToIncrement(selectedObjectRotationAtStartRotate + deltaAngle, rotateIncrement);

        if (!IsWorldTransform)
            newRotation += rotationIncrementOffset;

        selectedObject.transform.localRotation = Quaternion.Euler(0f, 0f, newRotation);
        ApplyRotationToSelectedRoots();
        rotationLine.SetPosition(1, pointerWorldPosition);
    }

    void ApplyRotationToSelectedRoots()
    {
        if (!HasMultipleSelection)
            return;

        Quaternion rotationDelta = selectedObject.transform.rotation * Quaternion.Inverse(selectionPivotRotationAtStartRotate);
        foreach (SelectionTransformState state in activeSelectionTransformStates)
        {
            if (state.transform == null)
                continue;

            state.transform.position = selectionPivotPositionAtStartRotate +
                                       rotationDelta * (state.worldPosition - selectionPivotPositionAtStartRotate);
            state.transform.rotation = rotationDelta * state.worldRotation;
        }
    }

    void EndRotateSelectedObject()
    {
        rotationLine.gameObject.SetActive(false);

        NormalizeSelectedRootRotations();
    }

    bool TryBeginScaleFromEdgeSelectedObject(ScaleFromEdgeHandle handle, Vector2 screenPosition)
    {
        if (!PointerInput.Instance.TryGetWorldPositionNoDepth(screenPosition, out Vector3 pointerWorldPositionAtStart))
        {
            Debug.LogError("LevelEditor could not begin edge scaling because a valid pointer world position is unavailable.", this);
            return false;
        }

        if (!TryGetScaleFromEdgeFrame(out ScaleFromEdgeFrame frameAtStart))
        {
            Debug.LogError("LevelEditor could not begin edge scaling because the selected object has no measurable scale frame.", this);
            return false;
        }

        ConfigureScaleFromEdgeGesture(
            handle,
            frameAtStart,
            pointerWorldPositionAtStart,
            IsScaleFromEdgeBothSidesModeHeld(),
            IsIndependentCornerScaleModeHeld(handle));
        return true;
    }

    void UpdateScaleFromEdgeSelectedObject(Vector3 pointerWorldPosition)
    {
        ScaleFromEdgeGesture gesture = activeScaleFromEdgeGesture;
        bool shouldScaleFromBothSides = IsScaleFromEdgeBothSidesModeHeld();
        bool shouldScaleIndependently = IsIndependentCornerScaleModeHeld(gesture.handle);
        if (gesture.scalesFromBothSides != shouldScaleFromBothSides ||
            gesture.scalesIndependently != shouldScaleIndependently)
        {
            // Begin a fresh gesture at the current size and pointer location when
            // a modifier changes. This changes the anchor without a visual jump.
            if (!TryGetScaleFromEdgeFrame(out ScaleFromEdgeFrame currentFrame))
                return;

            ConfigureScaleFromEdgeGesture(
                gesture.handle,
                currentFrame,
                pointerWorldPosition,
                shouldScaleFromBothSides,
                shouldScaleIndependently);
            gesture = activeScaleFromEdgeGesture;
        }

        Vector3 pointerDelta = pointerWorldPosition - gesture.pointerWorldPositionAtStart;
        bool scalesX = DoesScaleFromEdgeHandleScaleX(gesture.handle);
        bool scalesY = DoesScaleFromEdgeHandleScaleY(gesture.handle);
        bool scalesUniformly = scalesX && scalesY;

        float xFactor = 1f;
        float yFactor = 1f;
        if (scalesUniformly && !gesture.scalesIndependently)
        {
            Vector3 movingCornerDirection =
                (GetScaleFromEdgeHandleXSign(gesture.handle) * gesture.frameAtStart.right * gesture.frameAtStart.Width +
                 GetScaleFromEdgeHandleYSign(gesture.handle) * gesture.frameAtStart.up * gesture.frameAtStart.Height).normalized;
            float diagonalExtent = Mathf.Sqrt(
                gesture.frameAtStart.Width * gesture.frameAtStart.Width +
                gesture.frameAtStart.Height * gesture.frameAtStart.Height);
            float uniformFactor = GetScaleFromEdgeGestureFactor(
                gesture.scalesFromBothSides,
                diagonalExtent,
                Vector3.Dot(pointerDelta, movingCornerDirection),
                Mathf.Max(gesture.minimumXFactor, gesture.minimumYFactor),
                Mathf.Min(gesture.maximumXFactor, gesture.maximumYFactor));
            xFactor = uniformFactor;
            yFactor = uniformFactor;
        }
        else
        {
            if (scalesX)
            {
                Vector3 outwardDirection = GetScaleFromEdgeHandleXSign(gesture.handle) * gesture.frameAtStart.right;
                xFactor = GetScaleFromEdgeGestureFactor(
                    gesture.scalesFromBothSides,
                    gesture.frameAtStart.Width,
                    Vector3.Dot(pointerDelta, outwardDirection),
                    gesture.minimumXFactor,
                    gesture.maximumXFactor);
            }

            if (scalesY)
            {
                Vector3 outwardDirection = GetScaleFromEdgeHandleYSign(gesture.handle) * gesture.frameAtStart.up;
                yFactor = GetScaleFromEdgeGestureFactor(
                    gesture.scalesFromBothSides,
                    gesture.frameAtStart.Height,
                    Vector3.Dot(pointerDelta, outwardDirection),
                    gesture.minimumYFactor,
                    gesture.maximumYFactor);
            }
        }

        Vector3 newScale = gesture.selectedLocalScaleAtStart;
        newScale.x *= xFactor;
        newScale.y *= yFactor;
        selectedObject.transform.localScale = newScale;

        Vector3 newPosition = gesture.selectedWorldPositionAtStart;
        if (!gesture.scalesFromBothSides && scalesX)
        {
            float fixedX = GetScaleFromEdgeHandleXSign(gesture.handle) > 0f
                ? gesture.frameAtStart.minX
                : gesture.frameAtStart.maxX;
            newPosition += gesture.frameAtStart.right * ((1f - xFactor) * fixedX);
        }

        if (!gesture.scalesFromBothSides && scalesY)
        {
            float fixedY = GetScaleFromEdgeHandleYSign(gesture.handle) > 0f
                ? gesture.frameAtStart.minY
                : gesture.frameAtStart.maxY;
            newPosition += gesture.frameAtStart.up * ((1f - yFactor) * fixedY);
        }

        selectedObject.transform.position = newPosition;
        ApplyScaleToSelectedRoots(gesture, xFactor, yFactor, newPosition);
    }

    void ApplyScaleToSelectedRoots(
        ScaleFromEdgeGesture gesture,
        float xFactor,
        float yFactor,
        Vector3 newSelectionPivotPosition)
    {
        if (!HasMultipleSelection)
            return;

        foreach (SelectionTransformState state in activeSelectionTransformStates)
        {
            if (state.transform == null)
                continue;

            Vector3 offsetFromPivot = state.worldPosition - gesture.selectedWorldPositionAtStart;
            float x = Vector3.Dot(offsetFromPivot, gesture.frameAtStart.right);
            float y = Vector3.Dot(offsetFromPivot, gesture.frameAtStart.up);
            state.transform.position = newSelectionPivotPosition +
                                       gesture.frameAtStart.right * (x * xFactor) +
                                       gesture.frameAtStart.up * (y * yFactor);
            state.transform.localScale = new Vector3(
                state.localScale.x * xFactor,
                state.localScale.y * yFactor,
                state.localScale.z);
        }
    }

    void ConfigureScaleFromEdgeGesture(
        ScaleFromEdgeHandle handle,
        ScaleFromEdgeFrame frameAtStart,
        Vector3 pointerWorldPositionAtStart,
        bool scalesFromBothSides,
        bool scalesIndependently)
    {
        activeScaleFromEdgeGesture = new ScaleFromEdgeGesture
        {
            handle = handle,
            scalesFromBothSides = scalesFromBothSides,
            scalesIndependently = scalesIndependently,
            frameAtStart = frameAtStart,
            selectedLocalScaleAtStart = selectedObject.transform.localScale,
            selectedWorldPositionAtStart = selectedObject.transform.position,
            pointerWorldPositionAtStart = pointerWorldPositionAtStart
        };
        CaptureSelectionTransformStates();
        GetScaleFactorLimits(
            activeScaleFromEdgeGesture.selectedLocalScaleAtStart,
            out activeScaleFromEdgeGesture.minimumXFactor,
            out activeScaleFromEdgeGesture.maximumXFactor,
            out activeScaleFromEdgeGesture.minimumYFactor,
            out activeScaleFromEdgeGesture.maximumYFactor);
    }

    bool IsScaleFromEdgeBothSidesModeHeld()
    {
        return IsShiftModeHeld;
    }

    bool IsIndependentCornerScaleModeHeld(ScaleFromEdgeHandle handle)
    {
        return IsCtrlModeHeld &&
               DoesScaleFromEdgeHandleScaleX(handle) &&
               DoesScaleFromEdgeHandleScaleY(handle) &&
               TryGetSelectionControlAvailability(out SelectionControlAvailability availability) &&
               availability.canScaleHorizontally &&
               availability.canScaleVertically;
    }

    static bool DoesScaleFromEdgeHandleScaleX(ScaleFromEdgeHandle handle)
    {
        return handle != ScaleFromEdgeHandle.Up && handle != ScaleFromEdgeHandle.Down;
    }

    static bool DoesScaleFromEdgeHandleScaleY(ScaleFromEdgeHandle handle)
    {
        return handle != ScaleFromEdgeHandle.Left && handle != ScaleFromEdgeHandle.Right;
    }

    static float GetScaleFromEdgeHandleXSign(ScaleFromEdgeHandle handle)
    {
        return handle is ScaleFromEdgeHandle.Right or ScaleFromEdgeHandle.UpRight or ScaleFromEdgeHandle.DownRight ? 1f : -1f;
    }

    static float GetScaleFromEdgeHandleYSign(ScaleFromEdgeHandle handle)
    {
        return handle is ScaleFromEdgeHandle.Up or ScaleFromEdgeHandle.UpLeft or ScaleFromEdgeHandle.UpRight ? 1f : -1f;
    }

    void GetScaleFactorLimits(
        Vector3 selectedLocalScaleAtStart,
        out float minimumXFactor,
        out float maximumXFactor,
        out float minimumYFactor,
        out float maximumYFactor)
    {
        minimumXFactor = 0f;
        minimumYFactor = 0f;
        maximumXFactor = float.PositiveInfinity;
        maximumYFactor = float.PositiveInfinity;

        foreach (Transform scaleConstrainedTransform in GetScaleConstraintTransforms())
        {
            if (scaleConstrainedTransform == null)
                continue;

            float minimumObjectScale = GetMinimumObjectScale(scaleConstrainedTransform.gameObject);
            float startingXScale = GetStartingEffectiveScale(scaleConstrainedTransform, true, selectedLocalScaleAtStart);
            float startingYScale = GetStartingEffectiveScale(scaleConstrainedTransform, false, selectedLocalScaleAtStart);

            minimumXFactor = Mathf.Max(minimumXFactor, minimumObjectScale / startingXScale);
            maximumXFactor = Mathf.Min(maximumXFactor, maximumScale / startingXScale);
            minimumYFactor = Mathf.Max(minimumYFactor, minimumObjectScale / startingYScale);
            maximumYFactor = Mathf.Min(maximumYFactor, maximumScale / startingYScale);
        }
    }

    IEnumerable<Transform> GetScaleConstraintTransforms()
    {
        if (HasMultipleSelection)
        {
            foreach (Transform selectionRoot in selectedSelectionRoots)
            {
                if (selectionRoot != null)
                    yield return selectionRoot;
            }

            yield break;
        }

        if (IsSelectedPersistentGroup)
        {
            foreach (Transform child in selectedObject.transform)
                yield return child;

            yield break;
        }

        if (selectedObject != null)
            yield return selectedObject.transform;
    }

    float GetStartingEffectiveScale(Transform scaleConstrainedTransform, bool xAxis, Vector3 selectedLocalScaleAtStart)
    {
        float memberScale = Mathf.Abs(xAxis
            ? scaleConstrainedTransform.localScale.x
            : scaleConstrainedTransform.localScale.y);

        if (IsSelectedGroup)
        {
            float groupScale = Mathf.Abs(xAxis
                ? selectedLocalScaleAtStart.x
                : selectedLocalScaleAtStart.y);
            memberScale *= groupScale;
        }

        return Mathf.Max(memberScale, Mathf.Epsilon);
    }

    static float GetMinimumObjectScale(GameObject levelObject)
    {
        return levelObject.name.Contains("Puller") ? 3f : 0.2f;
    }

    static float GetCenteredScaleFactor(float startExtent, float outwardPointerDistance, float minimumFactor, float maximumFactor)
    {
        float desiredFactor = 1f + (2f * outwardPointerDistance / startExtent);
        maximumFactor = Mathf.Max(minimumFactor, maximumFactor);
        return Mathf.Clamp(desiredFactor, minimumFactor, maximumFactor);
    }

    static float GetEdgeScaleFactor(float startExtent, float outwardPointerDistance, float minimumFactor, float maximumFactor)
    {
        float desiredFactor = 1f + (outwardPointerDistance / startExtent);
        maximumFactor = Mathf.Max(minimumFactor, maximumFactor);
        return Mathf.Clamp(desiredFactor, minimumFactor, maximumFactor);
    }

    static float GetScaleFromEdgeGestureFactor(
        bool scalesFromBothSides,
        float startExtent,
        float outwardPointerDistance,
        float minimumFactor,
        float maximumFactor)
    {
        return scalesFromBothSides
            ? GetCenteredScaleFactor(startExtent, outwardPointerDistance, minimumFactor, maximumFactor)
            : GetEdgeScaleFactor(startExtent, outwardPointerDistance, minimumFactor, maximumFactor);
    }

    void EndScaleSelectedObject()
    {
        NormalizeSelectedRootRotations();
    }

    void RefreshSelectionControls()
    {
        bool isBoxSelecting = hasSelectionDragExceededThreshold && PointerInput.Instance.IsHeld;
        bool show = selectedObject != null &&
                    !(activeTransform != ActiveTransform.None || isTryingToPlace || isBoxSelecting);
        selectionControlsUI.SetSelectedTransform(selectedObject != null ? selectedObject.transform : null);
        // Keep the object active while a UI drag is running, but hide its CanvasGroup. That
        // lets its pointer handler still receive the matching drag and release events.
        selectionControlsUI.SetVisible(selectedObject != null && !isTryingToPlace);
        selectionControlsUI.SetControlsVisible(show);

        if (scaleFromEdgeControlsUI != null)
        {
            if (TryGetSelectionControlAvailability(out SelectionControlAvailability availability))
            {
                // Reapply this during the regular refresh as well as at selection
                // time. The edge-controls prefab can become active after its first
                // selection was configured, so this prevents its default handle
                // visibility from leaking into that first selection.
                scaleFromEdgeControlsUI.SetControlAvailability(
                    availability.canScaleHorizontally,
                    availability.canScaleVertically,
                    availability.canScaleBoth);
            }

            ScaleFromEdgeFrame scaleFromEdgeFrame = default;
            bool hasScaleFromEdgeFrame = selectedObject != null && TryGetScaleFromEdgeFrame(out scaleFromEdgeFrame);
            scaleFromEdgeControlsUI.SetSelectionFrame(hasScaleFromEdgeFrame, scaleFromEdgeFrame);
            scaleFromEdgeControlsUI.SetVisible(selectedObject != null && !isTryingToPlace);
            scaleFromEdgeControlsUI.SetControlsVisible(show && hasScaleFromEdgeFrame);
        }

        deselectObjectButton.SetActive(show);
        snapVerticalButton.SetActive(show);
        snapHorizontalButton.SetActive(show);
        if (createPersistentGroupButton != null)
            createPersistentGroupButton.SetActive(show && CanCreatePersistentGroup());
    }

    public void SwitchToPlayMode()
    {
        startLocationIcon.SetActive(false);
        UIManager.Instance.SwitchToPlayerMode();
    }

    #region Object Place Functions
    void StartTryingToPlaceObject()
    {
        if (!PointerInput.Instance.TryGetCurrentWorldPosition(out Vector3 pointerWorldPosition))
        {
            Debug.LogError("LevelEditor could not begin placing an object because a valid pointer world position is unavailable.", this);
            return;
        }

        isTryingToPlace = true;
        pointerInteraction = EditorPointerInteraction.PlaceObject;
        objectCurrentlyTryingToPlace = Instantiate(prefabToPlace, pointerWorldPosition, Quaternion.identity, levelObjectsCollection.transform);
    }
    public void PlaceBooster()
    {
        prefabToPlace = LevelManager.Instance.BoosterPrefab;
        StartTryingToPlaceObject();
    }
    public void PlaceBouncyWall()
    {
        prefabToPlace = LevelManager.Instance.BouncyWallPrefab;
        StartTryingToPlaceObject();
    }
    public void PlaceConstantBooster()
    {
        prefabToPlace = LevelManager.Instance.ConstantBoosterPrefab;
        StartTryingToPlaceObject();
    }
    public void PlaceConstantPuller()
    {
        prefabToPlace = LevelManager.Instance.ConstantPullerPrefab;
        StartTryingToPlaceObject();
    }
    public void PlaceConstantPusher()
    {
        prefabToPlace = LevelManager.Instance.ConstantPusherPrefab;
        StartTryingToPlaceObject();
    }
    public void PlaceFinish()
    {
        prefabToPlace = LevelManager.Instance.FinishPrefab;
        StartTryingToPlaceObject();
    }
    public void PlaceKillCircle()
    {
        prefabToPlace = LevelManager.Instance.KillCirclePrefab;
        StartTryingToPlaceObject();
    }
    public void PlaceKillWall()
    {
        prefabToPlace = LevelManager.Instance.KillWallPrefab;
        StartTryingToPlaceObject();
    }
    public void PlacePuller()
    {
        prefabToPlace = LevelManager.Instance.PullerPrefab;
        StartTryingToPlaceObject();
    }
    public void PlacePusher()
    {
        prefabToPlace = LevelManager.Instance.PusherPrefab;
        StartTryingToPlaceObject();
    }
    public void PlaceSlipperyWall()
    {
        prefabToPlace = LevelManager.Instance.SlipperyWallPrefab;
        StartTryingToPlaceObject();
    }
    #endregion

    // UI events use these to update value of pointerIsOverObjectSelectionBar on pointer enter and exit
    public void SetPointerIsOverObjectSelectionBarTrue()
    {
        pointerIsOverObjectSelectionBar = true;
    }
    public void SetPointerIsOverObjectSelectionBarFalse()
    {
        pointerIsOverObjectSelectionBar = false;
    }

    // TODO: move these to ButtonEventCaller
    // expose level manager functions for level editor UI buttons
    public void SaveLevel()
    {
        if (LevelManager.Instance == null || LevelManager.Instance.IsSavingLevel)
            return;

        SuspendSelectionForSave();
        LevelManager.Instance.SaveLevel(RestoreSelectionAfterSave);
    }
    public void DeleteAllLevelObjects()
    {
        CancelSuspendedSelectionRestore();
        UnselectObject();
        ClearSelectionHistory();
        LevelManager.Instance.DestroyAllExistingLevelObjects();
    }
    public void CopyLevelCodeToClipboard()
    {
        SuspendSelectionForSave();
        try
        {
            LevelManager.Instance.CopyLevelCodeToClipboard();
        }
        finally
        {
            RestoreSelectionAfterSave();
        }
    }
    public void LoadLevelFromClipboard()
    {
        CancelSuspendedSelectionRestore();
        UnselectObject();
        ClearSelectionHistory();
        LevelManager.Instance.GetLevelJsonFromClipboard();
        LevelManager.Instance.LoadLevel();
    }

    public void DeselectObject()
    {
        UnselectObject();
    }

}
