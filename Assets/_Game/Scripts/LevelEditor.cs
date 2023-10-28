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

    GameObject levelObjectsCollection;

    [SerializeField] Slider cameraSpeedSlider;

    bool isTryingToPlace = false;
    GameObject objectCurrentlyTryingToPlace = null;
    Vector3 mousePosition;
    bool pointerIsOverObjectSelectionBar = false;

    // Start is called before the first frame update
    void Start()
    {
        levelObjectsCollection = GameObject.Find("LevelObjects");
    }

    // Update is called once per frame
    void Update()
    {
        HandleViewMovement();
        HandlePlacePrefab();
        HandleSwitchToPlayMode();
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

    void HandlePlacePrefab()
    {
        if (objectCurrentlyTryingToPlace != null && isTryingToPlace)
        {
            // get mouse position
            mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePosition.z = 0;

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

    public void ChangeCameraMoveSpeed()
    {
        viewMoveSpeed = cameraSpeedSlider.value;
    }

    public void TryToPlace()
    {
        isTryingToPlace = true;
        objectCurrentlyTryingToPlace = Instantiate(prefabToPlace, mousePosition, Quaternion.identity, levelObjectsCollection.transform);
    }

    // selected prefab switching with buttons
    public void PlacePuller()
    {
        prefabToPlace = pullerPrefab;
        Debug.Log("Selected Puller");
        TryToPlace();
    }
    public void PlaceFinish()
    {
        prefabToPlace = finishPrefab;
        Debug.Log("Selected Finish");
        TryToPlace();
    }
    public void PlaceKillWall()
    {
        prefabToPlace = killWallPrefab;
        Debug.Log("Selected Kill Wall");
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
