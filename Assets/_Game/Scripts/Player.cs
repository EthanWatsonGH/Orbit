using TMPro;
using UnityEngine;

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
    bool isInAimingMode = true;
    bool playerPressedStart = false; // TODO: change to events
    bool isInvincible = false;
    bool isInWinState = false;
    bool isInLoseState = false;
    bool canMoveStartLaunchPoint = false;

    void Start()
    {
        lr.positionCount = 2;
        RetryLevel();
    }

    void Update()
    {
        if (true) // TODO: add a check to disallow doing these things while in pause menu
        {
            TryStartMovement();

            UpdateLineRenderer();

            if (!isInAimingMode)
            {
                if (!isInWinState)
                {
                    // update time display
                    timeDisplay.text = (Time.time - timeAtLastRetry).ToString("F3");
                }

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

    void TryStartMovement()
    {
        // TODO: improve this logic to only trigger once per retry
        if (isInAimingMode)
        {
            // reset HUD displays
            timeDisplay.text = "0";
            speedDisplay.text = "0";

            // show aiming guides
            lr.enabled = true;
            startLaunchPoint.SetActive(true);

            bool IsTouchOverStartLaunchPoint(Touch touch)
            {
                RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(touch.position), Vector2.zero);

                if (hit.collider != null && hit.collider.gameObject == startLaunchPoint)
                {
                    return true;
                }

                return false;
            }

            // check if a touch just began over startLaunchPoint
            if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began && IsTouchOverStartLaunchPoint(Input.GetTouch(0)))
                canMoveStartLaunchPoint = true;

            // when touch ends, reset ability to move start launch point
            if (Input.touchCount == 0)
                canMoveStartLaunchPoint = false;

            //// Check if a touch just began over startLaunchPoint
            //if (Input.touchCount > 0)
            //{
            //    foreach (Touch touch in Input.touches)
            //    {
            //        if (touch.phase == TouchPhase.Began && IsTouchOverStartLaunchPoint(touch))
            //        {
            //            canMoveStartLaunchPoint = true;
            //            break; // Exit loop after finding one touch over startLaunchPoint
            //        }
            //    }
            //}
            //else
            //{
            //    canMoveStartLaunchPoint = false;
            //}

            Debug.Log(canMoveStartLaunchPoint.ToString());

            // set startLaunchPoint location to touch position
            if (!GameManager.Instance.touchPointIsOverButton && Input.touchCount == 1 && canMoveStartLaunchPoint)
            {
                Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mousePosition.z = 0;

                startLaunchPoint.transform.position = mousePosition;
            }

            // ensure velocity is zero
            rb.velocity = Vector2.zero;

            // launch player in direction of startLaunchPoint
            if (Input.GetButtonDown("Jump") || playerPressedStart)
            {
                startDirection = (startLaunchPoint.transform.position - rb.transform.position).normalized;
                rb.velocity = startDirection * startForce;

                // set timeAtLastRetry for time display calculation
                timeAtLastRetry = Time.time;

                isInAimingMode = false;
                playerPressedStart = false;
            }
        }
    }

    //bool IsTouchOverStartLaunchPoint(Touch touch)
    //{
    //    RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(touch.position), Vector2.zero);
    //    if (hit.collider != null && hit.collider.gameObject == startLaunchPoint)
    //    {
    //        return true;
    //    }
    //    return false;
    //}

    void UpdateLineRenderer()
    {
        lr.SetPosition(0, this.gameObject.transform.position);
        lr.SetPosition(1, startLaunchPoint.transform.position);
    }

    void RetryLevel()
    {
        // reset to start location, ensuring z = 0
        this.gameObject.transform.position = new Vector3(startLocation.transform.position.x, startLocation.transform.position.y, 0);
        // hide displays
        winDisplay.gameObject.SetActive(false);
        loseDisplay.gameObject.SetActive(false);

        // ensure unpause
        Time.timeScale = 1f;

        // reset trail renderer
        tr.Clear();

        isInAimingMode = true;
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
