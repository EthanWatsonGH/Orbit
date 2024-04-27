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
    bool isInLoseState = false;


    void Start()
    {
        lr.positionCount = 2;
        RetryLevel();
    }

    void Update()
    {
        if (true) // TODO: add a check to disallow doing these things while in pause menu
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
    }

    private void OnEnable()
    {
        isInWinState = false;
        RetryLevel();
    }

    void StartMovement()
    {
        if (isTryingToStartMovement) // TODO: improve this logic to only trigger once per retry
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

            // launch player in direction of startLaunchPoint
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
        Time.timeScale = 1f;

        // reset trail renderer
        tr.Clear();

        isTryingToStartMovement = true;
        isInWinState = false;
        isInLoseState = false;
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
            Time.timeScale = 1f;

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
            Time.timeScale = 0f;
            loseDisplay.gameObject.SetActive(true);
            isInLoseState = true;
        }
        // finish area
        if (collision.gameObject.CompareTag("Finish") && !isInLoseState)
        {
            winDisplay.gameObject.SetActive(true);
            isInWinState = true;
        }
    }
}
