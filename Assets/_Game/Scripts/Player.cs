using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

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
    [SerializeField, FormerlySerializedAs("launchDirectionPoint")] Transform launchDirectionTarget;
    [SerializeField] GameObject launchButton;
    [SerializeField] GameObject retryButton;
    // HUD
    [SerializeField] TMP_Text timeDisplay;
    [SerializeField] TMP_Text speedDisplay;
    [SerializeField] TMP_Text winDisplay;
    [SerializeField] TMP_Text loseDisplay;
    // level object references
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
    [Header("Quick Retry Swipe")]
    [SerializeField, Min(0f)] float quickRetrySwipeMinimumDistancePixels = 150f;
    [SerializeField, Min(0f)] float quickRetrySwipeMaximumDurationSeconds = 0.35f;

    float timeAtLastLaunch;
    PlayerState state = PlayerState.Aiming;
    bool playerPressedLaunch; // TODO: change to events
    bool isInvincible;
    bool isDraggingLaunchDirectionTarget;
    Vector3 launchTargetDragOffset = Vector3.zero;
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

        HideFinishTrailRenderer();

        hasStarted = true;
        SyncQuickPlayToggleButtons();
        EnterAiming(false);
    }

    void Update()
    {
        if (UIManager.Instance.IsInControlBlockingMenu)
            return;

        if (WasQuickSwipeGesturePerformed())
        {
            if (state == PlayerState.Aiming)
                Launch();
            else
                EnterAiming(true);

            return;
        }

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

    bool WasQuickSwipeGesturePerformed()
    {
        PointerInput pointerInput = PointerInput.Instance;
        if (pointerInput == null)
            return false;

        return pointerInput.WasReleasedThisFrame
            && !pointerInput.WasCanceledThisFrame
            && !pointerInput.HadMultiplePointersDuringCurrentGesture
            && !pointerInput.WasPressedOverSelectableUi
            && !pointerInput.WasReleasedOverSelectableUi
            && pointerInput.PointerDurationSeconds <= quickRetrySwipeMaximumDurationSeconds
            && pointerInput.DragDistancePixels >= quickRetrySwipeMinimumDistancePixels;
    }

    void Launch()
    {
        if (state != PlayerState.Aiming)
            return;

        Vector2 launchDirection = launchDirectionTarget.position - rb.transform.position;
        rb.linearVelocity = launchDirection.normalized * launchForce;
        timeAtLastLaunch = Time.time;
        playerPressedLaunch = false;
        SetState(PlayerState.Playing);
    }

    void HideAimingGuides()
    {
        lr.enabled = false;
    }

    public bool BeginLaunchDirectionTargetDrag(Vector2 screenPosition)
    {
        if (state != PlayerState.Aiming || launchDirectionTarget == null ||
            !TryGetWorldPositionFromScreenPosition(screenPosition, out Vector3 pointerWorldPosition))
            return false;

        launchTargetDragOffset = launchDirectionTarget.position - pointerWorldPosition;
        isDraggingLaunchDirectionTarget = true;
        return true;
    }

    public void UpdateLaunchDirectionTargetDrag(Vector2 screenPosition)
    {
        if (!isDraggingLaunchDirectionTarget || launchDirectionTarget == null ||
            !TryGetWorldPositionFromScreenPosition(screenPosition, out Vector3 pointerWorldPosition))
            return;

        launchDirectionTarget.position = pointerWorldPosition + launchTargetDragOffset;
    }

    public void EndLaunchDirectionTargetDrag()
    {
        isDraggingLaunchDirectionTarget = false;
    }

    bool TryGetWorldPositionFromScreenPosition(Vector2 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (CameraViewManager.Instance == null ||
            !CameraViewManager.Instance.TryGetActiveWorldCamera(out Camera activeCamera))
            return false;

        float targetDepth = Mathf.Abs(launchDirectionTarget.position.z - activeCamera.transform.position.z);
        Vector3 convertedPosition = activeCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, targetDepth));
        worldPosition = new Vector3(convertedPosition.x, convertedPosition.y, launchDirectionTarget.position.z);
        return true;
    }

    void UpdateLineRenderer()
    {
        lr.SetPosition(0, transform.position);
        lr.SetPosition(1, launchDirectionTarget.position);
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

        UIManager.Instance.SwitchToLevelEditorMode();
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
        PointerInput pointerInput = PointerInput.Instance;
        if (pointerInput != null && pointerInput.IsSinglePointerHeld && state == PlayerState.Playing)
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
    }

    void HideInWorldUiElements()
    {
        lr.enabled = false;
    }

    public bool IsAiming => state == PlayerState.Aiming;
    public Vector3 LaunchDirectionTargetPosition => launchDirectionTarget != null ? launchDirectionTarget.position : transform.position;

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
