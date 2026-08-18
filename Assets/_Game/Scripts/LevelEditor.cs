using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelEditor : MonoBehaviour
{
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

    [Header("Screen Space UI")]
    [SerializeField] SelectionControlsUI selectionControlsUI;

    bool isTryingToPlace = false;
    GameObject objectCurrentlyTryingToPlace = null;
    bool pointerIsOverObjectSelectionBar = false;
    GameObject selectedObject = null;
    GameObject lastSelectedObject = null;
    bool isWorldTransform = true;

    // object selection
    const float MINIMUM_DRAG_DISTANCE_PIXELS = 15f;
    bool hasSelectionDragExceededThreshold = false;
    GameObject selectionGroup;

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
    Vector3 pointerPositionAtStartScale;
    Vector3 selectedObjectScaleAtStartScale;
    float minimumScale = 0.2f;
    float maximumScale = 999999f;
    float selectedObjectXScaleAtStartScale;
    float selectedObjectYScaleAtStartScale;
    float scaleIncrement = 0f;

    ObjectTransformControl activeMoveControl;
    ObjectTransformControl activeScaleControl;

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
        UpdateActiveScreenSpaceTransformControl();
        RefreshSelectionControls();
    }

    private void OnEnable()
    {
        EventManager.Instance.UnselectObjectEvent.AddListener(UnselectObject);
    }

    private void OnDisable()
    {
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
        if (selectedObject != null && lastSelectedObject != null)
        {
            selectedObject.transform.position = new Vector3(lastSelectedObject.transform.position.x, selectedObject.transform.position.y, 0f);
        }
    }

    public void SnapSelectedObjectToLastVertical()
    {
        if (selectedObject != null && lastSelectedObject != null)
        {
            selectedObject.transform.position = new Vector3(selectedObject.transform.position.x, lastSelectedObject.transform.position.y, 0f);
        }
    }

    #endregion

    Vector3 GetPointerWorldPosition(Vector2 screenPosition)
    {
        Vector3 currentPointerWorldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
        // ensure no depth
        currentPointerWorldPosition.z = 0;
        return currentPointerWorldPosition;
    }

    Vector3 GetCurrentPointerWorldPosition()
    {
        return GetPointerWorldPosition(PointerInput.Instance.ScreenPosition);
    }

    Vector2 GetCurrentPointerScreenPosition()
    {
        return PointerInput.Instance.ScreenPosition;
    }

    void UpdateBoxSelectIntentFromPointerDrag()
    {
        if (PointerInput.Instance.WasPressedThisFrame)
            hasSelectionDragExceededThreshold = false;

        if (PointerInput.Instance.IsHeld && !hasSelectionDragExceededThreshold)
        {
            float dragDistanceInPixels = PointerInput.Instance.DragDistancePixels;
            if (dragDistanceInPixels >= MINIMUM_DRAG_DISTANCE_PIXELS)
            {
                hasSelectionDragExceededThreshold = true;
                Debug.Log("LevelEditor: drag threshold crossed, box-select mode latched for this pointer cycle.");
            }
        }
    }

    void HandlePlacePrefab()
    {
        if (objectCurrentlyTryingToPlace != null && isTryingToPlace)
        {
            // make the object the player is currently trying to place follow the pointer
            objectCurrentlyTryingToPlace.transform.position = GetCurrentPointerWorldPosition();

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
                    SetMinimumScale();
                }

                objectCurrentlyTryingToPlace = null;
            }
        }
    }

    void HandleSelectObject()
    {
        // set the object the player clicks as selected if it's allowed to be selected
        if (PointerInput.Instance.WasReleasedThisFrame && !UIManager.Instance.IsInControlBlockingMenu)
        {
            bool shouldDoBoxSelect = hasSelectionDragExceededThreshold;
            hasSelectionDragExceededThreshold = false;

            if (PointerInput.Instance.WasReleasedOverUi) // click was on a UI element, so don't try to change selected object
                return;

            if (shouldDoBoxSelect)
            {
                Debug.Log("LevelEditor: pointer cycle resolved as box-select.");
                // TODO: create/update visible box while dragging.
                // TODO: collect objects inside selection box and apply selection grouping flow on pointer up.
            }
            else // click select
            {
                Debug.Log("LevelEditor: pointer cycle resolved as click-select.");
                Ray ray = Camera.main.ScreenPointToRay(GetCurrentPointerScreenPosition());
                RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

                if (hit.collider != null) // object hit
                {
                    SelectObject(hit.collider.gameObject);
                    ConfigureSelectionControlsForSelectedObject();
                    SetMinimumScale();
                }
                else // no object hit
                {
                    // TODO: circle-collision fallback selection flow should be initiated here.
                    if (!PointerInput.Instance.WasPressedOverUi) // if player clicks just the background, unselect object
                    {
                        UnselectObject();
                    }
                }

            }
        }

    }

    void SelectObject(GameObject objectToSelect)
    {
        if (selectedObject != null)
            lastSelectedObject = selectedObject;
        selectedObject = objectToSelect;

        if (selectionControlsUI != null)
            selectionControlsUI.SetSelectedTransform(selectedObject.transform);
    }

    void UnselectObject()
    {
        if (selectedObject != null)
            lastSelectedObject = selectedObject;
        selectedObject = null;

        if (selectionControlsUI != null)
        {
            selectionControlsUI.SetSelectedTransform(null);
            selectionControlsUI.SetVisible(false);
        }
    }

    void ConfigureSelectionControlsForSelectedObject()
    {
        if (selectedObject != null)
        {
            // show/hide certain controls depending on the type of object selected
            bool isPlayerStartPoint = selectedObject.name == "PlayerStartPoint";
            bool isPuller = selectedObject.name.Contains("Puller");
            bool isKillCircle = selectedObject.name.Contains("KillCircle");

            selectionControlsUI.SetControlAvailability(
                !isPlayerStartPoint,
                !isPlayerStartPoint,
                !isPlayerStartPoint && !isPuller && !isKillCircle,
                !isPlayerStartPoint && !isPuller && !isKillCircle,
                !isPlayerStartPoint && !isPuller && !isKillCircle);
        }
    }

    void SetMinimumScale()
    {
        // set minimum scale depending on which type of object is selected
        if (selectedObject.transform.name.Contains("Puller"))
            minimumScale = 3f;
        else
            minimumScale = 0.2f;
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
    public void BeginObjectTransformControl(ObjectTransformControl control, Vector2 screenPosition)
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
    }

    public void UpdateObjectTransformControl(ObjectTransformControl control, Vector2 screenPosition, float dragDistancePixels)
    {
        if (control == activeMoveControl && isTryingToMoveSelectedObject)
            UpdateMoveSelectedObject(screenPosition, dragDistancePixels);
        else if (control == ObjectTransformControl.Rotate && isTryingToRotateSelectedObject)
            UpdateRotateSelectedObject(screenPosition);
        else if (control == activeScaleControl && isTryingToScaleSelectedObject)
            UpdateScaleSelectedObject(screenPosition);
    }

    public void EndObjectTransformControl(ObjectTransformControl control, Vector2 screenPosition, float dragDistancePixels)
    {
        if (control == activeMoveControl && isTryingToMoveSelectedObject)
            EndMoveSelectedObject();
        else if (control == ObjectTransformControl.Rotate && isTryingToRotateSelectedObject)
            EndRotateSelectedObject();
        else if (control == activeScaleControl && isTryingToScaleSelectedObject)
            EndScaleSelectedObject();
    }

    void UpdateActiveScreenSpaceTransformControl()
    {
        if (!PointerInput.Instance.IsHeld)
            return;

        // UI drag events only fire when the pointer itself moves. Re-evaluate from the shared
        // screen position every frame so camera zoom/pan also updates the object's world position.
        Vector2 screenPosition = PointerInput.Instance.ScreenPosition;

        if (isTryingToMoveSelectedObject)
            UpdateMoveSelectedObject(screenPosition, PointerInput.Instance.DragDistancePixels);
        else if (isTryingToRotateSelectedObject)
            UpdateRotateSelectedObject(screenPosition);
        else if (isTryingToScaleSelectedObject)
            UpdateScaleSelectedObject(screenPosition);
    }

    void BeginMoveSelectedObject(ObjectTransformControl control, Vector2 screenPosition)
    {
        if (control == ObjectTransformControl.Duplicate)
        {
            SelectObject(Instantiate(selectedObject, levelObjectsCollection.transform));
            selectedObject.transform.name = selectedObject.transform.name.Replace("(Clone)", "");
            ConfigureSelectionControlsForSelectedObject();
            SetMinimumScale();
        }

        isTryingToMoveSelectedObject = true;
        activeMoveControl = control;
        selectedObjectPositionAtStartMove = selectedObject.transform.position;
        pointerPositionAtStartMove = GetPointerWorldPosition(screenPosition);

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

    void UpdateMoveSelectedObject(Vector2 screenPosition, float dragDistancePixels)
    {
        if (selectedObject == null)
            return;

        Vector3 pointerWorldPosition = GetPointerWorldPosition(screenPosition);

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

        if (pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint"))
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

        if (selectedObject != null && pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint"))
        {
            Destroy(selectedObject);
            UnselectObject();
            deselectObjectButton.gameObject.SetActive(false);
        }
    }

    void BeginRotateSelectedObject(Vector2 screenPosition)
    {
        rotationLine.gameObject.SetActive(true);
        rotationLine.SetPosition(0, selectedObject.transform.position);

        isTryingToRotateSelectedObject = true;
        selectedObjectRotationAtStartRotate = selectedObject.transform.localEulerAngles.z;
        Vector3 direction = GetPointerWorldPosition(screenPosition) - selectedObject.transform.position;
        angleToPointerAtStartRotate = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        rotationIncrementOffset = isWorldTransform
            ? 0f
            : selectedObjectRotationAtStartRotate - RoundToIncrement(selectedObjectRotationAtStartRotate, rotateIncrement);
    }

    void UpdateRotateSelectedObject(Vector2 screenPosition)
    {
        if (selectedObject == null)
            return;

        Vector3 pointerWorldPosition = GetPointerWorldPosition(screenPosition);
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
        isTryingToScaleSelectedObject = true;
        activeScaleControl = control;
        pointerPositionAtStartScale = GetPointerWorldPosition(screenPosition);
        selectedObjectScaleAtStartScale = selectedObject.transform.localScale;
        selectedObjectXScaleAtStartScale = selectedObject.transform.localScale.x;
        selectedObjectYScaleAtStartScale = selectedObject.transform.localScale.y;
    }

    void UpdateScaleSelectedObject(Vector2 screenPosition)
    {
        if (selectedObject == null)
            return;

        Vector3 newScale = selectedObject.transform.localScale;
        Vector3 pointerWorldPosition = GetPointerWorldPosition(screenPosition);
        float pointerScaleDeltaX = pointerPositionAtStartScale.x - pointerWorldPosition.x;
        float pointerScaleDeltaY = pointerWorldPosition.y - pointerPositionAtStartScale.y;

        switch (activeScaleControl)
        {
            case ObjectTransformControl.ScaleBoth:
                Vector3 scaleReferencePoint = pointerPositionAtStartScale + selectedObject.transform.right * 9999f;
                float distanceToReferenceAtStartScale = Vector3.Distance(pointerPositionAtStartScale, scaleReferencePoint);
                float scaleDelta = (Vector3.Distance(pointerWorldPosition, scaleReferencePoint) - distanceToReferenceAtStartScale) * 2f;
                float xScaleMultiplier = scaleDelta / selectedObjectXScaleAtStartScale;
                float yScaleMultiplier = scaleDelta / selectedObjectYScaleAtStartScale;
                float parentXScale = selectedObject.transform.parent.localScale.x;
                float parentYScale = selectedObject.transform.parent.localScale.y;

                if (selectedObjectXScaleAtStartScale > selectedObjectYScaleAtStartScale)
                {
                    float xAxisScaleDifferenceSinceStartScale = selectedObject.transform.localScale.x / selectedObjectXScaleAtStartScale;
                    newScale = new Vector3(
                        Mathf.Clamp((1 + xScaleMultiplier / parentXScale) * selectedObjectXScaleAtStartScale, minimumScale / parentXScale, maximumScale / parentXScale),
                        Mathf.Clamp(selectedObjectYScaleAtStartScale * xAxisScaleDifferenceSinceStartScale, minimumScale / parentYScale, maximumScale / parentYScale),
                        1f);
                }
                else if (selectedObjectYScaleAtStartScale > selectedObjectXScaleAtStartScale)
                {
                    float yAxisScaleDifferenceSinceStartScale = selectedObject.transform.localScale.y / selectedObjectYScaleAtStartScale;
                    newScale = new Vector3(
                        Mathf.Clamp(selectedObjectXScaleAtStartScale * yAxisScaleDifferenceSinceStartScale, minimumScale * parentXScale, maximumScale * parentXScale),
                        Mathf.Clamp((1 + yScaleMultiplier / parentYScale) * selectedObjectYScaleAtStartScale, minimumScale / parentYScale, maximumScale / parentYScale),
                        1f);
                }
                else
                {
                    newScale = new Vector3(
                        Mathf.Clamp((1 + xScaleMultiplier / parentXScale) * selectedObjectXScaleAtStartScale, minimumScale / parentXScale, maximumScale / parentXScale),
                        Mathf.Clamp((1 + yScaleMultiplier / parentYScale) * selectedObjectYScaleAtStartScale, minimumScale / parentYScale, maximumScale / parentYScale),
                        1f);
                }
                break;

            case ObjectTransformControl.ScaleX:
                horizontalLine.gameObject.SetActive(true);
                horizontalLine.SetPosition(0, selectedObject.transform.position + selectedObject.transform.right * 9999f);
                horizontalLine.SetPosition(1, selectedObject.transform.position - selectedObject.transform.right * 9999f);
                newScale = new Vector3(
                    Mathf.Clamp(pointerScaleDeltaX * 2f + selectedObjectScaleAtStartScale.x, minimumScale, maximumScale),
                    selectedObjectScaleAtStartScale.y,
                    1f);
                break;

            case ObjectTransformControl.ScaleY:
                verticalLine.gameObject.SetActive(true);
                verticalLine.SetPosition(0, selectedObject.transform.position + selectedObject.transform.up * 9999f);
                verticalLine.SetPosition(1, selectedObject.transform.position - selectedObject.transform.up * 9999f);
                newScale = new Vector3(
                    selectedObjectScaleAtStartScale.x,
                    Mathf.Clamp(pointerScaleDeltaY * 2f + selectedObjectScaleAtStartScale.y, minimumScale, maximumScale),
                    1f);
                break;
        }

        selectedObject.transform.localScale = newScale;
    }

    void EndScaleSelectedObject()
    {
        isTryingToScaleSelectedObject = false;
        horizontalLine.gameObject.SetActive(false);
        verticalLine.gameObject.SetActive(false);
    }

    void RefreshSelectionControls()
    {
        bool show = selectedObject != null && !(isTryingToMoveSelectedObject || isTryingToRotateSelectedObject || isTryingToScaleSelectedObject || isTryingToPlace);
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
        lastSelectedObject = null;
        selectedObject = null;
        RefreshSelectionControls();

        startLocationIcon.SetActive(false);
        UIManager.Instance.SwitchToPlayerMode();
    }

    #region Object Place Functions
    void StartTryingToPlaceObject()
    {
        isTryingToPlace = true;
        objectCurrentlyTryingToPlace = Instantiate(prefabToPlace, GetCurrentPointerWorldPosition(), Quaternion.identity, levelObjectsCollection.transform);
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
        LevelManager.Instance.SaveLevel();
    }
    public void DeleteAllLevelObjects()
    {
        LevelManager.Instance.DestroyAllExistingLevelObjects();
    }
    public void CopyLevelCodeToClipboard()
    {
        LevelManager.Instance.CopyLevelCodeToClipboard();
    }
    public void LoadLevelFromClipboard()
    {
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
