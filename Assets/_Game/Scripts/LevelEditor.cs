using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelEditor : MonoBehaviour
{
    public const string TemporarySelectionGroupName = "SelectionGroup";
    public const string DuplicatingSelectionGroupName = "DuplicatingSelectionGroup";

    // self object references
    [SerializeField] Rigidbody2D rb;
    [SerializeField] GameObject prefabToPlace;
    [SerializeField] GameObject startLocationIcon;
    [SerializeField] GameObject localTransformButton;
    [SerializeField] GameObject worldTransformButton;
    [SerializeField] GameObject deselectObjectButton;
    [SerializeField] GameObject snapVerticalButton;
    [SerializeField] GameObject snapHorizontalButton;
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
    [SerializeField] SelectionControlsUI selectionControlsUI;
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
    bool isWorldTransform = true;

    // object selection
    const float MINIMUM_DRAG_DISTANCE_PIXELS = 15f;
    bool hasSelectionDragExceededThreshold = false;
    GameObject selectionGroup;
    readonly List<TemporarySelectionMember> temporarySelectionMembers = new List<TemporarySelectionMember>();
    readonly List<GameObject> suspendedSelectionObjects = new List<GameObject>();
    readonly List<GameObject> selectionObjectsBeforeLeavingEditor = new List<GameObject>();
    bool isSelectionSuspendedForSave;
    int levelRevisionWhenEditorWasLeft = -1;
    bool hasRememberedEditorState;
    Canvas boxSelectionCanvas;
    RectTransform boxSelectionVisualParent;

    struct TemporarySelectionMember
    {
        public GameObject gameObject;
        public Transform originalParent;
        public int originalSiblingIndex;
    }

    struct SelectionControlAvailability
    {
        public bool canDuplicate;
        public bool canScaleBoth;
        public bool canScaleX;
        public bool canScaleY;
        public bool canRotate;
        public bool scaleHandlesFollowSelectionRotation;

        public static SelectionControlAvailability ForObject(GameObject levelObject)
        {
            bool isPlayerStartPoint = levelObject.name == "PlayerStartPoint";
            bool isPuller = levelObject.name.Contains("Puller");
            bool isKillCircle = levelObject.name.Contains("KillCircle");
            bool supportsIndependentScaleAndRotation = !isPlayerStartPoint && !isPuller && !isKillCircle;

            return new SelectionControlAvailability
            {
                canDuplicate = !isPlayerStartPoint,
                canScaleBoth = !isPlayerStartPoint,
                canScaleX = supportsIndependentScaleAndRotation,
                canScaleY = supportsIndependentScaleAndRotation,
                canRotate = supportsIndependentScaleAndRotation,
                scaleHandlesFollowSelectionRotation = supportsIndependentScaleAndRotation
            };
        }

        public void IntersectWith(SelectionControlAvailability other)
        {
            canDuplicate &= other.canDuplicate;
            canScaleBoth &= other.canScaleBoth;
            canScaleX &= other.canScaleX;
            canScaleY &= other.canScaleY;
            canRotate &= other.canRotate;
            scaleHandlesFollowSelectionRotation &= other.scaleHandlesFollowSelectionRotation;
        }
    }

    struct ScaleGesture
    {
        public Vector3 pointerWorldPositionAtStart;
        public Vector3 selectedLocalScaleAtStart;
        public float xExtentAtStart;
        public float yExtentAtStart;
        public float uniformExtentAtStart;
        public Vector3 xOutwardDirection;
        public Vector3 yOutwardDirection;
        public Vector3 uniformOutwardDirection;
        public float minimumXFactor;
        public float minimumYFactor;
        public float maximumXFactor;
        public float maximumYFactor;
    }

    // object movement
    Vector3 moveOffset;
    bool isTryingToMoveSelectedObject = false;
    Vector3 selectedObjectPositionAtStartMove;
    Vector3 pointerPositionAtStartMove;
    float moveIncrement = 0f;
    Vector3 moveIncrementOffset = new Vector3(0f, 0f, 0f);

    // object rotation
    bool isTryingToRotateSelectedObject = false;
    float selectedObjectRotationAtStartRotate;
    float angleToPointerAtStartRotate;
    float rotateIncrement = 0f;
    float rotationIncrementOffset = 0f;

    // object scaling
    bool isTryingToScaleSelectedObject = false;
    float maximumScale = 999999f;
    float scaleIncrement = 0f;
    ScaleGesture activeScaleGesture;

    ObjectTransformControl activeMoveControl;
    ObjectTransformControl activeScaleControl;
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
        worldTransformButton.SetActive(true);
        localTransformButton.SetActive(false);
        snapVerticalButton.SetActive(false);
        snapHorizontalButton.SetActive(false);
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
    }

    private void OnEnable()
    {
        EventManager.Instance.UnselectObjectEvent.AddListener(UnselectObject);
    }

    private void OnDisable()
    {
        SetBoxSelectionVisualVisible(false);
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
            selectedObject.transform.position = new Vector3(lastSelectedObject.transform.position.x, selectedObject.transform.position.y, 0f);
        }
    }

    public void SnapSelectedObjectToLastVertical()
    {
        if (selectedObject != null && lastSelectedObject != null && selectedObject != lastSelectedObject)
        {
            selectedObject.transform.position = new Vector3(selectedObject.transform.position.x, lastSelectedObject.transform.position.y, 0f);
        }
    }

    #endregion

    void UpdateBoxSelectIntentFromPointerDrag()
    {
        PointerInput pointerInput = PointerInput.Instance;
        if (pointerInput.WasPressedThisFrame)
        {
            hasSelectionDragExceededThreshold = false;
            SetBoxSelectionVisualVisible(false);
        }

        if (isTryingToPlace ||
            pointerInput.CurrentGestureStartedOverUi ||
            pointerInput.HadMultiplePointersDuringCurrentGesture ||
            !pointerInput.IsHeld ||
            PointerInput.Instance.WasCanceledThisFrame)
        {
            SetBoxSelectionVisualVisible(false);
            return;
        }

        if (!hasSelectionDragExceededThreshold)
        {
            float dragDistanceInPixels = pointerInput.DragDistancePixels;
            if (dragDistanceInPixels >= MINIMUM_DRAG_DISTANCE_PIXELS)
            {
                hasSelectionDragExceededThreshold = true;
                Debug.Log("LevelEditor: drag threshold crossed, box-select mode latched for this pointer cycle.");
            }
        }

        if (hasSelectionDragExceededThreshold)
            UpdateBoxSelectionVisual(pointerInput.PressStartScreenPosition, pointerInput.ScreenPosition);
    }

    void HandlePlacePrefab()
    {
        if (objectCurrentlyTryingToPlace != null && isTryingToPlace)
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
        if (PointerInput.Instance.WasReleasedThisFrame)
        {
            bool shouldDoBoxSelect = hasSelectionDragExceededThreshold;
            hasSelectionDragExceededThreshold = false;
            SetBoxSelectionVisualVisible(false);

            if (PointerInput.Instance.WasCanceledThisFrame)
                return;

            if (PointerInput.Instance.CurrentGestureStartedOverUi)
                return;

            // A world-origin gesture stays owned by the editor even when its release
            // position overlaps UI, such as a box selection ending over a HUD control.
            bool shouldRemoveFromSelection = IsRemoveSelectionModifierHeld();
            bool shouldAddToSelection = !shouldRemoveFromSelection && IsAddSelectionModifierHeld();
            if (shouldDoBoxSelect && !PointerInput.Instance.HadMultiplePointersDuringCurrentGesture)
            {
                SelectObjectsInsideBox(shouldAddToSelection, shouldRemoveFromSelection);
            }
            // A box selection owns its whole drag even if it ends over UI. A normal click
            // released over an interactive UI control belongs to that control, not the world.
            else if (!PointerInput.Instance.WasReleasedOverSelectableUi)
            {
                Debug.Log("LevelEditor: pointer cycle resolved as click-select.");
                Ray ray = Camera.main.ScreenPointToRay(PointerInput.Instance.ScreenPosition);
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
        }
    }

    void SelectObject(GameObject objectToSelect, bool rememberPreviousSelection = true)
    {
        if (selectionGroup != null && objectToSelect != selectionGroup)
            UnselectObject();

        SetSelectedObject(objectToSelect, rememberPreviousSelection);

        if (selectionControlsUI != null)
            selectionControlsUI.SetSelectedTransform(selectedObject.transform);
    }

    // Box selection will use this entry point once it can collect its matching objects. A
    // temporary Group is an editor helper only: it is dissolved before saving or deselecting.
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

        CreateTemporarySelectionGroup(selectionRoots);
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
        if (selectionGroup != null)
        {
            foreach (TemporarySelectionMember member in temporarySelectionMembers)
            {
                if (member.gameObject != null)
                    combinedSelection.Add(member.gameObject);
            }
        }
        else if (selectedObject != null)
        {
            combinedSelection.Add(selectedObject);
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

        if (selectionGroup != null)
        {
            foreach (TemporarySelectionMember member in temporarySelectionMembers)
            {
                if (member.gameObject == null)
                    continue;

                if (IsSelectionRootIncludedIn(rootsToRemove, member.gameObject.transform))
                    removedAnObject = true;
                else
                    remainingSelection.Add(member.gameObject);
            }
        }
        else if (IsSelectionRootIncludedIn(rootsToRemove, selectedObject.transform))
        {
            removedAnObject = true;
        }
        else
        {
            remainingSelection.Add(selectedObject);
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

    void CreateTemporarySelectionGroup(List<Transform> selectionRoots)
    {
        selectionGroup = new GameObject(TemporarySelectionGroupName);
        selectionGroup.transform.SetParent(levelObjectsCollection.transform, false);
        selectionGroup.transform.position = GetSelectionPivot(selectionRoots);
        selectionGroup.transform.rotation = Quaternion.identity;
        selectionGroup.transform.localScale = Vector3.one;

        temporarySelectionMembers.Clear();
        foreach (Transform selectionRoot in selectionRoots)
        {
            temporarySelectionMembers.Add(new TemporarySelectionMember
            {
                gameObject = selectionRoot.gameObject,
                originalParent = selectionRoot.parent,
                originalSiblingIndex = selectionRoot.GetSiblingIndex()
            });
            selectionRoot.SetParent(selectionGroup.transform, true);
        }

        SelectObject(selectionGroup, false);
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
        // A save may have temporarily flattened a selection group. Its snapshot belongs
        // to SuspendSelectionForSave/RestoreSelectionAfterSave, not normal deselection.
        bool wasTemporarySelectionGroup = selectionGroup != null;
        if (wasTemporarySelectionGroup)
            DissolveTemporarySelectionGroup();

        SetSelectedObject(null, !wasTemporarySelectionGroup);

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

    void DissolveTemporarySelectionGroup()
    {
        temporarySelectionMembers.Sort((first, second) =>
        {
            int firstParentId = first.originalParent != null ? first.originalParent.GetInstanceID() : int.MinValue;
            int secondParentId = second.originalParent != null ? second.originalParent.GetInstanceID() : int.MinValue;
            int parentComparison = firstParentId.CompareTo(secondParentId);
            if (parentComparison != 0)
                return parentComparison;

            return first.originalSiblingIndex.CompareTo(second.originalSiblingIndex);
        });

        foreach (TemporarySelectionMember member in temporarySelectionMembers)
        {
            if (member.gameObject == null)
                continue;

            Transform parentToRestore = member.originalParent != null
                ? member.originalParent
                : levelObjectsCollection.transform;
            member.gameObject.transform.SetParent(parentToRestore, true);

            if (member.originalParent != null)
            {
                int siblingIndex = Mathf.Min(member.originalSiblingIndex, parentToRestore.childCount - 1);
                member.gameObject.transform.SetSiblingIndex(siblingIndex);
            }
        }

        temporarySelectionMembers.Clear();

        if (selectionGroup != null)
        {
            selectionGroup.transform.SetParent(null);
            Destroy(selectionGroup);
            selectionGroup = null;
        }
    }

    bool IsAddSelectionModifierHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    bool IsRemoveSelectionModifierHeld()
    {
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }

    void SelectObjectsInsideBox(bool addToCurrentSelection, bool removeFromCurrentSelection)
    {
        PointerInput pointerInput = PointerInput.Instance;
        if (!pointerInput.TryGetWorldPositionNoDepth(pointerInput.PressStartScreenPosition, out Vector3 pressStartWorldPosition) ||
            !pointerInput.TryGetWorldPositionNoDepth(pointerInput.ScreenPosition, out Vector3 releaseWorldPosition))
        {
            Debug.LogError("LevelEditor could not box-select objects because the pointer positions could not be converted to world positions.", this);
            return;
        }

        Collider2D[] collidersInBox = Physics2D.OverlapAreaAll(pressStartWorldPosition, releaseWorldPosition);
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
        if (selectionControlsUI == null)
            return;

        selectionControlsUI.SetSelectedTransform(null);
        selectionControlsUI.SetVisible(false);
    }

    void SuspendSelectionForSave()
    {
        if (isSelectionSuspendedForSave)
            return;

        suspendedSelectionObjects.Clear();
        if (selectionGroup != null)
        {
            foreach (TemporarySelectionMember member in temporarySelectionMembers)
            {
                if (member.gameObject != null)
                    suspendedSelectionObjects.Add(member.gameObject);
            }

            DissolveTemporarySelectionGroup();
        }
        else if (selectedObject != null)
        {
            suspendedSelectionObjects.Add(selectedObject);
        }

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

    // UIManager calls this before it disables the editor. The temporary wrapper itself
    // is not kept; only its selected roots are remembered, so OnDisable can safely
    // dissolve the wrapper as usual.
    public void RememberStateBeforeLeavingEditor()
    {
        CancelSuspendedSelectionRestore();
        selectionObjectsBeforeLeavingEditor.Clear();

        if (selectionGroup != null)
        {
            foreach (TemporarySelectionMember member in temporarySelectionMembers)
            {
                if (member.gameObject != null)
                    selectionObjectsBeforeLeavingEditor.Add(member.gameObject);
            }
        }
        else if (selectedObject != null)
        {
            selectionObjectsBeforeLeavingEditor.Add(selectedObject);
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
        if (selectedObject == null)
            return;

        bool hasSelectionMember = false;
        SelectionControlAvailability availability = default;
        if (selectionGroup != null)
        {
            foreach (TemporarySelectionMember member in temporarySelectionMembers)
            {
                if (member.gameObject == null)
                    continue;

                SelectionControlAvailability memberAvailability = SelectionControlAvailability.ForObject(member.gameObject);
                if (hasSelectionMember)
                    availability.IntersectWith(memberAvailability);
                else
                {
                    availability = memberAvailability;
                    hasSelectionMember = true;
                }
            }
        }
        else
        {
            availability = SelectionControlAvailability.ForObject(selectedObject);
            hasSelectionMember = true;
        }

        if (!hasSelectionMember)
            return;

        // Rotation here affects the temporary selection wrapper, not one member's
        // individual transform. That remains valid for circles and other roots that
        // intentionally hide their individual rotate control.
        if (selectionGroup != null)
        {
            availability.canRotate = true;
            availability.scaleHandlesFollowSelectionRotation = true;
        }

        selectionControlsUI.SetControlAvailability(
            availability.canDuplicate,
            availability.canScaleBoth,
            availability.canScaleX,
            availability.canScaleY,
            availability.canRotate,
            availability.scaleHandlesFollowSelectionRotation);
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
        if (selectedObject == null || isTryingToMoveSelectedObject || isTryingToRotateSelectedObject || isTryingToScaleSelectedObject)
            return;

        switch (control)
        {
            case ObjectTransformControl.MoveBoth:
            case ObjectTransformControl.MoveX:
            case ObjectTransformControl.MoveY:
            case ObjectTransformControl.Duplicate:
                BeginMoveSelectedObject(control, screenPosition);
                break;
            case ObjectTransformControl.Rotate:
                BeginRotateSelectedObject(screenPosition);
                break;
            case ObjectTransformControl.ScaleBoth:
            case ObjectTransformControl.ScaleX:
            case ObjectTransformControl.ScaleY:
                BeginScaleSelectedObject(control, screenPosition);
                break;
        }

        if (isTryingToMoveSelectedObject || isTryingToRotateSelectedObject || isTryingToScaleSelectedObject)
        {
            activeTransformPointerId = pointerId;
            activeTransformPressScreenPosition = screenPosition;
        }
    }

    public void EndObjectTransformControl(ObjectTransformControl control, int pointerId)
    {
        if (pointerId != activeTransformPointerId)
            return;

        if (control == activeMoveControl && isTryingToMoveSelectedObject)
            EndMoveSelectedObject();
        else if (control == ObjectTransformControl.Rotate && isTryingToRotateSelectedObject)
            EndRotateSelectedObject();
        else if (control == activeScaleControl && isTryingToScaleSelectedObject)
            EndScaleSelectedObject();

        activeTransformPointerId = int.MinValue;
    }

    void UpdateActiveScreenSpaceTransformControl()
    {
        PointerInput pointerInput = PointerInput.Instance;
        if (pointerInput == null || activeTransformPointerId == int.MinValue ||
            !pointerInput.TryGetScreenPosition(activeTransformPointerId, out Vector2 pointerScreenPosition) ||
            !pointerInput.TryGetWorldPositionNoDepth(pointerScreenPosition, out Vector3 pointerWorldPosition))
            return;

        if (isTryingToMoveSelectedObject)
            UpdateMoveSelectedObject(pointerWorldPosition, Vector2.Distance(activeTransformPressScreenPosition, pointerScreenPosition));
        else if (isTryingToRotateSelectedObject)
            UpdateRotateSelectedObject(pointerWorldPosition);
        else if (isTryingToScaleSelectedObject)
            UpdateScaleSelectedObject(pointerWorldPosition);
    }

    void BeginMoveSelectedObject(ObjectTransformControl control, Vector2 screenPosition)
    {
        if (!PointerInput.Instance.TryGetWorldPositionNoDepth(screenPosition, out pointerPositionAtStartMove))
        {
            Debug.LogError("LevelEditor could not begin moving an object because a valid pointer world position is unavailable.", this);
            return;
        }

        if (control == ObjectTransformControl.Duplicate)
        {
            if (selectedObject == selectionGroup)
                DuplicateTemporarySelectionGroup();
            else
            {
                SelectObject(Instantiate(selectedObject, levelObjectsCollection.transform));
                selectedObject.transform.name = selectedObject.transform.name.Replace("(Clone)", "");
            }

            ConfigureSelectionControlsForSelectedObject();
        }

        isTryingToMoveSelectedObject = true;
        activeMoveControl = control;
        selectedObjectPositionAtStartMove = selectedObject.transform.position;

        if (isWorldTransform)
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
    }

    void DuplicateTemporarySelectionGroup()
    {
        GameObject originalSelectionGroup = selectionGroup;
        GameObject duplicatedSelectionGroup = Instantiate(originalSelectionGroup, levelObjectsCollection.transform);
        duplicatedSelectionGroup.name = DuplicatingSelectionGroupName;

        // The original objects become normal level objects again. The clone takes over
        // as the temporary selection group that the duplicate drag will move.
        DissolveTemporarySelectionGroup();

        selectionGroup = duplicatedSelectionGroup;
        temporarySelectionMembers.Clear();

        int firstSiblingIndex = duplicatedSelectionGroup.transform.GetSiblingIndex();
        int childIndex = 0;
        foreach (Transform duplicatedChild in duplicatedSelectionGroup.transform)
        {
            temporarySelectionMembers.Add(new TemporarySelectionMember
            {
                gameObject = duplicatedChild.gameObject,
                originalParent = levelObjectsCollection.transform,
                originalSiblingIndex = firstSiblingIndex + childIndex
            });
            childIndex++;
        }

        duplicatedSelectionGroup.name = TemporarySelectionGroupName;
        SetSelectedObject(duplicatedSelectionGroup, false);
    }

    void UpdateMoveSelectedObject(Vector3 pointerWorldPosition, float dragDistancePixels)
    {
        if (selectedObject == null)
            return;

        // A quick click duplicates in place. It starts moving only after the same threshold the
        // legacy world-space control used, so the two control systems feel the same.
        if (activeMoveControl == ObjectTransformControl.Duplicate && dragDistancePixels < MINIMUM_DRAG_DISTANCE_PIXELS)
        {
            selectedObject.transform.position = selectedObjectPositionAtStartMove;
        }
        else
        {
            float newX = RoundToIncrement(pointerWorldPosition.x + moveOffset.x, moveIncrement) + moveIncrementOffset.x;
            float newY = RoundToIncrement(pointerWorldPosition.y + moveOffset.y, moveIncrement) + moveIncrementOffset.y;
            selectedObject.transform.position = new Vector3(newX, newY, 0f);
        }

        bool canDeleteSelectedObject = !selectedObject.name.Equals("PlayerStartPoint");
        if (pointerIsOverObjectSelectionBar && canDeleteSelectedObject)
            selectedObject.SetActive(false);
        else
            selectedObject.SetActive(true);

        if (activeMoveControl == ObjectTransformControl.MoveX)
        {
            float newX = RoundToIncrement(selectedObject.transform.position.x, moveIncrement) + moveIncrementOffset.x;
            selectedObject.transform.position = new Vector3(newX, selectedObjectPositionAtStartMove.y, 0f);

            horizontalLine.gameObject.SetActive(true);
            horizontalLine.transform.position = selectedObject.transform.position;
            horizontalLine.SetPosition(0, new Vector3(horizontalLine.transform.position.x + 9999f, horizontalLine.transform.position.y, 0f));
            horizontalLine.SetPosition(1, new Vector3(horizontalLine.transform.position.x - 9999f, horizontalLine.transform.position.y, 0f));
        }

        if (activeMoveControl == ObjectTransformControl.MoveY)
        {
            float newY = RoundToIncrement(selectedObject.transform.position.y, moveIncrement) + moveIncrementOffset.y;
            selectedObject.transform.position = new Vector3(selectedObjectPositionAtStartMove.x, newY, 0f);

            verticalLine.gameObject.SetActive(true);
            verticalLine.transform.position = selectedObject.transform.position;
            verticalLine.SetPosition(0, new Vector3(verticalLine.transform.position.x, verticalLine.transform.position.y + 9999f, 0f));
            verticalLine.SetPosition(1, new Vector3(verticalLine.transform.position.x, verticalLine.transform.position.y - 9999f, 0f));
        }
    }

    void EndMoveSelectedObject()
    {
        verticalLine.gameObject.SetActive(false);
        horizontalLine.gameObject.SetActive(false);

        isTryingToMoveSelectedObject = false;

        if (selectedObject != null &&
            pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint"))
        {
            if (selectedObject == selectionGroup)
                DeleteTemporarySelectionGroup();
            else
                Destroy(selectedObject);

            UnselectObject();
            deselectObjectButton.gameObject.SetActive(false);
        }
    }

    void DeleteTemporarySelectionGroup()
    {
        temporarySelectionMembers.Clear();

        GameObject temporaryGroupToDelete = selectionGroup;
        selectionGroup = null;
        Destroy(temporaryGroupToDelete);
    }

    void BeginRotateSelectedObject(Vector2 screenPosition)
    {
        if (!PointerInput.Instance.TryGetWorldPositionNoDepth(screenPosition, out Vector3 pointerWorldPosition))
        {
            Debug.LogError("LevelEditor could not begin rotating an object because a valid pointer world position is unavailable.", this);
            return;
        }

        rotationLine.gameObject.SetActive(true);
        rotationLine.SetPosition(0, selectedObject.transform.position);

        isTryingToRotateSelectedObject = true;
        selectedObjectRotationAtStartRotate = selectedObject.transform.localEulerAngles.z;
        Vector3 direction = pointerWorldPosition - selectedObject.transform.position;
        angleToPointerAtStartRotate = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        rotationIncrementOffset = isWorldTransform
            ? 0f
            : selectedObjectRotationAtStartRotate - RoundToIncrement(selectedObjectRotationAtStartRotate, rotateIncrement);
    }

    void UpdateRotateSelectedObject(Vector3 pointerWorldPosition)
    {
        if (selectedObject == null)
            return;

        Vector3 direction = pointerWorldPosition - selectedObject.transform.position;
        float currentAngleToPointer = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float deltaAngle = currentAngleToPointer - angleToPointerAtStartRotate;
        float newRotation = RoundToIncrement(selectedObjectRotationAtStartRotate + deltaAngle, rotateIncrement);

        if (!isWorldTransform)
            newRotation += rotationIncrementOffset;

        selectedObject.transform.localRotation = Quaternion.Euler(0f, 0f, newRotation);
        rotationLine.SetPosition(1, pointerWorldPosition);
    }

    void EndRotateSelectedObject()
    {
        rotationLine.gameObject.SetActive(false);
        isTryingToRotateSelectedObject = false;
    }

    void BeginScaleSelectedObject(ObjectTransformControl control, Vector2 screenPosition)
    {
        if (!PointerInput.Instance.TryGetWorldPositionNoDepth(screenPosition, out Vector3 pointerWorldPositionAtStart))
        {
            Debug.LogError("LevelEditor could not begin scaling an object because a valid pointer world position is unavailable.", this);
            return;
        }

        Camera activeCamera = Camera.main;
        if (activeCamera == null || !TryGetSelectedScaleExtents(out float xExtent, out float yExtent))
        {
            Debug.LogError("LevelEditor could not begin scaling because the selected object has no measurable world bounds or active world camera.", this);
            return;
        }

        activeScaleGesture = new ScaleGesture
        {
            pointerWorldPositionAtStart = pointerWorldPositionAtStart,
            selectedLocalScaleAtStart = selectedObject.transform.localScale,
            xExtentAtStart = xExtent,
            yExtentAtStart = yExtent,
            uniformExtentAtStart = Mathf.Max(xExtent, yExtent),
            xOutwardDirection = GetCameraFacingDirection(selectedObject.transform.right, activeCamera.transform.right),
            yOutwardDirection = GetCameraFacingDirection(selectedObject.transform.up, activeCamera.transform.up),
            uniformOutwardDirection = -activeCamera.transform.right
        };
        GetScaleFactorLimits(out activeScaleGesture.minimumXFactor,
                             out activeScaleGesture.maximumXFactor,
                             out activeScaleGesture.minimumYFactor,
                             out activeScaleGesture.maximumYFactor);

        isTryingToScaleSelectedObject = true;
        activeScaleControl = control;
    }

    void UpdateScaleSelectedObject(Vector3 pointerWorldPosition)
    {
        if (selectedObject == null)
            return;

        Vector3 pointerDelta = pointerWorldPosition - activeScaleGesture.pointerWorldPositionAtStart;
        Vector3 newScale = activeScaleGesture.selectedLocalScaleAtStart;

        switch (activeScaleControl)
        {
            case ObjectTransformControl.ScaleBoth:
                float uniformScaleFactor = GetScaleFactor(
                    activeScaleGesture.uniformExtentAtStart,
                    Vector3.Dot(pointerDelta, activeScaleGesture.uniformOutwardDirection),
                    Mathf.Max(activeScaleGesture.minimumXFactor, activeScaleGesture.minimumYFactor),
                    Mathf.Min(activeScaleGesture.maximumXFactor, activeScaleGesture.maximumYFactor));
                newScale.x *= uniformScaleFactor;
                newScale.y *= uniformScaleFactor;
                break;

            case ObjectTransformControl.ScaleX:
                horizontalLine.gameObject.SetActive(true);
                horizontalLine.SetPosition(0, selectedObject.transform.position + selectedObject.transform.right * 9999f);
                horizontalLine.SetPosition(1, selectedObject.transform.position - selectedObject.transform.right * 9999f);
                newScale.x *= GetScaleFactor(
                    activeScaleGesture.xExtentAtStart,
                    Vector3.Dot(pointerDelta, activeScaleGesture.xOutwardDirection),
                    activeScaleGesture.minimumXFactor,
                    activeScaleGesture.maximumXFactor);
                break;

            case ObjectTransformControl.ScaleY:
                verticalLine.gameObject.SetActive(true);
                verticalLine.SetPosition(0, selectedObject.transform.position + selectedObject.transform.up * 9999f);
                verticalLine.SetPosition(1, selectedObject.transform.position - selectedObject.transform.up * 9999f);
                newScale.y *= GetScaleFactor(
                    activeScaleGesture.yExtentAtStart,
                    Vector3.Dot(pointerDelta, activeScaleGesture.yOutwardDirection),
                    activeScaleGesture.minimumYFactor,
                    activeScaleGesture.maximumYFactor);
                break;
        }

        selectedObject.transform.localScale = newScale;
    }

    bool TryGetSelectedScaleExtents(out float xExtent, out float yExtent)
    {
        xExtent = 0f;
        yExtent = 0f;

        if (selectedObject == null || !TryGetWorldBounds(GetScaleConstraintTransforms(), out Bounds selectionBounds))
            return false;

        xExtent = GetBoundsExtentAlongAxis(selectionBounds, selectedObject.transform.right);
        yExtent = GetBoundsExtentAlongAxis(selectionBounds, selectedObject.transform.up);
        return xExtent > Mathf.Epsilon && yExtent > Mathf.Epsilon;
    }

    static float GetBoundsExtentAlongAxis(Bounds bounds, Vector3 axis)
    {
        axis.z = 0f;
        axis.Normalize();
        return 2f * (Mathf.Abs(axis.x) * bounds.extents.x + Mathf.Abs(axis.y) * bounds.extents.y);
    }

    static Vector3 GetCameraFacingDirection(Vector3 selectionAxis, Vector3 cameraAxis)
    {
        selectionAxis.z = 0f;
        cameraAxis.z = 0f;
        selectionAxis.Normalize();
        cameraAxis.Normalize();
        return Vector3.Dot(selectionAxis, cameraAxis) > 0f ? -selectionAxis : selectionAxis;
    }

    void GetScaleFactorLimits(out float minimumXFactor, out float maximumXFactor, out float minimumYFactor, out float maximumYFactor)
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
            float startingXScale = GetStartingEffectiveScale(scaleConstrainedTransform, true);
            float startingYScale = GetStartingEffectiveScale(scaleConstrainedTransform, false);

            minimumXFactor = Mathf.Max(minimumXFactor, minimumObjectScale / startingXScale);
            minimumYFactor = Mathf.Max(minimumYFactor, minimumObjectScale / startingYScale);
            maximumXFactor = Mathf.Min(maximumXFactor, maximumScale / startingXScale);
            maximumYFactor = Mathf.Min(maximumYFactor, maximumScale / startingYScale);
        }
    }

    IEnumerable<Transform> GetScaleConstraintTransforms()
    {
        if (selectionGroup != null)
        {
            foreach (TemporarySelectionMember member in temporarySelectionMembers)
            {
                if (member.gameObject != null)
                    yield return member.gameObject.transform;
            }

            yield break;
        }

        if (selectedObject != null)
            yield return selectedObject.transform;
    }

    float GetStartingEffectiveScale(Transform scaleConstrainedTransform, bool xAxis)
    {
        float memberScale = Mathf.Abs(xAxis
            ? scaleConstrainedTransform.localScale.x
            : scaleConstrainedTransform.localScale.y);

        if (selectionGroup != null)
        {
            float groupScale = Mathf.Abs(xAxis
                ? activeScaleGesture.selectedLocalScaleAtStart.x
                : activeScaleGesture.selectedLocalScaleAtStart.y);
            memberScale *= groupScale;
        }

        return Mathf.Max(memberScale, Mathf.Epsilon);
    }

    static float GetMinimumObjectScale(GameObject levelObject)
    {
        return levelObject.name.Contains("Puller") ? 3f : 0.2f;
    }

    static float GetScaleFactor(float startExtent, float outwardPointerDistance, float minimumFactor, float maximumFactor)
    {
        float desiredFactor = 1f + (2f * outwardPointerDistance / startExtent);
        maximumFactor = Mathf.Max(minimumFactor, maximumFactor);
        return Mathf.Clamp(desiredFactor, minimumFactor, maximumFactor);
    }

    void EndScaleSelectedObject()
    {
        isTryingToScaleSelectedObject = false;
        horizontalLine.gameObject.SetActive(false);
        verticalLine.gameObject.SetActive(false);
    }

    void RefreshSelectionControls()
    {
        bool isBoxSelecting = hasSelectionDragExceededThreshold && PointerInput.Instance.IsHeld;
        bool show = selectedObject != null &&
                    !(isTryingToMoveSelectedObject || isTryingToRotateSelectedObject || isTryingToScaleSelectedObject || isTryingToPlace || isBoxSelecting);
        selectionControlsUI.SetSelectedTransform(selectedObject != null ? selectedObject.transform : null);
        // Keep the object active while a UI drag is running, but hide its CanvasGroup. That
        // lets its pointer handler still receive the matching drag and release events.
        selectionControlsUI.SetVisible(selectedObject != null && !isTryingToPlace);
        selectionControlsUI.SetControlsVisible(show);
        deselectObjectButton.SetActive(show);
        snapVerticalButton.SetActive(show);
        snapHorizontalButton.SetActive(show);
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

    public void SwitchToLocalTransformMode()
    {
        // change to opposite button
        worldTransformButton.SetActive(false);
        localTransformButton.SetActive(true);
        isWorldTransform = false;
    }

    public void SwitchToWorldTransformMode()
    {
        // change to opposite button
        localTransformButton.SetActive(false);
        worldTransformButton.SetActive(true);
        isWorldTransform = true;
    }
}
