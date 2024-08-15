using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class Player : MonoBehaviour
{
    // self component references
    [SerializeField] Rigidbody2D rb;
    [SerializeField] TrailRenderer tr;
    [SerializeField] LineRenderer lr;
    [SerializeField] TrailRenderer finishTrailRenderer;
    // self object references
    [SerializeField] GameObject launchDirectionPoint;
    [SerializeField] GameObject canvas;
    [SerializeField] GameObject launchButton;
    [SerializeField] GameObject retryButton;
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
    [SerializeField] float launchForce;
    float timeAtLastLaunch;
    bool isInAimingMode = true;
    bool playerPressedLaunch = false; // TODO: change to events
    bool isInvincible = false;
    bool isInWinState = false;
    bool isInLoseState = false;
    bool canMoveLaunchDirectionPoint = false;
    Vector3 offsetAtStartMoveLaunchDirectionPoint = Vector3.zero;
    float timeAtLastRetry;

    void Start()
    {
        lr.positionCount = 2;
        RetryLevel();

        // ensure player UI is enabled
        canvas.gameObject.SetActive(true);

        HideFinishTrailRenderer();
    }

    void Update()
    {
        if (true) // TODO: add a check to disallow doing these things while in pause menu
        {
            TryLaunch();

            if (!isInAimingMode)
            {
                if (!isInWinState)
                {
                    // update time display
                    timeDisplay.text = (Time.time - timeAtLastLaunch).ToString("F3");
                }

                // update speed display
                speedDisplay.text = rb.velocity.magnitude.ToString("F2");

                // hide launch direction UI when not in aiming mode
                lr.enabled = false;
                launchDirectionPoint.SetActive(false);
            }
        }
    }

    private void OnEnable()
    {
        isInWinState = false;
        RetryLevel();
    }

    void TryLaunch()
    {
        // TODO: improve this logic to only trigger once per retry
        if (isInAimingMode)
        {
            // show launch button instead of retry button
            launchButton.SetActive(true);
            retryButton.SetActive(false);

            // reset HUD displays
            timeDisplay.text = "0";
            speedDisplay.text = "0";

            // show aiming guides
            lr.enabled = true;
            launchDirectionPoint.SetActive(true);

            EnsureLaunchDirectionPointAlwaysInFront();
            HandleMoveLaunchDirectionPoint();
            HandleLaunchDirectionPointRotation();
            UpdateLineRenderer();

            // ensure velocity is zero
            rb.velocity = Vector2.zero;

            // launch player in direction of launchDirectionPoint when they press launch
            if ((Input.GetButtonDown("Jump") || playerPressedLaunch) && Time.time > timeAtLastRetry + 0.1f) // since launch and retry are on the same key, check a delay to not let them happen on the same frame
            {
                Vector2 launchDirection = launchDirectionPoint.transform.position - rb.transform.position;
                rb.velocity = launchDirection.normalized * launchForce;

                // for timer calculation
                timeAtLastLaunch = Time.time;

                isInAimingMode = false;
                playerPressedLaunch = false;
            }
        }
        else // in play mode
        {
            if (Input.GetButtonDown("Jump"))
            {
                RetryLevel();
            }

            // show retry button instead of launch button
            retryButton.SetActive(true);
            launchButton.SetActive(false);
        }
    }

    void EnsureLaunchDirectionPointAlwaysInFront()
    {
        Vector3 launchDirectionPointPosition = new Vector3(launchDirectionPoint.transform.position.x, launchDirectionPoint.transform.position.y, -1f);
        launchDirectionPoint.transform.position = launchDirectionPointPosition;
    }

    void HandleMoveLaunchDirectionPoint()
    {
        bool IsTouchOverLaunchDirectionPoint(Touch touch) // touchscreen
        {
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(touch.position), Vector2.zero);
            if (hit.collider != null && hit.collider.gameObject == launchDirectionPoint)
            {
                offsetAtStartMoveLaunchDirectionPoint = launchDirectionPoint.transform.position - Camera.main.ScreenToWorldPoint(touch.position);
                return true;
            }
            else
                return false;
        }

        bool IsMouseOverLaunchDirectionPoint() // desktop
        {
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if (hit.collider != null && hit.collider.gameObject == launchDirectionPoint)
            {
                offsetAtStartMoveLaunchDirectionPoint = launchDirectionPoint.transform.position - Camera.main.ScreenToWorldPoint(Input.mousePosition);
                return true;
            }
            else
                return false;
        }

        // check if a touch/click just began over launchDirectionPoint
        if ((Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began && IsTouchOverLaunchDirectionPoint(Input.GetTouch(0))) // touchscreen
            || (Input.GetMouseButtonDown(0) && IsMouseOverLaunchDirectionPoint())) // desktop
            canMoveLaunchDirectionPoint = true;

        // when touch ends, reset ability to move start launch point
        if (Input.touchCount == 0 && !Input.GetMouseButton(0))
            canMoveLaunchDirectionPoint = false;

        // set launchDirectionPoint location to pointer position
        if (canMoveLaunchDirectionPoint)
        {
            if (Input.touchCount == 1) // touchscreen
                launchDirectionPoint.transform.position = 
                    new Vector3(Camera.main.ScreenToWorldPoint(Input.GetTouch(0).position).x + offsetAtStartMoveLaunchDirectionPoint.x,
                    Camera.main.ScreenToWorldPoint(Input.GetTouch(0).position).y + offsetAtStartMoveLaunchDirectionPoint.y, 
                    launchDirectionPoint.transform.position.z);

            if (Input.GetMouseButton(0)) // desktop
                launchDirectionPoint.transform.position = 
                    new Vector3(Camera.main.ScreenToWorldPoint(Input.mousePosition).x + offsetAtStartMoveLaunchDirectionPoint.x,
                    Camera.main.ScreenToWorldPoint(Input.mousePosition).y + offsetAtStartMoveLaunchDirectionPoint.y,
                    launchDirectionPoint.transform.position.z);
        }
    }

    void HandleLaunchDirectionPointRotation()
    {
        // make launchDirectionPoint icon point away from the player
        if (canMoveLaunchDirectionPoint)
        {
            Vector3 direction = launchDirectionPoint.transform.position - transform.position;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            launchDirectionPoint.transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
        }
    }

    void UpdateLineRenderer()
    {
        lr.SetPosition(0, transform.position);
        lr.SetPosition(1, launchDirectionPoint.transform.position);
    }

    void RetryLevel()
    {
        timeAtLastRetry = Time.time;

        isInAimingMode = true;
        isInWinState = false;
        isInLoseState = false;
        playerPressedLaunch = false;

        // ensure velocity is zero
        rb.velocity = Vector2.zero;

        // reset to start location, ensuring z = 0
        gameObject.transform.position = new Vector3(startLocation.transform.position.x, startLocation.transform.position.y, 0f);

        // hide displays
        winDisplay.gameObject.SetActive(false);
        loseDisplay.gameObject.SetActive(false);

        // ensure unpause
        Time.timeScale = 1f;

        // reset trail renderers
        tr.Clear();
        finishTrailRenderer.Clear();

        HideFinishTrailRenderer();
    }

    public void SwitchToLevelEditor()
    {
        RetryLevel();
        
        // show player start location icon
        startLocationIcon.SetActive(true);

        levelEditor.SetActive(true);
        this.gameObject.SetActive(false);
    }

    void HideFinishTrailRenderer()
    {
        Color c = finishTrailRenderer.material.color;
        c.a = 0f;
        finishTrailRenderer.material.color = c;
    }

    void ShowFinishTrailRenderer()
    {
        Color c = finishTrailRenderer.material.color;
        c.a = 1f;
        finishTrailRenderer.material.color = c;
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        // pull area
        // TODO: subtract 1 touch for each one over a UI element, and then check if its still == 1 touch count
        if ((Input.GetMouseButton(0) || Input.touchCount == 1) && !isInAimingMode)
        {
            if (collision.gameObject.CompareTag("Pull"))
            {
                Vector2 pullDirection = collision.transform.position - rb.transform.position;
                rb.AddForce(pullDirection.normalized * pullForce, ForceMode2D.Force);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isInAimingMode) // in play mode
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
                Time.timeScale = 0f;
                winDisplay.gameObject.SetActive(true);
                isInWinState = true;
                ShowFinishTrailRenderer();
            }
        }
    }

    // TODO: change these to proper events
    public void PressedLaunch()
    {
        playerPressedLaunch = true;
    }
    public void PressedRetry()
    {
        RetryLevel();
    }    
}
