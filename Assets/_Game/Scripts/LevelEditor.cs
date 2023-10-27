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
    GameObject lastPlacedLevelObject;
    Vector3 mousePosition;
    bool canPlaceObjectAtMousePos = false;

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
        // get mouse position
        mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;

        // make the object the player is currently trying to place follow the mouse
        // TODO only show when not over a UI element
        if (isTryingToPlace)
        {
            lastPlacedLevelObject.transform.position = mousePosition;
        }

        // stop following mouse and finish placing object
        if (Input.GetButtonUp("Fire1") && isTryingToPlace)
        {
            isTryingToPlace = false;
            // TODO
            if (/*is hovering over another ui element   es.IsPointerOverGameObject()*/ false)
            {
                Destroy(lastPlacedLevelObject);
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
        lastPlacedLevelObject = Instantiate(prefabToPlace, mousePosition, Quaternion.identity, levelObjectsCollection.transform);
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
}
