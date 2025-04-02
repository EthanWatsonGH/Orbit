using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

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
    [SerializeField] LineRenderer verticalLine;
    [SerializeField] LineRenderer horizontalLine;
    [SerializeField] LineRenderer rotationLine;

    // world object references
    [Header("World Objects")]
    [SerializeField] GameObject levelObjectsCollection;
    [SerializeField] GameObject objectTransformControls;

    bool isTryingToPlace = false;
    GameObject objectCurrentlyTryingToPlace = null;
    Vector3 pointerPosition;
    bool pointerIsOverObjectSelectionBar = false;
    GameObject selectedObject = null;

    // object movement
    Vector3 moveOffset;
    bool isTryingToMoveSelectedObject = false;
    Transform lastHitMoveControl;
    Vector3 selectedObjectPositionAtStartMove;
    Vector3 pointerPositionAtStartMove;

    // object rotation
    bool isTryingToRotateSelectedObject = false;
    float selectedObjectRotationAtStartRotate;
    float angleToPointerAtStartRotate;

    // object scaling
    bool isTryingToScaleSelectedObject = false;
    Vector3 pointerPositionAtStartScale;
    Vector3 selectedObjectScaleAtStartScale;
    Transform lastHitScaleControl;
    float minimumScale = 0.2f;
    float maximumScale = 999999f;
    float selectedObjectXScaleAtStartScale;
    float selectedObjectYScaleAtStartScale;

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
    }

    void Start()
    {
        // ensure level editor object and all of its visuals are disabled before starting game
        // TODO: move this back to awake once i add real buttons. this is also to initialize button scaling
        gameObject.SetActive(false);

        // ensure level editor UI is enabled
        canvas.gameObject.SetActive(true);

        objectTransformControls.SetActive(false);
    }

    void Update()
    {
        HandlePlacePrefab();
        UpdatePointerPosition();
        HandleSelectObject();
        HandleScaleSelectedObject();
        HandleRotateSelectedObject();
        HandleMoveSelectedObject();
        HandleShowObjectTransformControls();
    }

    void UpdatePointerPosition()
    {
        pointerPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // TODO: i dont like this because when there's more than 1 finger the pointerPosition will jump between the fingers. just make each funtion handle touch and mouse position independently on their own
        if (Input.touchCount >= 1)
        {
            pointerPosition = Camera.main.ScreenToWorldPoint(Input.GetTouch(0).position);
        }

        // ensure no depth
        pointerPosition.z = 0;
    }

    void HandlePlacePrefab()
    {
        if (objectCurrentlyTryingToPlace != null && isTryingToPlace)
        {
            // make the object the player is currently trying to place follow the pointer
            objectCurrentlyTryingToPlace.transform.position = pointerPosition;

            // dont show object that is currently trying to be placed when over object selection bar
            if (pointerIsOverObjectSelectionBar)
                objectCurrentlyTryingToPlace.SetActive(false);
            else
                objectCurrentlyTryingToPlace.SetActive(true);

            // stop following mouse and finish placing object
            if (Input.GetButtonUp("Fire1") && isTryingToPlace)
            {
                isTryingToPlace = false;

                if (pointerIsOverObjectSelectionBar) // if player tries to place object over object selection bar, delete the object to cancel placement
                {
                    Destroy(objectCurrentlyTryingToPlace);
                }
                else // place object
                {
                    selectedObject = objectCurrentlyTryingToPlace;
                    SetWhichObjectTransformControlsToShow();
                    AlignScaleControlsWithSelectedObject();
                    SetMinimumScale();
                }

                objectCurrentlyTryingToPlace = null;
            }
        }
    }

    void HandleSelectObject()
    {
        // set the object the player clicks as selected if it's allowed to be selected
        if (Input.GetButtonDown("Fire1"))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.collider != null) // object hit
            {
                if (!UNSELECTABLE_OBJECTS.Contains(hit.collider.gameObject.transform.name)) // don't allow any UI objects to be set as selected object
                {
                    selectedObject = hit.collider.gameObject;

                    AlignScaleControlsWithSelectedObject();
                    SetWhichObjectTransformControlsToShow();
                    SetMinimumScale();
                }
            }
            else // no object / background hit
            {
                // deselect current object, if any
                selectedObject = null;
            }
        }

        if (selectedObject != null)
        {
            objectTransformControls.transform.position = new Vector3(selectedObject.transform.position.x, selectedObject.transform.position.y, objectTransformControls.transform.position.z);
        }
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

    void HandleMoveSelectedObject()
    {
        
        // start trying to move selected object when the player presses on move control
        if (Input.GetButtonDown("Fire1"))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.transform != null)
            {
                string hitName = hit.transform.name;

                if (hitName == "Move Both" || hitName == "Move X" || hitName == "Move Y" || hitName == "Duplicate")
                {
                    if (hitName == "Duplicate")
                    {
                        selectedObject = Instantiate(selectedObject, levelObjectsCollection.transform);
                        selectedObject.transform.name = selectedObject.transform.name.Replace("(Clone)", "");
                    }

                    isTryingToMoveSelectedObject = true;
                    lastHitMoveControl = hit.transform;
                    selectedObjectPositionAtStartMove = selectedObject.transform.position;
                    pointerPositionAtStartMove = pointerPosition;

                    // get offset between selected object and pointer position to keep it while moving
                    moveOffset = selectedObject.transform.position - pointerPosition;
                }
            }
        }

        // stop trying to move selected object when player releases
        // TODO: handle if they pause or exit edit mode while moving object, if that's still an issue later
        if (Input.GetButtonUp("Fire1") && isTryingToMoveSelectedObject)
        {
            verticalLine.gameObject.SetActive(false);
            horizontalLine.gameObject.SetActive(false);

            isTryingToMoveSelectedObject = false;
            lastHitMoveControl = null;

            // if object is dropped while pointer is over object selection bar, destroy it
            if (selectedObject != null && pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint"))
            {
                Destroy(selectedObject);
                selectedObject = null;
                closeObjectTransformControlsButton.gameObject.SetActive(false);
            }
        }

        if (Input.GetAxisRaw("Fire1") > 0 && selectedObject != null)
        {
            if (isTryingToMoveSelectedObject && lastHitMoveControl != null)
            {
                // to prevent the object from moving when the player is trying to just duplicate it, don't move the object when the pointer has only moved a small distance
                if (lastHitMoveControl.name == "Duplicate" && Vector3.Distance(pointerPositionAtStartMove, pointerPosition) < 0.2f)
                    selectedObject.transform.position = selectedObjectPositionAtStartMove;
                else
                    // make selectedObject move with pointer
                    selectedObject.transform.position = pointerPosition + moveOffset;

                // if hovering over object selection bar, hide object placement preview and transform controls
                if (pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint")) // TODO: make it so you can remove player start point, but must have one before you can play the level?
                {
                    selectedObject.SetActive(false);
                }
                else // not hovering pointer over object selection bar
                {
                    selectedObject.SetActive(true);
                }

                if (lastHitMoveControl.name == "Move X")
                {
                    selectedObject.transform.position = new Vector3(selectedObject.transform.position.x, selectedObjectPositionAtStartMove.y, 0f);
                    horizontalLine.gameObject.SetActive(true);
                    horizontalLine.transform.position = selectedObject.transform.position;
                    horizontalLine.SetPosition(0, new Vector3(horizontalLine.transform.position.x + 9999f, horizontalLine.transform.position.y, 0f));
                    horizontalLine.SetPosition(1, new Vector3(horizontalLine.transform.position.x - 9999f, horizontalLine.transform.position.y, 0f));
                }

                if (lastHitMoveControl.name == "Move Y")
                {
                    selectedObject.transform.position = new Vector3(selectedObjectPositionAtStartMove.x, selectedObject.transform.position.y, 0f);
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
        if (Input.GetButtonDown("Fire1")) // start rotate
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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
                Vector3 direction = Camera.main.ScreenToWorldPoint(Input.mousePosition) - selectedObject.transform.position;
                angleToPointerAtStartRotate = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            }
        }

        if (Input.GetButtonUp("Fire1") && isTryingToRotateSelectedObject) // end rotate
        {
            rotationLine.gameObject.SetActive(false);

            // stop rotating
            isTryingToRotateSelectedObject = false;

            AlignScaleControlsWithSelectedObject();
        }

        if (isTryingToRotateSelectedObject) // do rotate
        {
            // get rotation to current pointer position
            Vector3 direction = Camera.main.ScreenToWorldPoint(Input.mousePosition) - selectedObject.transform.position;
            float currentAngleToPointer = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // get the difference between current angle to pointer and the angle to pointer when the player started rotating
            float deltaAngle = currentAngleToPointer - angleToPointerAtStartRotate;

            // add the difference between start and end rotate to the selected object's rotation when the player started rotating
            float newRotation = selectedObjectRotationAtStartRotate + deltaAngle;

            // apply new rotation to selected object
            selectedObject.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, newRotation));

            // update line renderer position
            rotationLine.SetPosition(1, pointerPosition);
        }
    }

    void HandleScaleSelectedObject()
    {
        if (Input.GetButtonDown("Fire1")) // start scale
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.transform != null)
            {
                string hitName = hit.transform.name;

                if (hitName == "Scale Both" || hitName == "Scale X" || hitName == "Scale Y")
                {
                    isTryingToScaleSelectedObject = true;
                    pointerPositionAtStartScale = pointerPosition;
                    selectedObjectScaleAtStartScale = selectedObject.transform.localScale;
                    lastHitScaleControl = hit.transform;
                    selectedObjectXScaleAtStartScale = selectedObject.transform.localScale.x;
                    selectedObjectYScaleAtStartScale = selectedObject.transform.localScale.y;
                }
            }
        }
        if (Input.GetButtonUp("Fire1") && isTryingToScaleSelectedObject) // end scale
        {
            isTryingToScaleSelectedObject = false;

            // hide guide
            horizontalLine.gameObject.SetActive(false);
            verticalLine.gameObject.SetActive(false);
        }
        if (isTryingToScaleSelectedObject) // do scaling
        {
            Vector3 newScale = selectedObject.transform.localScale;

            // get scaling to add/remove depending on pointer movement
            // TODO: change this to be the distance directly towards/away from the selected object instead of this absolute position method
            float differenceX = pointerPositionAtStartScale.x - pointerPosition.x;
            float differenceY = pointerPosition.y - pointerPositionAtStartScale.y;

            //float scaleToAdd = differenceX + differenceY;

            //float xMultiplier = scaleToAdd / selectedObjectXScaleAtStartScale;
            //float yMultiplier = scaleToAdd / selectedObjectYScaleAtStartScale;

            // get scaling depending on which scale control was pressed
            switch (lastHitScaleControl.name)
            {
                case "Scale Both":
                    // get object width/magnitude at start scale, eg. 17m
                    // get distance from pointer movement to add as width, eg. 2m
                    // divide width to add by object width at start, eg. 2 / 17 = 0.11764...
                    // multiply object width at start by (1 + value of previous calculation), eg. (1 + 0.11764...) * 17 = 19

                    // or get aspect ratio between each axis, and scale each with percentage multiplied by aspect ratio

                    // if i scale one axis of an object, lets say the bigger one is the x axis and it's 13m at start scale, and i add 2.5m distance to it in the scaling,
                    //  then whatever percentage it just increased should be applied to the other smaller axis. say the y axis, the smaller axis, is 7m,
                    //  the x axis increased by (2.5 / 13 = 0.1923...) so the y axis should be increasing by the same percentage.
                    //  i dont even have to do any adding of the movement value to the smaller axis, just multiplying by the same percent

                    // for getting the proper amount of length to add/subtract:
                    // get pointer position at start scale
                    // get selected object position
                    // in the direction from pointer position at start scale to selected object position, create a vector 9999f away from selected object
                    Vector3 scaleToPoint = pointerPositionAtStartScale + selectedObject.transform.right * 9999f;
                    // get distance between pointer position at start scale and the created vector
                    float distanceAtStartScale = Vector3.Distance(pointerPositionAtStartScale, scaleToPoint);
                    // get difference between last calculation and (distance between current pointer position and created vector)
                    float scaleToAdd = (Vector3.Distance(pointerPosition, scaleToPoint) - distanceAtStartScale) * 2f; // * 2 since it needs to add the length to both sides

                    float xMultiplier = scaleToAdd / selectedObjectXScaleAtStartScale;
                    float yMultiplier = scaleToAdd / selectedObjectYScaleAtStartScale;

                    float parentXScale = selectedObject.transform.parent.localScale.x;
                    float parentYScale = selectedObject.transform.parent.localScale.y;

                    if (selectedObjectXScaleAtStartScale > selectedObjectYScaleAtStartScale) // x width bigger than y width
                    {
                        float xAxisScaleDifferenceSinceStartScale = selectedObject.transform.localScale.x / selectedObjectXScaleAtStartScale;

                        newScale = new Vector3(Mathf.Clamp((1 + xMultiplier / parentXScale) * selectedObjectXScaleAtStartScale, minimumScale / parentXScale, maximumScale / parentXScale),
                        Mathf.Clamp(selectedObjectYScaleAtStartScale * xAxisScaleDifferenceSinceStartScale, minimumScale / parentYScale, maximumScale / parentYScale),
                        1f);
                    }
                    else if (selectedObjectYScaleAtStartScale > selectedObjectXScaleAtStartScale) // y width bigger than x width
                    {
                        float yAxisScaleDifferenceSinceStartScale = selectedObject.transform.localScale.y / selectedObjectYScaleAtStartScale;

                        newScale = new Vector3(Mathf.Clamp(selectedObjectXScaleAtStartScale * yAxisScaleDifferenceSinceStartScale, minimumScale * parentXScale, maximumScale * parentXScale),
                        Mathf.Clamp((1 + yMultiplier / parentYScale) * selectedObjectYScaleAtStartScale, minimumScale / parentYScale, maximumScale / parentYScale),
                        1f);
                    }
                    else // x and y width equal. so square or circular objects
                    {
                        newScale = new Vector3(Mathf.Clamp((1 + xMultiplier / parentXScale) * selectedObjectXScaleAtStartScale, minimumScale / parentXScale, maximumScale / parentXScale),
                        Mathf.Clamp((1 + yMultiplier / parentYScale) * selectedObjectYScaleAtStartScale, minimumScale / parentYScale, maximumScale / parentYScale),
                        1f);

                        // TODO: this is a cool bug that i could turn into a feature
                        //float differenceX = pointerPositionAtStartScale.x - pointerPosition.x; // these 2 lines are here just in case i change the code for them above and need this to check the bug later
                        //float differenceY = pointerPosition.y - pointerPositionAtStartScale.y;
                        //newScale = new Vector3(Mathf.Clamp(differenceX * 2f + selectedObjectScaleAtStartScale.x, minimumScale, maximumScale), // * 2 since it's for both sides
                        //Mathf.Clamp(differenceY * 2f + selectedObjectScaleAtStartScale.y, minimumScale, maximumScale),
                        //1f);
                    }

                    break;
                case "Scale X":
                    // show guide
                    horizontalLine.gameObject.SetActive(true);
                    horizontalLine.SetPosition(0, selectedObject.transform.position + selectedObject.transform.right * 9999f);
                    horizontalLine.SetPosition(1, selectedObject.transform.position - selectedObject.transform.right * 9999f);

                    newScale = new Vector3(Mathf.Clamp(differenceX * 2f + selectedObjectScaleAtStartScale.x, minimumScale, maximumScale), // * 2 since it's for both sides
                        selectedObjectScaleAtStartScale.y,
                        1f);

                    break;
                case "Scale Y":
                    // show guide
                    verticalLine.gameObject.SetActive(true);
                    verticalLine.SetPosition(0, selectedObject.transform.position + selectedObject.transform.up * 9999f);
                    verticalLine.SetPosition(1, selectedObject.transform.position - selectedObject.transform.up * 9999f);

                    newScale = new Vector3(selectedObjectScaleAtStartScale.x,
                        Mathf.Clamp(differenceY * 2f + selectedObjectScaleAtStartScale.y, minimumScale, maximumScale), // * 2 since it's for both sides
                        1f);

                    break;
            }
            
            // apply scaling
            selectedObject.transform.localScale = newScale;
        }
    }

    void HandleShowObjectTransformControls()
    {
        bool show = selectedObject != null && !(isTryingToMoveSelectedObject || isTryingToRotateSelectedObject || isTryingToScaleSelectedObject);
        objectTransformControls.SetActive(show);
        closeObjectTransformControlsButton.SetActive(show);
    }

    public void SwitchToPlayMode()
    {
        // hide player start location icon
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
        objectCurrentlyTryingToPlace = Instantiate(prefabToPlace, pointerPosition, Quaternion.identity, levelObjectsCollection.transform);
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
        selectedObject = null;
    }

    public void SwitchToLocalTransformMode()
    {
        // change to opposite button
        worldTransformButton.SetActive(false);
        localTransformButton.SetActive(true);
    }

    public void SwitchToWorldTransformMode()
    {
        // change to opposite button
        localTransformButton.SetActive(false);
        worldTransformButton.SetActive(true);
    }
}
