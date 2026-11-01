using System.Collections.Generic;
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
    static readonly int LevelInteractionHeldShaderId = Shader.PropertyToID("_LevelInteractionHeld");

    // self component references
    [SerializeField] Rigidbody2D rb;
    [SerializeField] TrailRenderer tr;
    [SerializeField] LineRenderer lr;
    [SerializeField] TrailRenderer finishTrailRenderer;
    // self object references
    [SerializeField] Transform launchDirectionTarget;
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
    [SerializeField] float pullForce = 50f;
    [SerializeField] float pushForce = 50f;
    [SerializeField] float boostForce = 50f;
    [SerializeField] float launchForce = 10f;
    [Header("Quick Play Settings")]
    [SerializeField] ToggleButton quickRetryToggle;
    [SerializeField] ToggleButton quickLaunchToggle;
    [Header("Camera Settings")]
    [SerializeField] ToggleButton followPlayerToggle;
    [SerializeField] CameraPan playerCameraPan;
    [Header("Quick Retry Swipe")]
    [SerializeField, Min(0f)] float quickRetrySwipeMinimumDistancePixels = 50f;
    [SerializeField, Min(0f)] float quickRetrySwipeMaximumDurationSeconds = 0.25f;

    float timeAtLastLaunch;
    PlayerState state = PlayerState.Aiming;
    bool playerPressedLaunch; // TODO: change to events
    bool isInvincible;
    bool isDraggingLaunchDirectionTarget;
    bool quickSwipeStartedOverUi;
    readonly List<Collider2D> redirectors = new List<Collider2D>();
    // Trigger callbacks queue forces for the next physics step.
    Vector2 queuedInteractionForce;
    Vector3 launchTargetDragOffset = Vector3.zero;
    float timeAtLastRetry;
    bool hasStarted;

    public PlayerState State => state;
    public bool IsLevelInteractionHeld => isActiveAndEnabled &&
                                          state == PlayerState.Playing &&
                                          PointerInput.Instance != null &&
                                          PointerInput.Instance.IsSinglePointerHeld;

    void SetState(PlayerState newState)
    {
        state = newState;
    }

    void Start()
    {
        lr.positionCount = 2;

        HideFinishTrailRenderer();

        hasStarted = true;
        ApplyFollowPlayerSetting();
        EnterAiming(false);
    }

    void Update()
    {
        UpdateQuickSwipeGestureOrigin();

        if (WasQuickSwipeGesturePerformed())
        {
            if (state == PlayerState.Aiming)
                Launch();
            else
                EnterAiming(true);
        }
        else
        {
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

        ApplyInteractionForces();
    }

    void LateUpdate()
    {
        speedDisplay.text = state == PlayerState.Aiming
            ? "0"
            : rb.linearVelocity.magnitude.ToString("F2");

        // Future effect shaders should use this as a global uniform, not a material
        // property, so individual objects need no copies of the input or material.
        Shader.SetGlobalFloat(LevelInteractionHeldShaderId, IsLevelInteractionHeld ? 1f : 0f);

        if (!isDraggingLaunchDirectionTarget)
            return;

        UpdateLaunchDirectionTargetDrag();

        if (state == PlayerState.Aiming)
            UpdateLineRenderer();
    }

    void ApplyInteractionForces()
    {
        Collider2D redirector = null;
        if (IsLevelInteractionHeld)
        {
            for (int i = redirectors.Count - 1; i >= 0; i--)
            {
                Collider2D candidate = redirectors[i];
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
                {
                    redirectors.RemoveAt(i);
                    continue;
                }

                redirector = candidate;
                break;
            }
        }

        Vector2 force = queuedInteractionForce;
        queuedInteractionForce = Vector2.zero;

        if (redirector != null)
        {
            // Include any forces applied outside this queue before redirecting
            // momentum, then redirect the combined interaction force.
            Vector2 velocity = rb.linearVelocity + rb.totalForce * (Time.deltaTime / rb.mass);
            rb.totalForce = Vector2.zero;
            rb.linearVelocity = (Vector2)redirector.transform.up * velocity.magnitude;
            force = (Vector2)redirector.transform.up * force.magnitude;
        }

        if (force.sqrMagnitude > 0f)
            rb.AddForce(force, ForceMode2D.Force);
    }

    private void OnEnable()
    {
        EventManager.Instance.ShowPlayerInWorldUiElementsEvent.AddListener(ShowInWorldUiElements);
        EventManager.Instance.HidePlayerInWorldUiElementsEvent.AddListener(HideInWorldUiElements);
        EventManager.Instance.OnLevelLoadEvent.AddListener(EnterAimingAfterLevelLoad);
        SubscribeToFollowPlayerToggle();

        if (hasStarted)
        {
            EnterAiming(false);
            ApplyFollowPlayerSetting();
        }
    }

    private void OnDisable()
    {
        redirectors.Clear();
        queuedInteractionForce = Vector2.zero;
        Shader.SetGlobalFloat(LevelInteractionHeldShaderId, 0f);
        EventManager.Instance.ShowPlayerInWorldUiElementsEvent.RemoveListener(ShowInWorldUiElements);
        EventManager.Instance.HidePlayerInWorldUiElementsEvent.RemoveListener(HideInWorldUiElements);
        EventManager.Instance.OnLevelLoadEvent.RemoveListener(EnterAimingAfterLevelLoad);
        UnsubscribeFromFollowPlayerToggle();
        quickSwipeStartedOverUi = false;
    }

    void UpdateAiming()
    {
        launchButton.SetActive(true);
        retryButton.SetActive(false);

        timeDisplay.text = "0";

        lr.enabled = true;
        UpdateLineRenderer();

        rb.linearVelocity = Vector2.zero;

        if (WasLaunchRequested() && Time.time > timeAtLastRetry + 0.1f) // launch and retry share input, so prevent both in the same frame
            Launch();
    }

    void UpdatePlaying()
    {
        timeDisplay.text = (Time.time - timeAtLastLaunch).ToString("F3");

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
            && !quickSwipeStartedOverUi
            && !UiHitTest.IsScreenPositionOverUi(pointerInput.ScreenPosition)
            && pointerInput.PointerDurationSeconds <= quickRetrySwipeMaximumDurationSeconds
            && pointerInput.DragDistancePixels >= quickRetrySwipeMinimumDistancePixels;
    }

    void UpdateQuickSwipeGestureOrigin()
    {
        PointerInput pointerInput = PointerInput.Instance;
        if (pointerInput != null && pointerInput.WasPressedThisFrame)
            quickSwipeStartedOverUi = UiHitTest.IsScreenPositionOverUi(pointerInput.PressStartScreenPosition);
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
        PointerInput pointerInput = PointerInput.Instance;
        if (state != PlayerState.Aiming)
            return false;

        if (launchDirectionTarget == null || pointerInput == null ||
            !pointerInput.TryGetWorldPositionNoDepth(screenPosition, out Vector3 pointerWorldPosition))
        {
            Debug.LogError("Player could not begin dragging the launch direction because a required reference or valid pointer world position is unavailable.", this);
            return false;
        }

        launchTargetDragOffset = launchDirectionTarget.position - pointerWorldPosition;
        isDraggingLaunchDirectionTarget = true;
        return true;
    }

    void UpdateLaunchDirectionTargetDrag()
    {
        PointerInput pointerInput = PointerInput.Instance;
        if (!isDraggingLaunchDirectionTarget || launchDirectionTarget == null || pointerInput == null ||
            !pointerInput.TryGetCurrentWorldPosition(out Vector3 pointerWorldPosition))
            return;

        launchDirectionTarget.position = pointerWorldPosition + launchTargetDragOffset;
    }

    public void EndLaunchDirectionTargetDrag()
    {
        isDraggingLaunchDirectionTarget = false;
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
        redirectors.Clear();
        queuedInteractionForce = Vector2.zero;

        SetState(PlayerState.Aiming);
        playerPressedLaunch = false;

        // ensure velocity is zero
        rb.linearVelocity = Vector2.zero;
        rb.totalForce = Vector2.zero;

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

        if (allowQuickLaunch && IsQuickLaunchEnabled)
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
        if (collision.CompareTag("Redirect"))
        {
            RegisterRedirector(collision);
            return;
        }

        if (!IsLevelInteractionHeld)
            return;

        if (collision.CompareTag("Pull"))
            QueueInteractionForce(collision.transform.position - rb.transform.position, pullForce);
        if (collision.CompareTag("Push"))
            QueueInteractionForce(rb.transform.position - collision.transform.position, pushForce);
        if (collision.CompareTag("Boost"))
            QueueInteractionForce(collision.transform.up, boostForce);
    }

    void QueueInteractionForce(Vector2 direction, float strength)
    {
        queuedInteractionForce += direction.normalized * strength;
    }

    void RegisterRedirector(Collider2D area)
    {
        if (!redirectors.Contains(area))
            redirectors.Add(area);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Redirect"))
            RegisterRedirector(collision);
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

    void OnTriggerExit2D(Collider2D collision)
    {
        redirectors.Remove(collision);
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

        if (IsQuickRetryEnabled)
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

    bool IsQuickRetryEnabled => quickRetryToggle != null && quickRetryToggle.IsOn;
    bool IsQuickLaunchEnabled => quickLaunchToggle != null && quickLaunchToggle.IsOn;

    void SubscribeToFollowPlayerToggle()
    {
        if (followPlayerToggle == null)
            return;

        // Remove first so this remains safe if Unity enables the component more than once.
        followPlayerToggle.ValueChanged -= OnFollowPlayerToggleValueChanged;
        followPlayerToggle.ValueChanged += OnFollowPlayerToggleValueChanged;
    }

    void UnsubscribeFromFollowPlayerToggle()
    {
        if (followPlayerToggle != null)
            followPlayerToggle.ValueChanged -= OnFollowPlayerToggleValueChanged;
    }

    void OnFollowPlayerToggleValueChanged(bool _)
    {
        ApplyFollowPlayerSetting();
    }

    void ApplyFollowPlayerSetting()
    {
        if (playerCameraPan == null)
        {
            Debug.LogError("Player is missing its Player Camera Pan reference.", this);
            return;
        }

        if (followPlayerToggle == null)
        {
            Debug.LogError("Player is missing its Follow Player Toggle reference.", this);
            return;
        }

        playerCameraPan.SetFollowTargetEnabled(followPlayerToggle.IsOn);
    }
}
