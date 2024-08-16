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

    // object rotation
    bool isTryingToRotateSelectedObject = false;
    float selectedObjectRotationAtStartRotate;
    float angleToPointerAtStartRotate;

    // object scaling
    bool isTryingToScaleSelectedObject;
    Vector3 pointerPositionAtStartScale;
    Vector3 selectedObjectScaleAtStartScale;
    Transform lastHitScaleControl;

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
        // ensure level editor object and all of its visuals are disabled before starting game
        gameObject.SetActive(false);
        objectTransformControls.SetActive(false);
    }

    void Start()
    {
        // ensure level editor UI is enabled
        canvas.gameObject.SetActive(true);

        

        
    }

    void Update()
    {
        HandlePlacePrefab();
        UpdatePointerPosition();
        HandleSelectObject();
        HandleScaleSelectedObject();
        HandleRotateSelectedObject();
        HandleMoveSelectedObject();
        EnsureObjectTransformControlsAlwaysInFront();
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

                // if player tries to place object over object selection bar, delete the object to cancel placement
                if (pointerIsOverObjectSelectionBar)
                {
                    Destroy(objectCurrentlyTryingToPlace);
                }
                else
                {
                    selectedObject = objectCurrentlyTryingToPlace;
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
                    closeObjectTransformControlsButton.SetActive(true);

                    AlignScaleControlsWithSelectedObject();

                    // hide certain controls for certain objects
                    bool isPlayerStartPoint = selectedObject.name == "PlayerStartPoint";
                    bool isPuller = selectedObject.name.Contains("Puller");

                    objectTransformControls.transform.Find("Duplicate").gameObject.SetActive(!isPlayerStartPoint);
                    objectTransformControls.transform.Find("Scale Both").gameObject.SetActive(!isPlayerStartPoint);
                    objectTransformControls.transform.Find("Scale X").gameObject.SetActive(!isPlayerStartPoint && !isPuller);
                    objectTransformControls.transform.Find("Scale Y").gameObject.SetActive(!isPlayerStartPoint && !isPuller);
                }
            }
            else // no object / background hit
            {
                // deselect current object, if any
                selectedObject = null;
                closeObjectTransformControlsButton.SetActive(false);
            }
        }

        // only show controls if something is selected
        objectTransformControls.SetActive(selectedObject != null);

        if (selectedObject != null)
        {
            objectTransformControls.transform.position = selectedObject.transform.position;
        }
    }

    void AlignScaleControlsWithSelectedObject()
    {
        objectTransformControls.transform.Find("Scale Both").transform.localRotation = selectedObject.transform.localRotation;
        objectTransformControls.transform.Find("Scale X").transform.localRotation = selectedObject.transform.localRotation;
        objectTransformControls.transform.Find("Scale Y").transform.localRotation = selectedObject.transform.localRotation * Quaternion.Euler(0f, 0f, 90f);
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
                //Debug.Log(hitName);

                if (hitName == "Move Both" || hitName == "Move X" || hitName == "Move Y" || hitName == "Duplicate")
                {
                    if (hitName == "Duplicate")
                    {
                        selectedObject = Instantiate(selectedObject, levelObjectsCollection.transform);
                    }

                    objectTransformControls.SetActive(false);

                    isTryingToMoveSelectedObject = true;
                    lastHitMoveControl = hit.transform;
                    selectedObjectPositionAtStartMove = selectedObject.transform.position;

                    // get offset between selected object and pointer position to keep it while moving
                    moveOffset = selectedObject.transform.position - pointerPosition;
                }
            }
        }

        // stop trying to move selected object when player releases
        // TODO: handle if they pause or exit edit mode while moving object, if that's still an issue later
        if (Input.GetButtonUp("Fire1") && isTryingToMoveSelectedObject)
        {
            objectTransformControls.SetActive(true);
            verticalLine.gameObject.SetActive(false);
            horizontalLine.gameObject.SetActive(false);

            isTryingToMoveSelectedObject = false;
            lastHitMoveControl = null;

            // if object is dropped while pointer is over object selection bar, destroy it
            if (selectedObject != null && pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint"))
            {
                Destroy(selectedObject);
                selectedObject = null;
                objectTransformControls.SetActive(false);
            }
        }

        if (Input.GetAxisRaw("Fire1") > 0 && selectedObject != null)
        {
            objectTransformControls.SetActive(false);

            if (isTryingToMoveSelectedObject && lastHitMoveControl != null)
            {
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
                    horizontalLine.SetPosition(0, new Vector3(horizontalLine.transform.position.x + 999999f, horizontalLine.transform.position.y, 0f));
                    horizontalLine.SetPosition(1, new Vector3(horizontalLine.transform.position.x - 999999f, horizontalLine.transform.position.y, 0f));
                }

                if (lastHitMoveControl.name == "Move Y")
                {
                    selectedObject.transform.position = new Vector3(selectedObjectPositionAtStartMove.x, selectedObject.transform.position.y, 0f);
                    verticalLine.gameObject.SetActive(true);
                    verticalLine.transform.position = selectedObject.transform.position;
                    verticalLine.SetPosition(0, new Vector3(verticalLine.transform.position.x, verticalLine.transform.position.y + 999999f, 0f));
                    verticalLine.SetPosition(1, new Vector3(verticalLine.transform.position.x, verticalLine.transform.position.y - 999999f, 0f));
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
                Debug.Log(hit.transform.name);

                objectTransformControls.SetActive(false);
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
            objectTransformControls.SetActive(true);
            rotationLine.gameObject.SetActive(false);

            // stop rotating
            isTryingToRotateSelectedObject = false;

            AlignScaleControlsWithSelectedObject();
        }

        if (isTryingToRotateSelectedObject) // do rotate
        {
            // TODO: may not need this once it get it all good on the movement side
            objectTransformControls.SetActive(false);

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
        if (Input.GetButtonDown("Fire1"))
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
                }
            }
        }
        if (Input.GetButtonUp("Fire1") && isTryingToScaleSelectedObject)
        {
            isTryingToScaleSelectedObject = false;
        }
        if (isTryingToScaleSelectedObject)
        {
            Vector3 newScale = selectedObject.transform.localScale;

            switch (lastHitScaleControl.name)
            {
                case "Scale Both":
                    float differenceX = pointerPositionAtStartScale.x - pointerPosition.x; // make it so that when the pointer moves up/left it scales the object up
                    float differenceY = pointerPosition.y - pointerPositionAtStartScale.y;

                    newScale = new Vector3(selectedObjectScaleAtStartScale.x + (differenceX + differenceY), selectedObjectScaleAtStartScale.y + (differenceX + differenceY), 1f);
                    break;
                case "Scale X":
                    differenceX = pointerPositionAtStartScale.x - pointerPosition.x; // make it so that when the pointer moves up/left it scales the object up
                    differenceY = pointerPositionAtStartScale.y - pointerPosition.y;

                    newScale = new Vector3(selectedObjectScaleAtStartScale.x + (differenceX + differenceY), selectedObjectScaleAtStartScale.y, 1f);
                    break;
                case "Scale Y":
                    differenceX = pointerPositionAtStartScale.x - pointerPosition.x; // make it so that when the pointer moves up/left it scales the object up
                    differenceY = pointerPosition.y - pointerPositionAtStartScale.y;

                    newScale = new Vector3(selectedObjectScaleAtStartScale.x, selectedObjectScaleAtStartScale.y + (differenceX + differenceY), 1f);
                    break;
            }

            selectedObject.transform.localScale = newScale;
        }
    }

    void EnsureObjectTransformControlsAlwaysInFront()
    {
        Vector3 objectTransformControlsPosition = new Vector3(objectTransformControls.transform.position.x, objectTransformControls.transform.position.y, -1f);
        objectTransformControls.transform.position = objectTransformControlsPosition;
    }

    public void SwitchToPlayMode()
    {
        // hide player start location icon
        startLocationIcon.SetActive(false);

        // ensure objectTransformControls are hidden
        objectTransformControls.SetActive(false);

        player.SetActive(true);
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
    public void LoadLevel()
    {
        LevelManager.Instance.LoadLevel();
    }
    public void DeleteAllLevelObjects()
    {
        LevelManager.Instance.DestroyAllExistingLevelObjects();
    }

    public void CloseObjectTransformControls()
    {
        objectTransformControls.SetActive(false);
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
