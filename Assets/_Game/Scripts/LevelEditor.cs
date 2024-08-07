using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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
    Vector3 touchPosition;
    bool pointerIsOverObjectSelectionBar = false;
    GameObject selectedObject = null;
    bool isTryingToMoveSelectedObject = false;

    // object movement
    Vector3 moveControlOffsetFromParent;
    Vector3 moveOffset;

    float cameraZoomAtStart;

    readonly List<string> UNSELECTABLE_OBJECTS = new List<string> 
    {
        "X",
        "Y",
        "Both",
        "Rotate",
        "Close",
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
        cameraZoomAtStart = Camera.main.orthographicSize;

        // ensure level editor UI is enabled
        canvas.gameObject.SetActive(true);
    }

    void Update()
    {
        HandlePlacePrefab();
        GetTouchPosition();
        HandleSelectObject();
        HandleMoveSelectedObject();
        HandleScaleObjectTransformControlsWithZoom();
        EnsureObjectTransformControlsAlwaysInFront();
    }

    void GetTouchPosition()
    {
        if (Input.touchCount >= 1)
        {
            touchPosition = Camera.main.ScreenToWorldPoint(Input.GetTouch(0).position);
            // ensure no depth
            touchPosition.z = 0;
        }
    }

    void HandlePlacePrefab()
    {
        if (objectCurrentlyTryingToPlace != null && isTryingToPlace)
        {
            // make the object the player is currently trying to place follow the mouse
            objectCurrentlyTryingToPlace.transform.position = touchPosition;

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
                if (!UNSELECTABLE_OBJECTS.Contains(hit.collider.gameObject.transform.name)) // don't allow any object transform controls to be set as selected object
                {
                    selectedObject = hit.collider.gameObject;
                    objectTransformControls.transform.position = hit.transform.position;
                }
            }
            else // no object / background hit
            {
                // deselect current object, if any
                selectedObject = null;
                closeObjectTransformControlsButton.SetActive(false);
            }

            // only show controls if something is selected
            objectTransformControls.SetActive(selectedObject != null);
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
                moveOffset = moveControl.transform.position - touchPosition;
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
                moveControl.transform.position = touchPosition + moveOffset;
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

    void HandleScaleObjectTransformControlsWithZoom()
    {
        float currentCameraSize = Camera.main.orthographicSize;
        float cameraScaleRatio = cameraZoomAtStart / currentCameraSize;
        // invert so its bigger when zoomed out instead of smaller
        cameraScaleRatio = 1 / cameraScaleRatio;

        Vector3 newObjectTransformControlsScale = new Vector3(GameManager.Instance.UIScale * cameraScaleRatio, GameManager.Instance.UIScale * cameraScaleRatio, 1);
        objectTransformControls.transform.localScale = newObjectTransformControlsScale;
    }

    void EnsureObjectTransformControlsAlwaysInFront()
    {
        Vector3 objectTransformControlsPosition = new Vector3(objectTransformControls.transform.position.x, objectTransformControls.transform.position.y, -1f);
        objectTransformControls.transform.position = objectTransformControlsPosition;
    }

    void StartTryingToPlaceObject()
    {
        isTryingToPlace = true;
        objectCurrentlyTryingToPlace = Instantiate(prefabToPlace, touchPosition, Quaternion.identity, levelObjectsCollection.transform);
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
