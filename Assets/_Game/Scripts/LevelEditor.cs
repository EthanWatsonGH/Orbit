using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LevelEditor : MonoBehaviour
{
    [SerializeField] Rigidbody2D rb;
    [SerializeField] GameObject prefabToPlace;
    [SerializeField] float viewMoveSpeed;
    [SerializeField] GameObject player;
    [SerializeField] GameObject startLocationIcon;

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

        // temp select
        if (Input.GetKeyDown(KeyCode.Q)) {
            SelectPuller();
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            SelectFinish();
        }
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
            startLocationIcon.gameObject.SetActive(false);

            player.SetActive(true);
            this.gameObject.SetActive(false);
        }
    }

    void HandlePlacePrefab()
    {
        // get mosue position
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;

        // start trying to place
        if (Input.GetButtonDown("Fire1") && prefabToPlace != null)
        {
            isTryingToPlace = true;
            lastPlacedLevelObject = Instantiate(prefabToPlace, mousePosition, Quaternion.identity, levelObjectsCollection.transform);
        }

        // make the object the player is currently trying to place follow the mouse
        if (isTryingToPlace && isTryingToPlace)
        {
            lastPlacedLevelObject.transform.position = mousePosition;
        }

        // stop following mouse and finish placing object
        if (Input.GetButtonUp("Fire1") && isTryingToPlace)
        {
            isTryingToPlace = false;
        }
    }

    public void ChangeCameraMoveSpeed()
    {
        viewMoveSpeed = cameraSpeedSlider.value;
    }

    // selected prefab switching with buttons
    public void SelectPuller()
    {
        prefabToPlace = pullerPrefab;
        Debug.Log("Selected Puller");
    }
    public void SelectFinish()
    {
        prefabToPlace = finishPrefab;
        Debug.Log("Selected Finish");
    }
    public void SelectKillWall()
    {
        prefabToPlace = killWallPrefab;
        Debug.Log("Selected Kill Wall");
    }
}
