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
    [SerializeField] GameObject ungroupPersistentGroupButton;
    [SerializeField] TMP_Dropdown rotateIncrementDropdown;
    [SerializeField] TMP_Dropdown moveIncrementDropdown;

    // world object references
    [Header("World Objects")]
    [SerializeField] GameObject levelObjectsCollection;
    [SerializeField] GameObject playerStartPoint;

    [Header("Screen Space UI")]
    [SerializeField] ToggleButton worldTransformToggle;
    [SerializeField] SelectionControlsUI selectionControlsUI;
    [SerializeField] RectTransform boxSelectionVisual;
    [SerializeField] Material selectionOutlineMaterial;
    [SerializeField] Material editorGuideMaterial;
    [SerializeField] Material editorGridMaterial;
    [SerializeField] Material scaleSnapHighlightMaterial;

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
    SelectionOutlineRenderer selectionOutline;
    EditorGuideRenderer editorGuides;
    EditorGridRenderer editorGrid;
    ScaleSnapHighlightRenderer scaleSnapHighlights;
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
        public bool snapToWorld;
        public float distanceIncrement;
        public ScaleFromEdgeFrame frameAtStart;
        public Vector3 selectedLocalScaleAtStart;
        public Vector3 selectedWorldPositionAtStart;
        public Vector3 pointerWorldPositionAtStart;
        public float minimumXFactor;
        public float maximumXFactor;
        public float minimumYFactor;
        public float maximumYFactor;
    }

    struct WorldScalePath
    {
        public Vector3 origin;
        public Vector3 direction;
        public float minimum;
        public float maximum;
        public float distancePerFactor;
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

    Vector3 moveOffset;
    Vector3 selectedObjectPositionAtStartMove;
    Vector3 pointerPositionAtStartMove;
    readonly List<SelectionTransformState> activeSelectionTransformStates = new List<SelectionTransformState>();
    float distanceIncrement = 0f;
    Vector3 moveIncrementOffset = new Vector3(0f, 0f, 0f);
    bool wasShiftModeHeldDuringMove;
    bool shiftMoveUsesWorldTransform;
    bool shiftMoveHasSnapped;
    bool preserveWorldMoveOrigin;
    Vector3 worldMoveOrigin;
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
    ScaleFromEdgeGesture activeScaleFromEdgeGesture;
    bool hasGestureGridFrame;
    Vector3 gestureGridOrigin;
    Vector3 gestureGridRight;
    Vector3 gestureGridUp;
    bool selectionContainsCircularObject;
    bool hasCurrentScaleFrame;
    ScaleFromEdgeFrame currentScaleFrame;
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

        if (selectionOutlineMaterial != null && LayerMask.NameToLayer("EditorGuides") >= 0)
            selectionOutline = new SelectionOutlineRenderer(selectionOutlineMaterial);
        else
            Debug.LogError("LevelEditor requires a selection outline material and the EditorGuides layer.", this);

        if (editorGuideMaterial != null && LayerMask.NameToLayer("EditorGuides") >= 0)
            editorGuides = new EditorGuideRenderer(editorGuideMaterial);
        else
            Debug.LogError("LevelEditor requires an editor guide material and the EditorGuides layer.", this);

        if (editorGridMaterial != null && LayerMask.NameToLayer("EditorGuides") >= 0)
            editorGrid = new EditorGridRenderer(editorGridMaterial);
        else
            Debug.LogError("LevelEditor requires an editor grid material and the EditorGuides layer.", this);

        if (scaleSnapHighlightMaterial != null && LayerMask.NameToLayer("EditorGuides") >= 0)
            scaleSnapHighlights = new ScaleSnapHighlightRenderer(scaleSnapHighlightMaterial);
        else
            Debug.LogError("LevelEditor requires a scale snap highlight material and the EditorGuides layer.", this);

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
        if (ungroupPersistentGroupButton != null)
            ungroupPersistentGroupButton.SetActive(false);
    }

    void Start()
    {
        // ensure level editor object and all of its visuals are disabled before starting game
        gameObject.SetActive(false);

        // setup increment dropdown listeners
        moveIncrementDropdown.onValueChanged.AddListener(OnMoveIncrementDropdownChanged);
        rotateIncrementDropdown.onValueChanged.AddListener(OnRotateIncrementDropdownChanged);
        OnMoveIncrementDropdownChanged(moveIncrementDropdown.value);
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
        if (CameraViewManager.Instance != null &&
            CameraViewManager.Instance.TryGetActiveWorldCamera(out Camera activeCamera))
        {
            scaleSnapHighlights?.BeginFrame(activeCamera);
            DrawIncrementGrid(activeCamera);
            selectionOutline?.Draw(activeCamera);
            editorGuides?.Draw(activeCamera);
            DrawTransformHighlights(activeCamera);
        }
        else
            scaleSnapHighlights?.Clear();
    }

    void OnDestroy()
    {
        selectionOutline?.Dispose();
        editorGuides?.Dispose();
        editorGrid?.Dispose();
        scaleSnapHighlights?.Dispose();
    }

    void DrawTransformHighlights(Camera camera)
    {
        if (scaleSnapHighlights == null || selectedObject == null || activeTransform == ActiveTransform.None)
            return;

        if (activeTransform != ActiveTransform.ScaleFromEdge)
        {
            scaleSnapHighlights.DrawPoint(camera, selectedObject.transform.position);
            return;
        }

        if (!TryGetScaleFromEdgeFrame(out ScaleFromEdgeFrame currentFrame))
            return;

        Vector3 currentHandle = currentFrame.GetHandlePosition(activeScaleFromEdgeGesture.handle);
        if (activeScaleFromEdgeGesture.snapToWorld && activeScaleFromEdgeGesture.distanceIncrement > 0f &&
            TryGetWorldScalePath(activeScaleFromEdgeGesture, out WorldScalePath path))
        {
            scaleSnapHighlights.Draw(camera, path.origin, path.direction, path.minimum, path.maximum,
                activeScaleFromEdgeGesture.distanceIncrement, currentHandle);
        }
        else
        {
            scaleSnapHighlights.DrawPoint(camera, currentHandle);
        }
    }

    void DrawIncrementGrid(Camera camera)
    {
        if (IsWorldTransform || selectedObject == null)
            hasGestureGridFrame = false;

        if (editorGrid == null || distanceIncrement <= 0f)
            return;

        Vector3 origin = Vector3.zero;
        Vector3 right = Vector3.right;
        Vector3 up = Vector3.up;
        if (!IsWorldTransform && selectedObject != null)
        {
            if (activeTransform == ActiveTransform.ScaleFromEdge)
            {
                ScaleFromEdgeGesture gesture = activeScaleFromEdgeGesture;
                GetLocalScaleGridFrame(gesture.frameAtStart, gesture.handle, gesture.scalesIndependently,
                    out origin, out right, out up);
                RememberGestureGridFrame(origin, right, up);
            }
            else if (activeTransform == ActiveTransform.Move)
            {
                if (wasShiftModeHeldDuringMove)
                {
                    origin = shiftMoveGuideOrigin;
                    right = shiftMoveGuideRight;
                    up = shiftMoveGuideUp;
                }
                else
                    origin = moveIncrementOffset;
                RememberGestureGridFrame(origin, right, up);
            }
            else if (hasGestureGridFrame)
            {
                origin = gestureGridOrigin;
                right = gestureGridRight;
                up = gestureGridUp;
            }
            else if (selectionContainsCircularObject && hasCurrentScaleFrame)
            {
                // Before the first transform, show a uniform-scale corner path.
                GetLocalScaleGridFrame(currentScaleFrame, ScaleFromEdgeHandle.UpRight, false,
                    out origin, out right, out up);
            }
            else
            {
                Vector3 position = selectedObject.transform.position;
                origin = new Vector3(position.x - RoundToIncrement(position.x, distanceIncrement),
                    position.y - RoundToIncrement(position.y, distanceIncrement), 0f);
            }
        }

        editorGrid.Draw(camera, distanceIncrement, origin, right, up);
    }

    void RememberGestureGridFrame(Vector3 origin, Vector3 right, Vector3 up)
    {
        hasGestureGridFrame = true;
        gestureGridOrigin = origin;
        gestureGridRight = right;
        gestureGridUp = up;
    }

    static void GetLocalScaleGridFrame(ScaleFromEdgeFrame frame, ScaleFromEdgeHandle handle, bool scalesIndependently,
        out Vector3 origin, out Vector3 right, out Vector3 up)
    {
        origin = frame.GetHandlePosition(handle);
        right = frame.right;
        up = frame.up;
        if (DoesScaleFromEdgeHandleScaleX(handle) && DoesScaleFromEdgeHandleScaleY(handle) && !scalesIndependently)
        {
            right = (GetScaleFromEdgeHandleXSign(handle) * right * frame.Width +
                     GetScaleFromEdgeHandleYSign(handle) * up * frame.Height).normalized;
            up = new Vector3(-right.y, right.x, 0f);
        }
    }

    private void OnEnable()
    {
        EventManager.Instance.UnselectObjectEvent.AddListener(UnselectObject);
    }

    private void OnDisable()
    {
        scaleSnapHighlights?.Clear();
        CancelActiveTransform();
        pointerInteraction = EditorPointerInteraction.None;
        hasBoxSelectionWorldPositions = false;
        isShiftModeButtonHeld = false;
        isCtrlModeButtonHeld = false;
        SetBoxSelectionVisualVisible(false);
        editorGuides?.HideMoveGuides();
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
        distanceIncrement = float.Parse(selected);
    }

    void OnRotateIncrementDropdownChanged(int index)
    {
        string selected = SanitizeSelectedValue(rotateIncrementDropdown.options[index].text);
        rotateIncrement = float.Parse(selected);
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
                SelectObjectsInsideBox(shouldAddToSelection, shouldRemoveFromSelection, IsIndividualSelectionModifierHeld());
            }
            else if (interactionAtRelease == EditorPointerInteraction.WorldSelection &&
                     !pointerInput.HadMultiplePointersDuringCurrentGesture)
            {
                Ray ray = Camera.main.ScreenPointToRay(pointerInput.ScreenPosition);
                RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

                if (hit.collider != null) // object hit
                {
                    ApplySelectionHits(
                        new[] { hit.collider.gameObject },
                        shouldAddToSelection,
                        shouldRemoveFromSelection,
                        IsIndividualSelectionModifierHeld());
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
            else if (IsPersistentGroup(selectionRoot.gameObject))
            {
                // A group can be selected as one root while Alt targets just one
                // of its members. Expand the selection, not the level hierarchy.
                bool removesGroupMember = rootsToRemove.Exists(root => root.IsChildOf(selectionRoot));
                if (!removesGroupMember)
                {
                    remainingSelection.Add(selectionRoot.gameObject);
                    continue;
                }

                removedAnObject = true;
                foreach (Transform member in selectionRoot)
                {
                    if (!IsSelectionRootIncludedIn(rootsToRemove, member))
                        remainingSelection.Add(member.gameObject);
                }
            }
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
            if (candidate == selectionRoot || candidate.IsChildOf(selectionRoot))
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

        List<Transform> groupingMembers = GetGroupingMembers();
        if (groupingMembers.Count < 2)
            return;

        // Multi-selection never changes the level hierarchy. Grouping is the one
        // deliberate hierarchy operation: create a real Group at the selection pivot
        // and reparent level-object roots while preserving their world transforms.
        // Existing selected Groups contribute their children, never the Group itself,
        // so this action cannot create nested Groups.
        GameObject persistentGroup = new GameObject(LevelManager.GroupObjectType);
        persistentGroup.transform.SetParent(levelObjectsCollection.transform, false);
        persistentGroup.transform.SetPositionAndRotation(selectionPivot.transform.position, selectionPivot.transform.rotation);
        persistentGroup.transform.localScale = selectionPivot.transform.localScale;

        foreach (Transform groupingMember in groupingMembers)
        {
            if (groupingMember != null)
                groupingMember.SetParent(persistentGroup.transform, true);
        }

        NormalizePersistentGroups();

        ClearSelectedSelectionRoots();
        DestroySelectionPivot();
        selectedSelectionRoots.Add(persistentGroup.transform);
        SetSelectedObject(persistentGroup, false);

        ConfigureSelectionControlsForSelectedObject();
    }

    bool CanCreatePersistentGroup()
    {
        return HasMultipleSelection && GetGroupingMembers().Count >= 2;
    }

    List<Transform> GetGroupingMembers()
    {
        List<Transform> groupingMembers = new List<Transform>();
        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot == null)
                continue;

            if (IsPersistentGroup(selectionRoot.gameObject))
            {
                // Persistent Groups are never moved into another Group. Their
                // direct level-object children become members of the new Group.
                foreach (Transform child in selectionRoot)
                {
                    if (child != null &&
                        !IsPersistentGroup(child.gameObject) &&
                        !groupingMembers.Contains(child))
                    {
                        groupingMembers.Add(child);
                    }
                }
            }
            else if (!groupingMembers.Contains(selectionRoot))
            {
                groupingMembers.Add(selectionRoot);
            }
        }

        return groupingMembers;
    }

    public void UngroupSelectedPersistentGroup()
    {
        if (activeTransform != ActiveTransform.None || !IsSelectedPersistentGroup ||
            levelObjectsCollection == null)
        {
            return;
        }

        Transform groupTransform = selectedObject.transform;
        List<GameObject> formerMembers = new List<GameObject>(groupTransform.childCount);
        foreach (Transform child in groupTransform)
        {
            if (child == null || IsPersistentGroup(child.gameObject))
                continue;

            formerMembers.Add(child.gameObject);
        }

        foreach (GameObject formerMember in formerMembers)
            formerMember.transform.SetParent(levelObjectsCollection.transform, true);

        DestroyPersistentGroup(groupTransform);
        NormalizePersistentGroups();
        SelectObjects(formerMembers);
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

    // Persistent groups have one purpose: contain two or more direct level-object
    // roots. This is called only after a hierarchy-changing editor action, not per frame.
    void NormalizePersistentGroups()
    {
        if (levelObjectsCollection == null)
            return;

        Transform levelObjectsRoot = levelObjectsCollection.transform;
        List<Transform> persistentGroups = new List<Transform>();
        foreach (Transform child in levelObjectsRoot)
        {
            if (IsPersistentGroup(child.gameObject))
                persistentGroups.Add(child);
        }

        foreach (Transform persistentGroup in persistentGroups)
        {
            if (persistentGroup == null)
                continue;

            if (persistentGroup.childCount == 0)
            {
                DestroyPersistentGroup(persistentGroup);
                continue;
            }

            if (persistentGroup.childCount == 1)
            {
                Transform onlyMember = persistentGroup.GetChild(0);
                onlyMember.SetParent(levelObjectsRoot, true);
                DestroyPersistentGroup(persistentGroup);
            }
        }
    }

    static void DestroyPersistentGroup(Transform persistentGroup)
    {
        if (persistentGroup == null)
            return;

        // Remove it from the level hierarchy immediately; Destroy itself completes
        // at frame end, which would otherwise leave an empty Group visible to save.
        persistentGroup.SetParent(null, true);
        Destroy(persistentGroup.gameObject);
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

        hasGestureGridFrame = false;

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

    bool IsIndividualSelectionModifierHeld()
    {
        return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    }

    // All pointer selection goes through the same target resolution, whether it
    // comes from a click or a box. Stored/restored selections already contain
    // explicit roots and must not be promoted to groups again.
    void ApplySelectionHits(IEnumerable<GameObject> hits, bool addToCurrentSelection, bool removeFromCurrentSelection, bool selectIndividuals)
    {
        List<GameObject> targets = new List<GameObject>();
        foreach (GameObject hit in hits)
        {
            Transform target = GetSelectionTarget(hit != null ? hit.transform : null, selectIndividuals);
            if (target != null && !targets.Contains(target.gameObject))
                targets.Add(target.gameObject);
        }

        if (removeFromCurrentSelection)
            RemoveObjectsFromSelection(targets);
        else if (addToCurrentSelection)
            AddObjectsToSelection(targets);
        else if (targets.Count == 1)
        {
            SelectObject(targets[0]);
            ConfigureSelectionControlsForSelectedObject();
        }
        else
            SelectObjects(targets);
    }

    Transform GetSelectionTarget(Transform hit, bool selectIndividuals)
    {
        if (hit == null)
            return null;

        if (playerStartPoint != null && hit.IsChildOf(playerStartPoint.transform))
            return playerStartPoint.transform;

        if (levelObjectsCollection == null)
            return null;

        Transform levelRoot = levelObjectsCollection.transform;
        if (hit == levelRoot || !hit.IsChildOf(levelRoot))
            return null;

        // Colliders can live below an object's root. Stop at the direct member
        // of a Group, or at the standalone object directly under the level root.
        Transform target = hit;
        while (target.parent != levelRoot && !IsPersistentGroup(target.parent.gameObject))
            target = target.parent;

        if (!selectIndividuals && IsPersistentGroup(target.parent.gameObject))
            target = target.parent;

        return target;
    }

    void SelectObjectsInsideBox(bool addToCurrentSelection, bool removeFromCurrentSelection, bool selectIndividuals)
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

        ApplySelectionHits(objectsToSelect, addToCurrentSelection, removeFromCurrentSelection, selectIndividuals);
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
        selectionOutline?.Clear();
        if (selectionControlsUI != null)
        {
            selectionControlsUI.SetSelectedTransform(null);
            selectionControlsUI.SetVisible(false);
        }

        if (createPersistentGroupButton != null)
            createPersistentGroupButton.SetActive(false);
        if (ungroupPersistentGroupButton != null)
            ungroupPersistentGroupButton.SetActive(false);
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
        selectionOutline?.SetSelection(selectedSelectionRoots);
        selectionContainsCircularObject = false;
        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (ContainsRotationInvariantCircle(selectionRoot))
            {
                selectionContainsCircularObject = true;
                break;
            }
        }
        if (!TryGetSelectionControlAvailability(out SelectionControlAvailability availability))
            return;

        selectionControlsUI.SetControlAvailability(availability.canDuplicate, availability.canRotate,
            availability.canScaleHorizontally, availability.canScaleVertically, availability.canScaleBoth);
    }

    static bool ContainsRotationInvariantCircle(Transform root)
    {
        if (root == null)
            return false;
        if (IsRotationInvariantCircularObject(root.gameObject))
            return true;
        foreach (Transform child in root)
        {
            if (ContainsRotationInvariantCircle(child))
                return true;
        }
        return false;
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
        preserveWorldMoveOrigin = false;
        activeShiftMoveConstraint = ShiftMoveConstraint.None;

        moveIncrementOffset = GetMoveIncrementOffset(selectedObjectPositionAtStartMove);

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

        bool shouldConstrainMove = IsShiftModeHeld &&
            activeMoveControl is ObjectTransformControl.MoveBoth or ObjectTransformControl.Duplicate;
        if (shouldConstrainMove && (!wasShiftModeHeldDuringMove || shiftMoveUsesWorldTransform != IsWorldTransform))
            BeginShiftMoveGuides(pointerWorldPosition);
        else if (!shouldConstrainMove && wasShiftModeHeldDuringMove)
            EndShiftMoveGuides(pointerWorldPosition);

        // A quick click duplicates in place. It starts moving only after the same threshold the
        // legacy world-space control used, so the two control systems feel the same.
        Vector3 desiredPosition;
        if (activeMoveControl == ObjectTransformControl.Duplicate && dragDistancePixels < MINIMUM_DRAG_DISTANCE_PIXELS)
        {
            desiredPosition = selectedObjectPositionAtStartMove;
        }
        else if (shouldConstrainMove)
            desiredPosition = GetShiftConstrainedMovePosition(pointerWorldPosition + moveOffset);
        else
        {
            float newX = RoundToIncrement(pointerWorldPosition.x + moveOffset.x - moveIncrementOffset.x, distanceIncrement) + moveIncrementOffset.x;
            float newY = RoundToIncrement(pointerWorldPosition.y + moveOffset.y - moveIncrementOffset.y, distanceIncrement) + moveIncrementOffset.y;
            desiredPosition = new Vector3(newX, newY, 0f);
            if (preserveWorldMoveOrigin)
            {
                Vector3 intendedPosition = pointerWorldPosition + moveOffset;
                if ((intendedPosition - worldMoveOrigin).sqrMagnitude <= (intendedPosition - desiredPosition).sqrMagnitude)
                    desiredPosition = worldMoveOrigin;
                else
                    preserveWorldMoveOrigin = false;
            }
        }

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

    Vector3 GetShiftConstrainedMovePosition(Vector3 intendedPosition)
    {
        // Use the grab-adjusted position before snapping so grid steps cannot
        // influence which axis the pointer is trying to follow.
        Vector3 candidateOffsetFromGuideOrigin = intendedPosition - shiftMoveGuideOrigin;
        float distanceToRightGuide = Mathf.Abs(Vector3.Dot(candidateOffsetFromGuideOrigin, shiftMoveGuideUp));
        float distanceToUpGuide = Mathf.Abs(Vector3.Dot(candidateOffsetFromGuideOrigin, shiftMoveGuideRight));

        // Keep the current guide when the pointer is exactly between them, avoiding
        // visual flicker while the user crosses the intersection point.
        if (distanceToRightGuide < distanceToUpGuide)
            activeShiftMoveConstraint = ShiftMoveConstraint.AlongRight;
        else if (distanceToUpGuide < distanceToRightGuide)
            activeShiftMoveConstraint = ShiftMoveConstraint.AlongUp;

        Vector3 direction = activeShiftMoveConstraint == ShiftMoveConstraint.AlongRight
            ? shiftMoveGuideRight : shiftMoveGuideUp;
        float distance = Vector3.Dot(candidateOffsetFromGuideOrigin, direction);
        if (shiftMoveUsesWorldTransform && distanceIncrement > 0f)
        {
            if (WorldScaleSnapping.TrySnapAlongLine(shiftMoveGuideOrigin, direction, distance,
                float.NegativeInfinity, float.PositiveInfinity, distanceIncrement, out float snappedDistance))
            {
                // An off-grid starting point remains still until the pointer is
                // closer to a grid crossing, rather than jumping when Shift is pressed.
                if (!shiftMoveHasSnapped && Mathf.Abs(distance) < Mathf.Abs(snappedDistance - distance))
                    distance = 0f;
                else
                {
                    distance = snappedDistance;
                    shiftMoveHasSnapped = true;
                }
            }
        }
        else
            distance = RoundToIncrement(distance, distanceIncrement);

        return shiftMoveGuideOrigin + direction * distance;
    }

    void BeginShiftMoveGuides(Vector3 pointerWorldPosition)
    {
        wasShiftModeHeldDuringMove = true;
        shiftMoveUsesWorldTransform = IsWorldTransform;
        shiftMoveHasSnapped = false;
        preserveWorldMoveOrigin = false;
        shiftMoveGuideOrigin = selectedObject.transform.position;
        moveOffset = shiftMoveGuideOrigin - pointerWorldPosition;

        shiftMoveGuideRight = IsWorldTransform ? Vector3.right : selectedObject.transform.right;
        shiftMoveGuideUp = IsWorldTransform ? Vector3.up : selectedObject.transform.up;
        shiftMoveGuideRight.z = 0f;
        shiftMoveGuideUp.z = 0f;
        shiftMoveGuideRight.Normalize();
        shiftMoveGuideUp.Normalize();
        activeShiftMoveConstraint = ShiftMoveConstraint.AlongRight;

        editorGuides?.ShowMoveGuides(shiftMoveGuideOrigin, shiftMoveGuideRight, shiftMoveGuideUp);
    }

    void EndShiftMoveGuides(Vector3 pointerWorldPosition)
    {
        wasShiftModeHeldDuringMove = false;
        activeShiftMoveConstraint = ShiftMoveConstraint.None;
        editorGuides?.HideMoveGuides();
        Vector3 position = selectedObject.transform.position;
        moveOffset = position - pointerWorldPosition;
        moveIncrementOffset = GetMoveIncrementOffset(position);
        worldMoveOrigin = position;
        Vector3 worldGridPosition = new Vector3(
            RoundToIncrement(position.x, distanceIncrement),
            RoundToIncrement(position.y, distanceIncrement), position.z);
        float tolerance = 0.00001f * Mathf.Max(1f, distanceIncrement);
        preserveWorldMoveOrigin = IsWorldTransform && distanceIncrement > 0f &&
            (position - worldGridPosition).sqrMagnitude > tolerance * tolerance;
    }

    Vector3 GetMoveIncrementOffset(Vector3 position)
    {
        if (IsWorldTransform)
            return Vector3.zero;
        return new Vector3(
            position.x - RoundToIncrement(position.x, distanceIncrement),
            position.y - RoundToIncrement(position.y, distanceIncrement),
            0f);
    }

    void EndMoveSelectedObject(bool deleteMovedObject)
    {
        editorGuides?.HideMoveGuides();
        wasShiftModeHeldDuringMove = false;
        preserveWorldMoveOrigin = false;
        activeShiftMoveConstraint = ShiftMoveConstraint.None;

        NormalizeSelectedRootRotations();

        if (deleteMovedObject && selectedObject != null &&
            pointerIsOverObjectSelectionBar && CanDeleteSelectedRoots())
        {
            DeleteSelectedRoots();
            deselectObjectButton.gameObject.SetActive(false);
        }
        else if (selectedObject != null)
        {
            // A cancelled move must not leave an object hidden merely because the
            // pointer last passed over the delete area.
            SetSelectedRootsActive(true);
        }
    }

    void DeleteSelectedRoots()
    {
        foreach (Transform selectionRoot in selectedSelectionRoots)
        {
            if (selectionRoot == null)
                continue;

            // Detaching first makes a parent Group's child count accurate now,
            // rather than waiting until Destroy runs at the end of the frame.
            selectionRoot.SetParent(null, true);
            Destroy(selectionRoot.gameObject);
        }

        NormalizePersistentGroups();
        UnselectObject();
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

        editorGuides?.SetRotationGuide(selectedObject.transform.position, pointerWorldPosition);

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
        editorGuides?.SetRotationGuide(selectedObject.transform.position, pointerWorldPosition);
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
        editorGuides?.HideRotationGuide();

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
            ShouldScaleCornerIndependently(handle));
        return true;
    }

    // Shared by snapping and its visual highlights so both use the same path
    // and the same size limits, including an off-center Shift scaling pivot.
    static bool TryGetWorldScalePath(ScaleFromEdgeGesture gesture, out WorldScalePath path)
    {
        path = default;
        bool scalesX = DoesScaleFromEdgeHandleScaleX(gesture.handle);
        bool scalesY = DoesScaleFromEdgeHandleScaleY(gesture.handle);
        if (scalesX && scalesY && gesture.scalesIndependently)
            return false;

        ScaleFromEdgeFrame frame = gesture.frameAtStart;
        path.origin = frame.GetHandlePosition(gesture.handle);
        float first;
        float last;
        if (scalesX && scalesY)
        {
            Vector3 factorDirection = gesture.scalesFromBothSides
                ? path.origin - frame.pivot
                : GetScaleFromEdgeHandleXSign(gesture.handle) * frame.right * frame.Width +
                  GetScaleFromEdgeHandleYSign(gesture.handle) * frame.up * frame.Height;
            path.distancePerFactor = factorDirection.magnitude;
            if (path.distancePerFactor <= Mathf.Epsilon)
                return false;
            path.direction = factorDirection / path.distancePerFactor;
            float minimumFactor = Mathf.Max(gesture.minimumXFactor, gesture.minimumYFactor);
            float maximumFactor = Mathf.Max(minimumFactor, Mathf.Min(gesture.maximumXFactor, gesture.maximumYFactor));
            first = (minimumFactor - 1f) * path.distancePerFactor;
            last = (maximumFactor - 1f) * path.distancePerFactor;
        }
        else
        {
            float sign = scalesX ? GetScaleFromEdgeHandleXSign(gesture.handle) : GetScaleFromEdgeHandleYSign(gesture.handle);
            path.direction = sign * (scalesX ? frame.right : frame.up);
            float edge = scalesX ? (sign > 0f ? frame.maxX : frame.minX) : (sign > 0f ? frame.maxY : frame.minY);
            path.distancePerFactor = gesture.scalesFromBothSides ? sign * edge : (scalesX ? frame.Width : frame.Height);
            float minimumFactor = scalesX ? gesture.minimumXFactor : gesture.minimumYFactor;
            float maximumFactor = scalesX ? gesture.maximumXFactor : gesture.maximumYFactor;
            first = (minimumFactor - 1f) * path.distancePerFactor;
            last = (Mathf.Max(minimumFactor, maximumFactor) - 1f) * path.distancePerFactor;
        }

        path.minimum = Mathf.Min(first, last);
        path.maximum = Mathf.Max(first, last);
        return true;
    }

    void UpdateScaleFromEdgeSelectedObject(Vector3 pointerWorldPosition)
    {
        ScaleFromEdgeGesture gesture = activeScaleFromEdgeGesture;
        bool shouldScaleFromBothSides = IsScaleFromEdgeBothSidesModeHeld();
        bool shouldScaleIndependently = ShouldScaleCornerIndependently(gesture.handle);
        bool snapToWorld = IsWorldTransform;
        if (gesture.scalesFromBothSides != shouldScaleFromBothSides ||
            gesture.scalesIndependently != shouldScaleIndependently ||
            gesture.distanceIncrement != distanceIncrement || gesture.snapToWorld != snapToWorld)
        {
            // Rebase at the current size and pointer when the anchor or snapping
            // mode changes. World snapping can then align the handle to the grid.
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
        bool snapWorld = gesture.snapToWorld && gesture.distanceIncrement > 0f;
        Vector3 startHandle = gesture.frameAtStart.GetHandlePosition(gesture.handle);

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
            float minimumFactor = Mathf.Max(gesture.minimumXFactor, gesture.minimumYFactor);
            float maximumFactor = Mathf.Max(minimumFactor, Mathf.Min(gesture.maximumXFactor, gesture.maximumYFactor));
            float outwardDistance = Vector3.Dot(pointerDelta, movingCornerDirection);
            if (snapWorld)
            {
                if (!TryGetWorldScalePath(gesture, out WorldScalePath path))
                    return;
                outwardDistance = Vector3.Dot(pointerDelta, path.direction);
                if (!WorldScaleSnapping.TrySnapAlongLine(path.origin, path.direction, outwardDistance,
                    path.minimum, path.maximum, gesture.distanceIncrement, out outwardDistance))
                    return;
                xFactor = yFactor = Mathf.Clamp(1f + outwardDistance / path.distancePerFactor, minimumFactor, maximumFactor);
            }
            else
            {
                outwardDistance = RoundToIncrement(outwardDistance, gesture.distanceIncrement);
                xFactor = yFactor = GetScaleFromEdgeGestureFactor(
                    gesture.scalesFromBothSides, diagonalExtent, outwardDistance, minimumFactor, maximumFactor);
            }
        }
        else
        {
            Vector3 xDirection = GetScaleFromEdgeHandleXSign(gesture.handle) * gesture.frameAtStart.right;
            Vector3 yDirection = GetScaleFromEdgeHandleYSign(gesture.handle) * gesture.frameAtStart.up;
            float xDistance = Vector3.Dot(pointerDelta, xDirection);
            float yDistance = Vector3.Dot(pointerDelta, yDirection);
            float xSign = GetScaleFromEdgeHandleXSign(gesture.handle);
            float ySign = GetScaleFromEdgeHandleYSign(gesture.handle);
            float xPerFactor = gesture.scalesFromBothSides
                ? xSign * (xSign > 0f ? gesture.frameAtStart.maxX : gesture.frameAtStart.minX)
                : gesture.frameAtStart.Width;
            float yPerFactor = gesture.scalesFromBothSides
                ? ySign * (ySign > 0f ? gesture.frameAtStart.maxY : gesture.frameAtStart.minY)
                : gesture.frameAtStart.Height;
            float xFirst = (gesture.minimumXFactor - 1f) * xPerFactor;
            float xLast = (Mathf.Max(gesture.minimumXFactor, gesture.maximumXFactor) - 1f) * xPerFactor;
            float yFirst = (gesture.minimumYFactor - 1f) * yPerFactor;
            float yLast = (Mathf.Max(gesture.minimumYFactor, gesture.maximumYFactor) - 1f) * yPerFactor;
            Vector2 minimumDistance = new Vector2(
                Mathf.Min(xFirst, xLast), Mathf.Min(yFirst, yLast));
            Vector2 maximumDistance = new Vector2(
                Mathf.Max(xFirst, xLast), Mathf.Max(yFirst, yLast));

            if (snapWorld && scalesX && scalesY)
            {
                if (!WorldScaleSnapping.TrySnapCorner(startHandle, xDirection, yDirection,
                    new Vector2(xDistance, yDistance), minimumDistance, maximumDistance,
                    gesture.distanceIncrement, out Vector2 snappedDistance))
                    return;
                xDistance = snappedDistance.x;
                yDistance = snappedDistance.y;
            }
            else if (snapWorld && scalesX)
            {
                if (!TryGetWorldScalePath(gesture, out WorldScalePath path) ||
                    !WorldScaleSnapping.TrySnapAlongLine(path.origin, path.direction, xDistance,
                        path.minimum, path.maximum, gesture.distanceIncrement, out xDistance))
                    return;
            }
            else if (snapWorld && scalesY)
            {
                if (!TryGetWorldScalePath(gesture, out WorldScalePath path) ||
                    !WorldScaleSnapping.TrySnapAlongLine(path.origin, path.direction, yDistance,
                        path.minimum, path.maximum, gesture.distanceIncrement, out yDistance))
                    return;
            }
            else
            {
                xDistance = RoundToIncrement(xDistance, gesture.distanceIncrement);
                yDistance = RoundToIncrement(yDistance, gesture.distanceIncrement);
            }

            if (scalesX)
            {
                xFactor = snapWorld
                    ? GetWorldSnappedScaleFactor(xDistance, xPerFactor, gesture.minimumXFactor, gesture.maximumXFactor)
                    : GetScaleFromEdgeGestureFactor(gesture.scalesFromBothSides, gesture.frameAtStart.Width, xDistance, gesture.minimumXFactor, gesture.maximumXFactor);
            }

            if (scalesY)
            {
                yFactor = snapWorld
                    ? GetWorldSnappedScaleFactor(yDistance, yPerFactor, gesture.minimumYFactor, gesture.maximumYFactor)
                    : GetScaleFromEdgeGestureFactor(gesture.scalesFromBothSides, gesture.frameAtStart.Height, yDistance, gesture.minimumYFactor, gesture.maximumYFactor);
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
            snapToWorld = IsWorldTransform,
            distanceIncrement = distanceIncrement,
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

    bool ShouldScaleCornerIndependently(ScaleFromEdgeHandle handle)
    {
        return !IsCtrlModeHeld &&
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

    static float GetWorldSnappedScaleFactor(float distance, float distancePerFactor, float minimumFactor, float maximumFactor)
    {
        if (Mathf.Abs(distancePerFactor) <= Mathf.Epsilon)
            return 1f;
        return Mathf.Clamp(1f + distance / distancePerFactor, minimumFactor, Mathf.Max(minimumFactor, maximumFactor));
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

        currentScaleFrame = default;
        hasCurrentScaleFrame = selectedObject != null && TryGetScaleFromEdgeFrame(out currentScaleFrame);
        selectionControlsUI.SetSelectionFrame(hasCurrentScaleFrame, currentScaleFrame);
        if (TryGetSelectionControlAvailability(out SelectionControlAvailability availability))
        {
            selectionControlsUI.SetControlAvailability(availability.canDuplicate, availability.canRotate,
                availability.canScaleHorizontally && hasCurrentScaleFrame,
                availability.canScaleVertically && hasCurrentScaleFrame,
                availability.canScaleBoth && hasCurrentScaleFrame);
        }

        deselectObjectButton.SetActive(show);
        snapVerticalButton.SetActive(show);
        snapHorizontalButton.SetActive(show);
        if (createPersistentGroupButton != null)
            createPersistentGroupButton.SetActive(show && CanCreatePersistentGroup());
        if (ungroupPersistentGroupButton != null)
            ungroupPersistentGroupButton.SetActive(show && IsSelectedPersistentGroup);
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
    public void PlaceMomentumRedirector()
    {
        prefabToPlace = LevelManager.Instance.MomentumRedirectorPrefab;
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
