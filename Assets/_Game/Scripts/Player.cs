using TMPro;
using UnityEngine;

public enum PlayerState
{
    Aiming,
    Playing,
    Win,
    Lose
}

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
    [Header("Quick Play Settings")]
    [SerializeField] bool quickRetryEnabled;
    [SerializeField] bool quickLaunchEnabled;
    [SerializeField] ToggleButton quickRetryToggle;
    [SerializeField] ToggleButton quickLaunchToggle;

    float timeAtLastLaunch;
    PlayerState state = PlayerState.Aiming;
    bool playerPressedLaunch; // TODO: change to events
    bool isInvincible;
    bool canMoveLaunchDirectionPoint;
    Vector3 offsetAtStartMoveLaunchDirectionPoint = Vector3.zero;
    float timeAtLastRetry;
    bool hasStarted;

    public PlayerState State => state;

    void SetState(PlayerState newState)
    {
        state = newState;
    }

    void Start()
    {
        lr.positionCount = 2;

        // ensure player UI is enabled
        canvas.gameObject.SetActive(true);

        HideFinishTrailRenderer();

        hasStarted = true;
        SyncQuickPlayToggleButtons();
        EnterAiming(false);
    }

    void Update()
    {
        if (UIManager.Instance.IsInControlBlockingMenu)
            return;

        switch (state)
        {
            case PlayerState.Aiming:
                UpdateAiming();
                break;
            case PlayerState.Playing:
                UpdatePlaying();
                break;
            case PlayerState.Win:
            case PlayerState.Lose:
                UpdateFinishedAttempt();
                break;
        }
    }

    private void OnEnable()
    {
        EventManager.Instance.ShowPlayerInWorldUiElementsEvent.AddListener(ShowInWorldUiElements);
        EventManager.Instance.HidePlayerInWorldUiElementsEvent.AddListener(HideInWorldUiElements);
        EventManager.Instance.OnLevelLoadEvent.AddListener(EnterAimingAfterLevelLoad);

        if (hasStarted)
        {
            EnterAiming(false);
            SyncQuickPlayToggleButtons();
        }
    }

    private void OnDisable()
    {
        EventManager.Instance.ShowPlayerInWorldUiElementsEvent.RemoveListener(ShowInWorldUiElements);
        EventManager.Instance.HidePlayerInWorldUiElementsEvent.RemoveListener(HideInWorldUiElements);
        EventManager.Instance.OnLevelLoadEvent.RemoveListener(EnterAimingAfterLevelLoad);
    }

    void UpdateAiming()
    {
        launchButton.SetActive(true);
        retryButton.SetActive(false);

        timeDisplay.text = "0";
        speedDisplay.text = "0";

        lr.enabled = true;
        launchDirectionPoint.SetActive(true);

        EnsureLaunchDirectionPointAlwaysInFront();
        HandleMoveLaunchDirectionPoint();
        HandleLaunchDirectionPointRotation();
        UpdateLineRenderer();

        rb.linearVelocity = Vector2.zero;

        if (WasLaunchRequested() && Time.time > timeAtLastRetry + 0.1f) // launch and retry share input, so prevent both in the same frame
            Launch();
    }

    void UpdatePlaying()
    {
        timeDisplay.text = (Time.time - timeAtLastLaunch).ToString("F3");
        speedDisplay.text = rb.linearVelocity.magnitude.ToString("F2");

        retryButton.SetActive(true);
        launchButton.SetActive(false);
        HideAimingGuides();

        if (WasRetryRequested())
            EnterAiming(true);
    }

    void UpdateFinishedAttempt()
    {
        retryButton.SetActive(true);
        launchButton.SetActive(false);
        HideAimingGuides();

        if (WasRetryRequested())
            EnterAiming(true);
    }

    bool WasLaunchRequested()
    {
        return Input.GetButtonDown("Jump") || Input.GetMouseButtonDown(3) || Input.GetMouseButtonDown(4) || playerPressedLaunch;
    }

    bool WasRetryRequested()
    {
        return Input.GetButtonDown("Jump") || Input.GetMouseButtonDown(3) || Input.GetMouseButtonDown(4);
    }

    void Launch()
    {
        if (state != PlayerState.Aiming)
            return;

        Vector2 launchDirection = launchDirectionPoint.transform.position - rb.transform.position;
        rb.linearVelocity = launchDirection.normalized * launchForce;
        timeAtLastLaunch = Time.time;
        playerPressedLaunch = false;
        SetState(PlayerState.Playing);
    }

    void HideAimingGuides()
    {
        lr.enabled = false;
        launchDirectionPoint.SetActive(false);
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

    void EnterAimingAfterLevelLoad()
    {
        // A level change always waits for a manual first launch, even when Quick Launch is enabled.
        EnterAiming(false);
    }

    void EnterAiming(bool allowQuickLaunch)
    {
        timeAtLastRetry = Time.time;

        SetState(PlayerState.Aiming);
        playerPressedLaunch = false;

        // ensure velocity is zero
        rb.linearVelocity = Vector2.zero;

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

        if (allowQuickLaunch && quickLaunchEnabled)
            Launch();
    }

    public void SwitchToLevelEditor()
    {
        EnterAiming(false);
        
        // show player start location icon
        startLocationIcon.SetActive(true);

        UIManager.Instance.HideAllUI();
        levelEditor.SetActive(true);
        levelEditor.transform.Find("Canvas").gameObject.SetActive(true);
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
        if ((Input.GetMouseButton(0) || Input.touchCount == 1) && state == PlayerState.Playing)
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
        if (state == PlayerState.Playing)
        {
            // kill area
            if (collision.gameObject.CompareTag("Kill") && !isInvincible)
            {
                Lose();
                return;
            }
            // finish area
            if (collision.gameObject.CompareTag("Finish"))
            {
                Win();
            }
        }
    }

    void Win()
    {
        if (state != PlayerState.Playing)
            return;

        SetState(PlayerState.Win);
        Time.timeScale = 0f;
        winDisplay.gameObject.SetActive(true);
        ShowFinishTrailRenderer();
    }

    void Lose()
    {
        if (state != PlayerState.Playing)
            return;

        if (quickRetryEnabled)
        {
            EnterAiming(true);
            return;
        }

        SetState(PlayerState.Lose);
        Time.timeScale = 0f;
        loseDisplay.gameObject.SetActive(true);
    }

    void ShowInWorldUiElements()
    {
        lr.enabled = true;
        launchDirectionPoint.SetActive(true);
    }

    void HideInWorldUiElements()
    {
        lr.enabled = false;
        launchDirectionPoint.SetActive(false);
    }

    public void PressedLaunch()
    {
        playerPressedLaunch = true;
    }

    public void PressedRetry()
    {
        EnterAiming(true);
    }

    public void SetQuickRetryEnabled(bool isEnabled)
    {
        quickRetryEnabled = isEnabled;
        if (quickRetryToggle != null)
            quickRetryToggle.SetIsOn(quickRetryEnabled);
    }

    public void SetQuickLaunchEnabled(bool isEnabled)
    {
        quickLaunchEnabled = isEnabled;
        if (quickLaunchToggle != null)
            quickLaunchToggle.SetIsOn(quickLaunchEnabled);
    }

    void SyncQuickPlayToggleButtons()
    {
        if (quickRetryToggle != null)
            quickRetryToggle.SetIsOn(quickRetryEnabled);
        if (quickLaunchToggle != null)
            quickLaunchToggle.SetIsOn(quickLaunchEnabled);
    }
}
