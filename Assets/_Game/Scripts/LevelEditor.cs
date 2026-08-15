using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelEditor : MonoBehaviour
{
    // self object references
    [SerializeField] Rigidbody2D rb;
    [SerializeField] GameObject prefabToPlace;
    [SerializeField] GameObject player;
    [SerializeField] GameObject startLocationIcon;
    [SerializeField] EventSystem es;
    [SerializeField] GameObject canvas;
    [SerializeField] GameObject localTransformButton;
    [SerializeField] GameObject worldTransformButton;
    [SerializeField] GameObject closeObjectTransformControlsButton;
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
    [SerializeField] GameObject objectTransformControls;

    bool isTryingToPlace = false;
    GameObject objectCurrentlyTryingToPlace = null;
    bool pointerIsOverObjectSelectionBar = false;
    GameObject selectedObject = null;
    GameObject lastSelectedObject = null;
    bool isWorldTransform = true;
    bool wasLastPointerDownOverUi = false;
    bool wasLastPointerUpOverUi = false;
    Vector3 pointerWorldPositionAtLastPointerDown;

    // object selection
    const float MINIMUM_DRAG_DISTANCE_PIXELS = 15f;
    Vector2 pointerScreenPositionAtLastPointerDown;
    bool hasSelectionDragExceededThreshold = false;
    int primaryFingerId = -1;
    Vector2 currentPointerScreenPosition;
    bool pointerWasPressedThisFrame = false;
    bool pointerIsHeld = false;
    bool pointerWasReleasedThisFrame = false;
    Vector2 pointerPositionAtStartSelect;
    GameObject selectionGroup;

    // object movement
    Vector3 moveOffset;
    bool isTryingToMoveSelectedObject = false;
    Transform lastHitMoveControl;
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
    Transform lastHitScaleControl;
    float minimumScale = 0.2f;
    float maximumScale = 999999f;
    float selectedObjectXScaleAtStartScale;
    float selectedObjectYScaleAtStartScale;
    float scaleIncrement = 0f;

    // TODO: if i turn the in world buttons into UI buttons i won't need this
    readonly List<string> UNSELECTABLE_OBJECTS = new List<string> 
    {
        "Move X",
        "Move Y",
        "Move Both",
        "Scale X",
        "Scale Y",
        "Scale Both",
        "Rotate",
        "Duplicate"
    };

    void Awake()
    {
        // enable this for a frame to let the scaling initialize
        // TODO: change this to false once i change them to real buttons
        objectTransformControls.SetActive(true);

        // ensure toggleable elements are at proper default show/hide
        closeObjectTransformControlsButton.SetActive(false);
        worldTransformButton.SetActive(true);
        localTransformButton.SetActive(false);
        snapVerticalButton.SetActive(false);
        snapHorizontalButton.SetActive(false);
    }

    void Start()
    {
        // ensure level editor object and all of its visuals are disabled before starting game
        // TODO: move this back to awake once i add real buttons. this is also to initialize button scaling
        gameObject.SetActive(false);

        // ensure level editor UI is enabled
        canvas.gameObject.SetActive(true);

        objectTransformControls.SetActive(false);

        // setup increment dropdown listeners
        moveIncrementDropdown.onValueChanged.AddListener(OnMoveIncrementDropdownChanged);
        rotateIncrementDropdown.onValueChanged.AddListener(OnRotateIncrementDropdownChanged);
        scaleIncrementDropdown.onValueChanged.AddListener(OnScaleIncrementDropdownChanged);
    }

    void Update()
    {
        UpdatePointerInput();
        CheckPointerPositionAtLastPointerDown();
        CheckIfLastPointerDownWasOverUi();
        CheckIfLastPointerUpWasOverUi();
        UpdateBoxSelectIntentFromPointerDrag();
        HandlePlacePrefab();
        HandleSelectObject();
        HandleScaleSelectedObject();
        HandleRotateSelectedObject();
        HandleMoveSelectedObject();
        HandleShowObjectTransformControls();
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

    // A touch that begins an editor interaction remains its primary pointer until it ends or is canceled.
    // This prevents a second touch from taking over the editor interaction mid-gesture.
    void UpdatePointerInput()
    {
        pointerWasPressedThisFrame = false;
        pointerWasReleasedThisFrame = false;

        if (primaryFingerId != -1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId == primaryFingerId)
                {
                    currentPointerScreenPosition = touch.position;
                    pointerIsHeld = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;

                    if (!pointerIsHeld)
                    {
                        pointerWasReleasedThisFrame = true;
                        primaryFingerId = -1;
                    }

                    return;
                }
            }

            pointerIsHeld = false;
            pointerWasReleasedThisFrame = true;
            primaryFingerId = -1;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began)
            {
                primaryFingerId = touch.fingerId;
                currentPointerScreenPosition = touch.position;
                pointerWasPressedThisFrame = true;
                pointerIsHeld = true;
                return;
            }
        }

        if (Input.touchCount > 0)
        {
            pointerIsHeld = false;
            return;
        }

        currentPointerScreenPosition = Input.mousePosition;
        pointerWasPressedThisFrame = Input.GetMouseButtonDown(0);
        pointerIsHeld = Input.GetMouseButton(0);
        pointerWasReleasedThisFrame = Input.GetMouseButtonUp(0);
    }

    Vector3 GetCurrentPointerWorldPosition()
    {
        Vector2 currentPointerScreenPosition = GetCurrentPointerScreenPosition();
        Vector3 currentPointerWorldPosition = Camera.main.ScreenToWorldPoint(currentPointerScreenPosition);
        // ensure no depth
        currentPointerWorldPosition.z = 0;
        return currentPointerWorldPosition;
    }

    void CheckPointerPositionAtLastPointerDown()
    {
        if (pointerWasPressedThisFrame)
        {
            pointerWorldPositionAtLastPointerDown = GetCurrentPointerWorldPosition();
            pointerScreenPositionAtLastPointerDown = GetCurrentPointerScreenPosition();
            hasSelectionDragExceededThreshold = false;
        }
    }

    Vector2 GetCurrentPointerScreenPosition()
    {
        return currentPointerScreenPosition;
    }

    void UpdateBoxSelectIntentFromPointerDrag()
    {
        if (pointerIsHeld && !hasSelectionDragExceededThreshold)
        {
            float dragDistanceInPixels = Vector2.Distance(pointerScreenPositionAtLastPointerDown, GetCurrentPointerScreenPosition());
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
            if (pointerWasReleasedThisFrame && isTryingToPlace)
            {
                isTryingToPlace = false;

                if (pointerIsOverObjectSelectionBar) // if player tries to place object over object selection bar, delete the object to cancel placement
                {
                    Destroy(objectCurrentlyTryingToPlace);
                }
                else // place object
                {
                    SelectObject(objectCurrentlyTryingToPlace);
                    SetWhichObjectTransformControlsToShow();
                    AlignScaleControlsWithSelectedObject();
                    SetMinimumScale();
                }

                objectCurrentlyTryingToPlace = null;
            }
        }
    }

    void CheckIfLastPointerDownWasOverUi()
    {
        if (pointerWasPressedThisFrame)
        {
            // check if any UI elements were hit
            PointerEventData data = new PointerEventData(EventSystem.current);
            data.position = GetCurrentPointerScreenPosition();

            List<RaycastResult> uiHits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, uiHits);

            wasLastPointerDownOverUi = uiHits.Count > 1;
        }
    }

    void CheckIfLastPointerUpWasOverUi()
    {
        if (pointerWasReleasedThisFrame)
        {
            // check if any UI elements were hit
            PointerEventData data = new PointerEventData(EventSystem.current);
            data.position = GetCurrentPointerScreenPosition();

            List<RaycastResult> uiHits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, uiHits);

            wasLastPointerUpOverUi = uiHits.Count > 1;
        }
    }

    void HandleSelectObject()
    {
        // set the object the player clicks as selected if it's allowed to be selected
        if (pointerWasReleasedThisFrame && !UIManager.Instance.IsInControlBlockingMenu)
        {
            bool shouldDoBoxSelect = hasSelectionDragExceededThreshold;
            hasSelectionDragExceededThreshold = false;

            if (wasLastPointerUpOverUi) // click was on a UI element, so don't try to change selected object
                return;

            if (isTryingToMoveSelectedObject && lastHitMoveControl != null && lastHitMoveControl.name == "Duplicate")
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
                    if (!UNSELECTABLE_OBJECTS.Contains(hit.collider.gameObject.transform.name)) // don't allow any UI objects to be set as selected object
                    {
                        SelectObject(hit.collider.gameObject);

                        AlignScaleControlsWithSelectedObject();
                        SetWhichObjectTransformControlsToShow();
                        SetMinimumScale();
                    }
                }
                else // no object hit
                {
                    // TODO: circle-collision fallback selection flow should be initiated here.
                    if (!wasLastPointerDownOverUi) // if player clicks just the background, unselect object
                    {
                        UnselectObject();
                    }
                }

            }
        }

        if (selectedObject != null)
        {
            objectTransformControls.transform.position = new Vector3(selectedObject.transform.position.x, selectedObject.transform.position.y, objectTransformControls.transform.position.z);
        }
    }

    void SelectObject(GameObject objectToSelect)
    {
        if (selectedObject != null)
            lastSelectedObject = selectedObject;
        selectedObject = objectToSelect;
    }

    void UnselectObject()
    {
        if (selectedObject != null)
            lastSelectedObject = selectedObject;
        selectedObject = null;
    }

    void AlignScaleControlsWithSelectedObject()
    {
        if (selectedObject != null)
        {
            objectTransformControls.transform.Find("Scale Both").transform.localRotation = selectedObject.transform.localRotation;
            objectTransformControls.transform.Find("Scale X").transform.localRotation = selectedObject.transform.localRotation;
            objectTransformControls.transform.Find("Scale Y").transform.localRotation = selectedObject.transform.localRotation * Quaternion.Euler(0f, 0f, 90f);
        }
    }

    void SetWhichObjectTransformControlsToShow()
    {
        if (selectedObject != null)
        {
            // show/hide certain controls depending on the type of object selected
            bool isPlayerStartPoint = selectedObject.name == "PlayerStartPoint";
            bool isPuller = selectedObject.name.Contains("Puller");
            bool isKillCircle = selectedObject.name.Contains("KillCircle");

            objectTransformControls.transform.Find("Duplicate").gameObject.SetActive(!isPlayerStartPoint);
            objectTransformControls.transform.Find("Scale Both").gameObject.SetActive(!isPlayerStartPoint);
            objectTransformControls.transform.Find("Scale X").gameObject.SetActive(!isPlayerStartPoint && !isPuller && !isKillCircle);
            objectTransformControls.transform.Find("Scale Y").gameObject.SetActive(!isPlayerStartPoint && !isPuller && !isKillCircle);
            objectTransformControls.transform.Find("Rotate").gameObject.SetActive(!isPlayerStartPoint && !isPuller && !isKillCircle);
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

    void HandleMoveSelectedObject()
    {
        // start trying to move selected object when the player presses on move control
        if (pointerWasPressedThisFrame && !wasLastPointerDownOverUi)
        {
            Ray ray = Camera.main.ScreenPointToRay(GetCurrentPointerScreenPosition());
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.transform != null)
            {
                string hitName = hit.transform.name;

                if (hitName == "Move Both" || hitName == "Move X" || hitName == "Move Y" || hitName == "Duplicate")
                {
                    if (hitName == "Duplicate")
                    {
                        SelectObject(Instantiate(selectedObject, levelObjectsCollection.transform));
                        selectedObject.transform.name = selectedObject.transform.name.Replace("(Clone)", "");
                    }

                    isTryingToMoveSelectedObject = true;
                    lastHitMoveControl = hit.transform;
                    selectedObjectPositionAtStartMove = selectedObject.transform.position;
                    pointerPositionAtStartMove = GetCurrentPointerWorldPosition();

                    if (isWorldTransform)
                    {
                        moveIncrementOffset = new Vector3(0f, 0f, 0f);
                    }
                    else // local transform
                    {
                        moveIncrementOffset = new Vector3(selectedObjectPositionAtStartMove.x - RoundToIncrement(selectedObjectPositionAtStartMove.x, moveIncrement),
                                                            selectedObjectPositionAtStartMove.y - RoundToIncrement(selectedObjectPositionAtStartMove.y, moveIncrement),
                                                            0f);
                    }

                    // get offset between selected object and pointer position to keep it while moving
                    moveOffset = selectedObject.transform.position - GetCurrentPointerWorldPosition();
                }
            }
        }

        // stop trying to move selected object when player releases
        // TODO: handle if they pause or exit edit mode while moving object, if that's still an issue later
        if (pointerWasReleasedThisFrame && isTryingToMoveSelectedObject)
        {
            verticalLine.gameObject.SetActive(false);
            horizontalLine.gameObject.SetActive(false);

            isTryingToMoveSelectedObject = false;
            lastHitMoveControl = null;

            // if object is dropped while pointer is over object selection bar, destroy and deselect it
            if (selectedObject != null && pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint"))
            {
                Destroy(selectedObject);
                selectedObject = null;
                closeObjectTransformControlsButton.gameObject.SetActive(false);
            }
        }

        if (pointerIsHeld && selectedObject != null)
        {
            if (isTryingToMoveSelectedObject && lastHitMoveControl != null)
            {
                Vector3 pointerWorldPosition = GetCurrentPointerWorldPosition();

                // Keep a clicked Duplicate clone in place until the pointer moves beyond the shared drag threshold.
                float dragDistanceInPixels = Vector2.Distance(pointerScreenPositionAtLastPointerDown, GetCurrentPointerScreenPosition());
                if (lastHitMoveControl.name == "Duplicate" && dragDistanceInPixels < MINIMUM_DRAG_DISTANCE_PIXELS)
                    selectedObject.transform.position = selectedObjectPositionAtStartMove;
                else
                {
                    float newX = RoundToIncrement(pointerWorldPosition.x + moveOffset.x, moveIncrement) + moveIncrementOffset.x;
                    float newY = RoundToIncrement(pointerWorldPosition.y + moveOffset.y, moveIncrement) + moveIncrementOffset.y;

                    // make selectedObject move with pointer
                    selectedObject.transform.position = new Vector3(newX, newY, 0f);
                }

                // if hovering over object selection bar, hide object placement preview and transform controls
                if (pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint"))
                {
                    selectedObject.SetActive(false);
                }
                else // not hovering pointer over object selection bar
                {
                    selectedObject.SetActive(true);
                }

                if (lastHitMoveControl.name == "Move X")
                {
                    // move
                    float newX = RoundToIncrement(selectedObject.transform.position.x, moveIncrement) + moveIncrementOffset.x;
                    selectedObject.transform.position = new Vector3(newX, selectedObjectPositionAtStartMove.y, 0f);

                    // show guide
                    horizontalLine.gameObject.SetActive(true);
                    horizontalLine.transform.position = selectedObject.transform.position;
                    horizontalLine.SetPosition(0, new Vector3(horizontalLine.transform.position.x + 9999f, horizontalLine.transform.position.y, 0f));
                    horizontalLine.SetPosition(1, new Vector3(horizontalLine.transform.position.x - 9999f, horizontalLine.transform.position.y, 0f));
                }

                if (lastHitMoveControl.name == "Move Y")
                {
                    // move
                    float newY = RoundToIncrement(selectedObject.transform.position.y, moveIncrement) + moveIncrementOffset.y;
                    selectedObject.transform.position = new Vector3(selectedObjectPositionAtStartMove.x, newY, 0f);

                    // show guide
                    verticalLine.gameObject.SetActive(true);
                    verticalLine.transform.position = selectedObject.transform.position;
                    verticalLine.SetPosition(0, new Vector3(verticalLine.transform.position.x, verticalLine.transform.position.y + 9999f, 0f));
                    verticalLine.SetPosition(1, new Vector3(verticalLine.transform.position.x, verticalLine.transform.position.y - 9999f, 0f));
                }
            }
        }
    }

    void HandleRotateSelectedObject()
    {
        if (pointerWasPressedThisFrame && !wasLastPointerDownOverUi) // start rotate
        {
            Ray ray = Camera.main.ScreenPointToRay(GetCurrentPointerScreenPosition());
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.transform != null && hit.transform.name == "Rotate")
            {
                rotationLine.gameObject.SetActive(true);
                rotationLine.SetPosition(0, selectedObject.transform.position);

                // initiate rotation
                isTryingToRotateSelectedObject = true;
                
                // remember values at start rotate to later make the rotation relative to the selected object's starting rotation
                selectedObjectRotationAtStartRotate = selectedObject.transform.localEulerAngles.z;
                // get the angle to the pointer when the player starts rotating the object
                Vector3 direction = GetCurrentPointerWorldPosition() - selectedObject.transform.position;
                angleToPointerAtStartRotate = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                if (isWorldTransform)
                {
                    rotationIncrementOffset = 0f;
                }
                else
                {
                    rotationIncrementOffset = selectedObjectRotationAtStartRotate - RoundToIncrement(selectedObjectRotationAtStartRotate, rotateIncrement);
                }
            }
        }

        if (pointerWasReleasedThisFrame && isTryingToRotateSelectedObject) // end rotate
        {
            rotationLine.gameObject.SetActive(false);

            // stop rotating
            isTryingToRotateSelectedObject = false;

            AlignScaleControlsWithSelectedObject();
        }

        if (isTryingToRotateSelectedObject) // do rotate
        {
            // get rotation to current pointer position
            Vector3 direction = GetCurrentPointerWorldPosition() - selectedObject.transform.position;
            float currentAngleToPointer = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // get the difference between current angle to pointer and the angle to pointer when the player started rotating
            float deltaAngle = currentAngleToPointer - angleToPointerAtStartRotate;

            // add the difference between start and end rotate to the selected object's rotation when the player started rotating
            float newRotation;

            if (isWorldTransform)
            {
                newRotation = RoundToIncrement(selectedObjectRotationAtStartRotate + deltaAngle, rotateIncrement);
            }
            else // local transform
            {
                newRotation = RoundToIncrement(selectedObjectRotationAtStartRotate + deltaAngle, rotateIncrement) + rotationIncrementOffset;
            }

            // apply new rotation to selected object
            selectedObject.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, newRotation));

            // update line renderer position
            rotationLine.SetPosition(1, GetCurrentPointerWorldPosition());
        }
    }

    void HandleScaleSelectedObject()
    {
        if (pointerWasPressedThisFrame && !wasLastPointerDownOverUi) // start scale
        {
            Ray ray = Camera.main.ScreenPointToRay(GetCurrentPointerScreenPosition());
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.transform != null)
            {
                string hitName = hit.transform.name;

                if (hitName == "Scale Both" || hitName == "Scale X" || hitName == "Scale Y")
                {
                    isTryingToScaleSelectedObject = true;
                    pointerPositionAtStartScale = GetCurrentPointerWorldPosition();
                    selectedObjectScaleAtStartScale = selectedObject.transform.localScale;
                    lastHitScaleControl = hit.transform;
                    selectedObjectXScaleAtStartScale = selectedObject.transform.localScale.x;
                    selectedObjectYScaleAtStartScale = selectedObject.transform.localScale.y;
                }
            }
        }
        if (pointerWasReleasedThisFrame && isTryingToScaleSelectedObject) // end scale
        {
            isTryingToScaleSelectedObject = false;

            // hide guide
            horizontalLine.gameObject.SetActive(false);
            verticalLine.gameObject.SetActive(false);
        }
        if (isTryingToScaleSelectedObject) // do scaling
        {
            Vector3 newScale = selectedObject.transform.localScale;
            Vector3 pointerWorldPosition = GetCurrentPointerWorldPosition();

            // get scaling to add/remove depending on pointer movement
            // TODO: change this to be the distance directly towards/away from the selected object instead of this absolute position method
            float pointerScaleDeltaX = pointerPositionAtStartScale.x - pointerWorldPosition.x;
            float pointerScaleDeltaY = pointerWorldPosition.y - pointerPositionAtStartScale.y;

            // get scaling depending on which scale control was pressed
            switch (lastHitScaleControl.name)
            {
                case "Scale Both":
                    Vector3 scaleReferencePoint = pointerPositionAtStartScale + selectedObject.transform.right * 9999f;
                    float distanceToReferenceAtStartScale = Vector3.Distance(pointerPositionAtStartScale, scaleReferencePoint);
                    float scaleDelta = (Vector3.Distance(pointerWorldPosition, scaleReferencePoint) - distanceToReferenceAtStartScale) * 2f; // * 2 since it needs to add the length to both sides

                    float xScaleMultiplier = scaleDelta / selectedObjectXScaleAtStartScale;
                    float yScaleMultiplier = scaleDelta / selectedObjectYScaleAtStartScale;

                    float parentXScale = selectedObject.transform.parent.localScale.x;
                    float parentYScale = selectedObject.transform.parent.localScale.y;

                    if (selectedObjectXScaleAtStartScale > selectedObjectYScaleAtStartScale) // x width bigger than y width
                    {
                        float xAxisScaleDifferenceSinceStartScale = selectedObject.transform.localScale.x / selectedObjectXScaleAtStartScale;

                        newScale = new Vector3(Mathf.Clamp((1 + xScaleMultiplier / parentXScale) * selectedObjectXScaleAtStartScale, minimumScale / parentXScale, maximumScale / parentXScale),
                        Mathf.Clamp(selectedObjectYScaleAtStartScale * xAxisScaleDifferenceSinceStartScale, minimumScale / parentYScale, maximumScale / parentYScale),
                        1f);
                    }
                    else if (selectedObjectYScaleAtStartScale > selectedObjectXScaleAtStartScale) // y width bigger than x width
                    {
                        float yAxisScaleDifferenceSinceStartScale = selectedObject.transform.localScale.y / selectedObjectYScaleAtStartScale;

                        newScale = new Vector3(Mathf.Clamp(selectedObjectXScaleAtStartScale * yAxisScaleDifferenceSinceStartScale, minimumScale * parentXScale, maximumScale * parentXScale),
                        Mathf.Clamp((1 + yScaleMultiplier / parentYScale) * selectedObjectYScaleAtStartScale, minimumScale / parentYScale, maximumScale / parentYScale),
                        1f);
                    }
                    else // x and y width equal. so square or circular objects
                    {
                        newScale = new Vector3(Mathf.Clamp((1 + xScaleMultiplier / parentXScale) * selectedObjectXScaleAtStartScale, minimumScale / parentXScale, maximumScale / parentXScale),
                        Mathf.Clamp((1 + yScaleMultiplier / parentYScale) * selectedObjectYScaleAtStartScale, minimumScale / parentYScale, maximumScale / parentYScale),
                        1f);
                    }

                    break;
                case "Scale X":
                    // show guide
                    horizontalLine.gameObject.SetActive(true);
                    horizontalLine.SetPosition(0, selectedObject.transform.position + selectedObject.transform.right * 9999f);
                    horizontalLine.SetPosition(1, selectedObject.transform.position - selectedObject.transform.right * 9999f);

                    newScale = new Vector3(Mathf.Clamp(pointerScaleDeltaX * 2f + selectedObjectScaleAtStartScale.x, minimumScale, maximumScale), // * 2 since it's for both sides
                        selectedObjectScaleAtStartScale.y,
                        1f);

                    break;
                case "Scale Y":
                    // show guide
                    verticalLine.gameObject.SetActive(true);
                    verticalLine.SetPosition(0, selectedObject.transform.position + selectedObject.transform.up * 9999f);
                    verticalLine.SetPosition(1, selectedObject.transform.position - selectedObject.transform.up * 9999f);

                    newScale = new Vector3(selectedObjectScaleAtStartScale.x,
                        Mathf.Clamp(pointerScaleDeltaY * 2f + selectedObjectScaleAtStartScale.y, minimumScale, maximumScale), // * 2 since it's for both sides
                        1f);

                    break;
            }
            
            // apply scaling
            selectedObject.transform.localScale = newScale;
        }
    }

    #region Scale Object From Edge



    void HandleScaleSelectedObjectFromEdge()
    {

    }

    #endregion

    void HandleShowObjectTransformControls()
    {
        bool show = selectedObject != null && !(isTryingToMoveSelectedObject || isTryingToRotateSelectedObject || isTryingToScaleSelectedObject || isTryingToPlace);
        objectTransformControls.SetActive(show);
        closeObjectTransformControlsButton.SetActive(show);
        snapVerticalButton.SetActive(show);
        snapHorizontalButton.SetActive(show);
    }

    public void SwitchToPlayMode()
    {
        lastSelectedObject = null;
        selectedObject = null;
        HandleShowObjectTransformControls();

        startLocationIcon.SetActive(false);
        UIManager.Instance.HideAllUI();
        
        player.SetActive(true);
        player.transform.Find("Canvas").gameObject.SetActive(true);
        this.gameObject.SetActive(false);
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

    public void CloseObjectTransformControls()
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
