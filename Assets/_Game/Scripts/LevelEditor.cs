using System.Collections;
using System.Collections.Generic;
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

    [SerializeField] Slider cameraSpeedSlider;

    bool isTryingToPlace = false;
    GameObject objectCurrentlyTryingToPlace = null;
    Vector3 mousePosition;
    bool pointerIsOverObjectSelectionBar = false;
    GameObject selectedObject = null;


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
        HandleShowObjectTransformControls();
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
            Debug.Log("Trying to select object");
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Debug.Log("Hit" + hit);
                selectedObject = hit.transform.gameObject;
            }
            else
            {
                selectedObject = null;
            }
        }
    }

    void HandleShowObjectTransformControls()
    {
        objectTransformControls.SetActive(selectedObject != null);
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
