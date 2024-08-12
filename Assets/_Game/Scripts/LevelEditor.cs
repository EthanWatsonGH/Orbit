using System.Collections.Generic;
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

    // world object references
    [Header("World Objects")]
    [SerializeField] GameObject levelObjectsCollection;
    [SerializeField] GameObject objectTransformControls;
    [SerializeField] GameObject moveControl;

    bool isTryingToPlace = false;
    GameObject objectCurrentlyTryingToPlace = null;
    Vector3 pointerPosition;
    bool pointerIsOverObjectSelectionBar = false;
    GameObject selectedObject = null;

    // object movement
    Vector3 moveControlOffsetFromParent;
    Vector3 moveOffset;
    bool isTryingToMoveSelectedObject = false;

    // object rotation
    bool isTryingToRotateSelectedObject = false;
    float selectedObjectRotationAtStartRotate;
    float angleToPointerAtStartRotate;

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
        // ensure level editor object and all of it's visuals are disabled before starting game
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
        GetPointerPosition();
        HandleSelectObject();
        HandleMoveSelectedObject();
        HandleRotateSelectedObject();
        EnsureObjectTransformControlsAlwaysInFront();
    }

    void GetPointerPosition()
    {
        pointerPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

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

    void HandleMoveSelectedObject()
    {
        // start trying to move selected object when the player presses on move control
        if (Input.GetButtonDown("Fire1"))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.transform != null && hit.transform.gameObject.name.Equals("Move"))
            {
                isTryingToMoveSelectedObject = true;

                // set offset from parent to keep relative offset at all scales
                moveControlOffsetFromParent = moveControl.transform.position - objectTransformControls.transform.position;
                // set position of mouse at click for calculating offset for movement
                moveOffset = moveControl.transform.position - pointerPosition;
            }
        }

        // stop trying to move selected object when player releases
        // TODO: handle if they pause or exit edit mode while moving object, if that's still an issue later
        if (Input.GetButtonUp("Fire1"))
        {
            isTryingToMoveSelectedObject = false;

            // if object is dropped over selection bar, destroy it
            if (selectedObject != null && pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint"))
            {
                Destroy(selectedObject);
                selectedObject = null;
                objectTransformControls.SetActive(false);
            }
        }

        if (Input.GetAxisRaw("Fire1") > 0 && selectedObject != null)
        {
            if (isTryingToMoveSelectedObject)
            {
                // make moveControl follow mouse, with the offset from the exact location on click
                moveControl.transform.position = pointerPosition + moveOffset;
                // make objectTransformControls follow moveControl while keeping offset
                objectTransformControls.transform.position = moveControl.transform.position - moveControlOffsetFromParent;
                // make selectedObject follow objectTransformControls
                selectedObject.transform.position = objectTransformControls.transform.position;

                // if hovering over object selection bar, hide object placement preview and transform controls
                if (pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint")) // TODO: make it so you can remove player start point, but must have one before you can play the level?
                {
                    selectedObject.SetActive(false);
                    objectTransformControls.SetActive(false);
                }
                else
                {
                    selectedObject.SetActive(true);
                    objectTransformControls.SetActive(true);
                }
            }
        }
    }

    void HandleRotateSelectedObject()
    {
        void HideAllObjectTransformControlsChildren()
        {
            foreach (Transform child in objectTransformControls.transform)
            {
                child.gameObject.SetActive(false);
            }
        }

        void ShowAllObjectTransformControlsChildren()
        {
            foreach (Transform child in objectTransformControls.transform)
            {
                child.gameObject.SetActive(true);
            }
        }

        if (Input.GetButtonDown("Fire1")) // start
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.transform != null && hit.transform.IsChildOf(objectTransformControls.transform))
            {
                switch (hit.transform.name)
                {
                    case "Rotate":
                        // dont show any part of the object transform controls other than its line renderer
                        HideAllObjectTransformControlsChildren();
                        objectTransformControls.GetComponent<LineRenderer>().enabled = true;

                        // initiate rotation
                        isTryingToRotateSelectedObject = true;
                        
                        // remember values at start rotate to later make the rotation relative to the selected object's starting rotation
                        selectedObjectRotationAtStartRotate = selectedObject.transform.localEulerAngles.z;
                        // get the angle to the pointer when the player starts rotating the object
                        Vector3 direction = Camera.main.ScreenToWorldPoint(Input.mousePosition) - selectedObject.transform.position;
                        angleToPointerAtStartRotate = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        break;
                }
            }
        }
        if (Input.GetButtonUp("Fire1") && isTryingToRotateSelectedObject) // end
        {
            // show everything on object transform controls except its line renderer
            ShowAllObjectTransformControlsChildren();
            objectTransformControls.GetComponent<LineRenderer>().enabled = false;

            // stop rotating
            isTryingToRotateSelectedObject = false;
        }
        if (isTryingToRotateSelectedObject)
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
            objectTransformControls.GetComponent<LineRenderer>().SetPosition(0, objectTransformControls.transform.position);
            objectTransformControls.GetComponent<LineRenderer>().SetPosition(1, pointerPosition);
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
