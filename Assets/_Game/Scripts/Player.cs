using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    // self component references
    [SerializeField] Rigidbody2D rb;
    [SerializeField] TrailRenderer tr;
    [SerializeField] LineRenderer lr;
    // self object references
    [SerializeField] GameObject startLaunchPoint;
    // HUD
    [SerializeField] TMP_Text timeDisplay;
    [SerializeField] TMP_Text speedDisplay;
    [SerializeField] TMP_Text winDisplay;
    [SerializeField] TMP_Text loseDisplay;
    // level object references
    [SerializeField] GameObject levelEditor;
    [SerializeField] GameObject startLocation;
    [SerializeField] GameObject startLocationIcon;
    // variables
    [SerializeField] float pullForce;
    [SerializeField] float startForce;
    Vector2 startDirection;
    float timeAtLastRetry;
    bool isTryingToStartMovement = true;
    bool isInvincible = false;
    bool isInWinState = false;
    public bool quickRetry = false;
    public bool quickLaunch = false;


    // Start is called before the first frame update
    void Start()
    {
        lr.positionCount = 2;
        RetryLevel();
    }

    // Update is called once per frame
    void Update()
    {
        StartMovement();

        UpdateLineRenderer();

        HandleRetryLevel();

        if (!isTryingToStartMovement)
        {
            // update time display
            timeDisplay.text = (Time.time - timeAtLastRetry).ToString("F3");
            // update speed display
            speedDisplay.text = rb.velocity.magnitude.ToString("F2");

            // hide start guides when not trying to start movement
            lr.enabled = false;
            startLaunchPoint.SetActive(false);
        }

        HandleSwitchToLevelEditor();
    }

    // called when player is set to active
    private void OnEnable()
    {
        // isInWinState = false;
        // TODO: may want to use this in the future for resetting things after coming out of level editor
    }

    // called when level is finished being laoded from a save file
    void OnLevelLoadFinished()
    {
        RetryLevel();    
    }

    void Pause()
    {
        Time.timeScale = 0.0f;
    }

    void StartMovement()
    {
        if (isTryingToStartMovement)
        {
            // reset HUD displays
            timeDisplay.text = "0";
            speedDisplay.text = "0";

            // show start guides
            lr.enabled = true;
            startLaunchPoint.SetActive(true);

            // set startLaunchPoint location to mouse position
            if (Input.GetAxisRaw("Fire1") > 0f)
            {
                Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mousePosition.z = 0;

                startLaunchPoint.transform.position = mousePosition;
            }

            // ensure velocity is zero
            rb.velocity = Vector2.zero;

            // launch player at startLaunchPoint
            if (Input.GetButtonDown("Jump"))
            {
                startDirection = (startLaunchPoint.transform.position - rb.transform.position).normalized;
                rb.velocity = startDirection * startForce;

                // set timeAtLastRetry for time display calculation
                timeAtLastRetry = Time.time;

                isTryingToStartMovement = false;
            }
        }
    }

    void UpdateLineRenderer()
    {
        lr.SetPosition(0, this.gameObject.transform.position);
        lr.SetPosition(1, startLaunchPoint.transform.position);
    }

    void HandleRetryLevel()
    {
        if (Input.GetButtonDown("Fire2"))
        {
            RetryLevel();
        }
    }

    void RetryLevel()
    {
        // reset to start location
        this.gameObject.transform.position = startLocation.transform.position;
        // hide displays
        winDisplay.gameObject.SetActive(false);
        loseDisplay.gameObject.SetActive(false);

        // ensure unpause
        Time.timeScale = 1.0f;

        // reset trail renderer
        tr.Clear();

        isTryingToStartMovement = true;
        isInWinState = false;
    }

    void HandleSwitchToLevelEditor()
    {
        if (Input.GetButtonDown("Fire3"))
        {
            RetryLevel();

            levelEditor.SetActive(true);
            this.gameObject.SetActive(false);

            // show player start location icon
            startLocationIcon.SetActive(true);

            // ensure unpause
            Time.timeScale = 1.0f;

            // ensure not in win state
            isInWinState = false;
        }
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        // pull area
        if (Input.GetAxisRaw("Fire1") > 0f && !isTryingToStartMovement)
        {
            if (collision.gameObject.CompareTag("Pull"))
            {
                Vector2 pullDirection = collision.transform.position - rb.transform.position;
                pullDirection.Normalize();
                rb.AddForce(pullDirection * pullForce, ForceMode2D.Force);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // kill area
        if (collision.gameObject.CompareTag("Kill") && !isInvincible && !isInWinState)
        {
            loseDisplay.gameObject.SetActive(true);
            Pause();
        }
        // finish area
        if (collision.gameObject.CompareTag("Finish"))
        {
            winDisplay.gameObject.SetActive(true);
            // Pause();
            isInWinState = true;
        }
    }
}
