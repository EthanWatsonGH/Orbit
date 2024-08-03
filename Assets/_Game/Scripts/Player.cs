using TMPro;
using UnityEngine;

public class Player : MonoBehaviour
{
    // self component references
    [SerializeField] Rigidbody2D rb;
    [SerializeField] TrailRenderer tr;
    [SerializeField] LineRenderer lr;
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
    [SerializeField] float startForce;
    Vector2 startDirection;
    float timeAtLastRetry;
    bool isInAimingMode = true;
    bool playerPressedStart = false; // TODO: change to events
    bool isInvincible = false;
    bool isInWinState = false;
    bool isInLoseState = false;
    bool canMoveLaunchDirectionPoint = false;

    void Start()
    {
        lr.positionCount = 2;
        RetryLevel();

        // ensure player UI is enabled
        canvas.gameObject.SetActive(true);
    }

    void Update()
    {
        if (true) // TODO: add a check to disallow doing these things while in pause menu
        {
            TryStartMovement();

            if (!isInAimingMode)
            {
                if (!isInWinState)
                {
                    // update time display
                    timeDisplay.text = (Time.time - timeAtLastRetry).ToString("F3");
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

    void TryStartMovement()
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
            if (Input.GetButtonDown("Jump") || playerPressedStart)
            {
                startDirection = (launchDirectionPoint.transform.position - rb.transform.position).normalized;
                rb.velocity = startDirection * startForce;

                // for timer calculation
                timeAtLastRetry = Time.time;

                isInAimingMode = false;
                playerPressedStart = false;
            }
        }
        else // in play mode
        {
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
        bool IsTouchOverLaunchDirectionPoint(Touch touch)
        {
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(touch.position), Vector2.zero);
            return hit.collider != null && hit.collider.gameObject == launchDirectionPoint;
        }

        bool IsMouseOverLaunchDirectionPoint()
        {
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            return hit.collider != null && hit.collider.gameObject == launchDirectionPoint;
        }

        // check if a touch just began over launchDirectionPoint
        if ((Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began && IsTouchOverLaunchDirectionPoint(Input.GetTouch(0))) // touchscreen
            || (Input.GetMouseButtonDown(0) && IsMouseOverLaunchDirectionPoint())) // desktop
            canMoveLaunchDirectionPoint = true;

        // when touch ends, reset ability to move start launch point
        if (Input.touchCount == 0 && !Input.GetMouseButton(0))
            canMoveLaunchDirectionPoint = false;

        // set launchDirectionPoint location to touch/mouse position
        if (canMoveLaunchDirectionPoint)
        {
            if (Input.touchCount == 1) // touchscreen
                launchDirectionPoint.transform.position = Camera.main.ScreenToWorldPoint(Input.GetTouch(0).position);

            if (Input.GetMouseButtonDown(0)) // desktop
                launchDirectionPoint.transform.position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        }
    }

    void HandleLaunchDirectionPointRotation()
    {
        // make launchDirectionPoint icon point away from the player
        // TODO: make it only update when the player is currently dragging the launchDirectionPoint
        Vector3 direction = launchDirectionPoint.transform.position - transform.position;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        launchDirectionPoint.transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }    

    void UpdateLineRenderer()
    {
        lr.SetPosition(0, this.gameObject.transform.position);
        lr.SetPosition(1, launchDirectionPoint.transform.position);
    }

    void RetryLevel()
    {
        isInAimingMode = true;
        isInWinState = false;
        isInLoseState = false;
        playerPressedStart = false;

        // ensure velocity is zero
        rb.velocity = Vector2.zero;

        // reset to start location, ensuring z = 0
        this.gameObject.transform.position = new Vector3(startLocation.transform.position.x, startLocation.transform.position.y, 0);
        // hide displays
        winDisplay.gameObject.SetActive(false);
        loseDisplay.gameObject.SetActive(false);

        // ensure unpause
        Time.timeScale = 1f;

        // reset trail renderer
        tr.Clear();
    }

    public void SwitchToLevelEditor()
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

    public void RecenterCamera()
    {
        Camera.main.transform.position = new Vector3(gameObject.transform.position.x, gameObject.transform.position.y, Camera.main.transform.position.z);
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        // pull area
        if (Input.GetAxisRaw("Fire1") > 0f && !isInAimingMode)
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

    public void PressedStart()
    {
        playerPressedStart = true;
    }
    public void PressedRetry()
    {
        RetryLevel();
    }    
}
