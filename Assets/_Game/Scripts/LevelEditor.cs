using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LevelEditor : MonoBehaviour
{
    [SerializeField] Rigidbody2D rb;
    [SerializeField] GameObject prefabToPlace;
    [SerializeField] float viewMoveSpeed;
    [SerializeField] GameObject player;
    [SerializeField] GameObject startLocationIcon;
    [SerializeField] EventSystem es;

    // level editor pieces prefab refs
    [Header("Level Editor Pieces")]
    [SerializeField] GameObject pullerPrefab;
    [SerializeField] GameObject killWallPrefab;
    [SerializeField] GameObject killCirclePrefab;
    [SerializeField] GameObject finishPrefab;

    [SerializeField] GameObject levelObjectsCollection;
    [SerializeField] GameObject objectTransformControls;
    [SerializeField] GameObject moveControl;

    [SerializeField] Slider cameraSpeedSlider;

    bool isTryingToPlace = false;
    GameObject objectCurrentlyTryingToPlace = null;
    Vector3 mousePosition;
    bool pointerIsOverObjectSelectionBar = false;
    GameObject selectedObject = null;
    bool isTryingToMoveSelectedObject = false;
    Vector3 mousePositionAtClick;

    void Awake()
    {
        // ensure level editor object and all of it's visuals are disabled before starting game
        gameObject.SetActive(false);
        objectTransformControls.SetActive(false);
    }

    void Start()
    {
        
    }

    void Update()
    {
        HandleViewMovement();
        HandlePlacePrefab();
        HandleSwitchToPlayMode();
        GetMousePosition();
        HandleSelectObject();
        HandleMoveSelectedObject();
    }

    void HandleViewMovement()
    {
        rb.velocity = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) * viewMoveSpeed;
    }

    void HandleSwitchToPlayMode()
    {
        if (Input.GetButtonDown("Fire3"))
        {
            // hide player start location icon
            startLocationIcon.SetActive(false);

            // ensure objectTransformControls are hidden
            objectTransformControls.SetActive(false);

            player.SetActive(true);
            this.gameObject.SetActive(false);
        }
    }

    void GetMousePosition()
    {
        mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        // ensure no depth
        mousePosition.z = 0;
    }

    void HandlePlacePrefab()
    {
        if (objectCurrentlyTryingToPlace != null && isTryingToPlace)
        {
            // make the object the player is currently trying to place follow the mouse
            objectCurrentlyTryingToPlace.transform.position = mousePosition;

            // dont show object that is currently trying to be placed when over a ui element
            if (pointerIsOverObjectSelectionBar)
                objectCurrentlyTryingToPlace.SetActive(false);
            else
                objectCurrentlyTryingToPlace.SetActive(true);

            // stop following mouse and finish placing object
            if (Input.GetButtonUp("Fire1") && isTryingToPlace)
            {
                isTryingToPlace = false;

                // if player tries to place object over ui element, delete the object to cancel placement
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
        if (Input.GetButtonDown("Fire1"))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.collider != null)
            {
                if (!hit.collider.gameObject.transform.parent.name.Equals("Scale") && !hit.collider.gameObject.transform.parent.name.Equals("ObjectTransformControls")) // don't allow object transform controls to be set as selected object
                {
                    selectedObject = hit.collider.gameObject;
                    objectTransformControls.transform.position = hit.transform.position;
                }
            }
            else
            {
                selectedObject = null;
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
            Debug.Log("1");
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.transform.gameObject.name.Equals("Move"))
            {
                isTryingToMoveSelectedObject = true;

                // set position of mouse at click for calculating offset for movement
                mousePositionAtClick = mousePosition;
            }
        }

        // stop trying to move selected object when player releases
        if (Input.GetButtonUp("Fire1"))
        {
            Debug.Log("2");
            isTryingToMoveSelectedObject = false;
            if (pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint"))
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
                Vector3 objectTransformControlsPosition = objectTransformControls.transform.position;
                Vector3 moveControlPosition = moveControl.transform.position;
                Vector3 childOffset = objectTransformControlsPosition - moveControlPosition;

                // TODO: this stuff is jank and there's probably way of doing it

                // calculate offset from where exactly the player clicked to the position of the move control when they click on it to ensure the object only moves relative to how much they move the cursor
                //Vector3 moveOffset = moveControlPosition - mousePositionAtClick;

                // set position of move control
                moveControl.transform.position = mousePosition;
                // apply offest to only move with cursor
                //moveControl.transform.position += moveOffset;
                // move parent to child position
                objectTransformControlsPosition = moveControl.transform.position;
                // offset to keep relative distance
                //objectTransformControlsPosition += childOffset;
                // make selectedObject follow objectTransformControls
                selectedObject.transform.position = objectTransformControlsPosition;
                // make parent follow selected object
                objectTransformControls.transform.position = selectedObject.transform.position;

                if (pointerIsOverObjectSelectionBar && !selectedObject.name.Equals("PlayerStartPoint"))
                {
                    selectedObject.SetActive(false);
                }
                else
                {
                    selectedObject.SetActive(true);
                }
            }
        }
    }

    void ChangeCameraMoveSpeed()
    {
        viewMoveSpeed = cameraSpeedSlider.value;
    }

    void TryToPlace()
    {
        isTryingToPlace = true;
        objectCurrentlyTryingToPlace = Instantiate(prefabToPlace, mousePosition, Quaternion.identity, levelObjectsCollection.transform);
    }

    // place events for each button
    public void PlacePuller()
    {
        prefabToPlace = pullerPrefab;
        TryToPlace();
    }
    public void PlaceFinish()
    {
        prefabToPlace = finishPrefab;
        TryToPlace();
    }
    public void PlaceKillWall()
    {
        prefabToPlace = killWallPrefab;
        TryToPlace();
    }

    // update value of pointerIsOverObjectSelectionBar on pointer enter and exit
    public void SetPointerIsOverObjectSelectionBarTrue()
    {
        pointerIsOverObjectSelectionBar = true;
    }
    public void SetPointerIsOverObjectSelectionBarFalse()
    {
        pointerIsOverObjectSelectionBar = false;
    }
}
