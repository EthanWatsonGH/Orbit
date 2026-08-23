using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public class PointerInput : MonoBehaviour
{
    public static PointerInput Instance { get; private set; }

    public bool WasPressedThisFrame { get; private set; }
    public bool WasReleasedThisFrame { get; private set; }
    public bool WasCanceledThisFrame { get; private set; }
    public bool IsHeld { get; private set; }
    public Vector2 ScreenPosition { get; private set; }
    public Vector2 PressStartScreenPosition { get; private set; }
    public float PressStartUnscaledTime { get; private set; }
    public bool CurrentGestureStartedOverUi { get; private set; }
    public bool WasReleasedOverUi { get; private set; }
    public bool CurrentGestureStartedOverSelectableUi { get; private set; }
    public bool WasReleasedOverSelectableUi { get; private set; }
    public bool HadMultiplePointersDuringCurrentGesture { get; private set; }
    public bool IsSinglePointerHeld => IsHeld && GetGameplayTouchCount() <= 1;
    public float PointerDurationSeconds => (IsHeld ? Time.unscaledTime : pointerReleaseUnscaledTime) - PressStartUnscaledTime;
    public float DragDistancePixels => Vector2.Distance(PressStartScreenPosition, ScreenPosition);

    // This is intentionally separate from the primary gameplay pointer above. A transform gesture keeps
    // using the first finger even when a second finger starts a two-finger camera pan.
    public Vector2 PanGestureScreenPosition { get; private set; }
    public Vector2 PanGestureScreenDelta { get; private set; }
    public bool HasPanGesture { get; private set; }
    public Vector2 PinchGestureScreenPosition { get; private set; }
    public float PinchGestureZoomScale { get; private set; } = 1f;
    public bool HasPinchGesture { get; private set; }

    bool isTrackingTouch;
    int primaryFingerId = -1;
    float pointerReleaseUnscaledTime;
    Vector3 currentWorldPosition;
    bool hasCurrentWorldPosition;
    bool isTouchPanGestureActive;
    Vector2 previousTouchPanGesturePosition;
    bool isMousePanGestureActive;
    Vector2 previousMousePanGesturePosition;
    bool isTouchPinchGestureActive;
    float previousTouchPinchDistance;
    readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    readonly HashSet<int> excludedTouchFingerIds = new HashSet<int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Duplicate PointerInput in the scene.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        WasPressedThisFrame = false;
        WasReleasedThisFrame = false;
        WasCanceledThisFrame = false;
        UpdateExcludedModifierTouches();
        UpdateInputGestures();

        if (isTrackingTouch)
        {
            UpdateTrackedTouch();
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && !IsExcludedTouch(touch))
            {
                isTrackingTouch = true;
                primaryFingerId = touch.fingerId;
                BeginPointer(touch.position);
                return;
            }
        }

        if (Input.touchCount > 0)
        {
            IsHeld = false;
            return;
        }

        UpdateMouse();
    }

    void LateUpdate()
    {
        hasCurrentWorldPosition = TryGetWorldPositionNoDepth(ScreenPosition, out currentWorldPosition);
    }

    public bool TryGetWorldPosition(Vector2 screenPosition, float worldPlaneZ, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (CameraViewManager.Instance == null ||
            !CameraViewManager.Instance.TryGetActiveWorldCamera(out Camera activeCamera))
            return false;

        float distanceFromCamera = Mathf.Abs(worldPlaneZ - activeCamera.transform.position.z);
        Vector3 convertedPosition = activeCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, distanceFromCamera));
        worldPosition = new Vector3(convertedPosition.x, convertedPosition.y, worldPlaneZ);
        return true;
    }

    public bool TryGetWorldPositionNoDepth(Vector2 screenPosition, out Vector3 worldPosition)
    {
        return TryGetWorldPosition(screenPosition, 0f, out worldPosition);
    }

    // A UI control can capture the pointer that pressed it without changing the primary
    // gameplay pointer. This lets a transform handle follow its own finger during multitouch.
    public bool TryGetScreenPosition(int pointerId, out Vector2 screenPosition)
    {
        if (pointerId < 0)
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == pointerId)
            {
                screenPosition = touch.position;
                return true;
            }
        }

        screenPosition = default;
        return false;
    }

    // The cached position is refreshed after cameras move, so it remains correct when the
    // pointer is stationary while the camera zooms or pans.
    public bool TryGetCurrentWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = currentWorldPosition;
        return hasCurrentWorldPosition;
    }

    public bool TryGetPanGestureScreenDelta(out Vector2 screenPosition, out Vector2 screenDelta)
    {
        screenPosition = PanGestureScreenPosition;
        screenDelta = PanGestureScreenDelta;
        return HasPanGesture;
    }

    public bool TryGetPinchGesture(out Vector2 screenPosition, out float zoomScale)
    {
        screenPosition = PinchGestureScreenPosition;
        zoomScale = PinchGestureZoomScale;
        return HasPinchGesture;
    }

    void UpdateInputGestures()
    {
        HasPanGesture = false;
        PanGestureScreenDelta = Vector2.zero;
        HasPinchGesture = false;
        PinchGestureZoomScale = 1f;

        if (Input.touchCount > 0)
        {
            isMousePanGestureActive = false;
            UpdateTouchPanGesture();
            UpdateTouchPinchGesture();
            return;
        }

        isTouchPanGestureActive = false;
        isTouchPinchGestureActive = false;
        UpdateMousePanGesture();
    }

    void UpdateTouchPanGesture()
    {
        // Camera panning requires exactly two live touches. The first touch remains the gameplay pointer;
        // this midpoint is used only by the camera and never replaces ScreenPosition or primaryFingerId.
        if (!TryGetTwoGameplayTouches(out Touch firstTouch, out Touch secondTouch))
        {
            isTouchPanGestureActive = false;
            return;
        }

        Vector2 midpoint = (firstTouch.position + secondTouch.position) * 0.5f;
        PanGestureScreenPosition = midpoint;

        if (!isTouchPanGestureActive || firstTouch.phase == TouchPhase.Began || secondTouch.phase == TouchPhase.Began)
        {
            isTouchPanGestureActive = true;
            previousTouchPanGesturePosition = midpoint;
            return;
        }

        PanGestureScreenDelta = midpoint - previousTouchPanGesturePosition;
        previousTouchPanGesturePosition = midpoint;
        HasPanGesture = true;
    }

    void UpdateMousePanGesture()
    {
        bool isPanButtonHeld = Input.GetMouseButton(1) || Input.GetMouseButton(2);
        if (!isPanButtonHeld)
        {
            isMousePanGestureActive = false;
            return;
        }

        Vector2 mousePosition = Input.mousePosition;
        PanGestureScreenPosition = mousePosition;

        if (!isMousePanGestureActive)
        {
            isMousePanGestureActive = true;
            previousMousePanGesturePosition = mousePosition;
            return;
        }

        PanGestureScreenDelta = mousePosition - previousMousePanGesturePosition;
        previousMousePanGesturePosition = mousePosition;
        HasPanGesture = true;
    }

    void UpdateTouchPinchGesture()
    {
        if (!TryGetTwoGameplayTouches(out Touch firstTouch, out Touch secondTouch))
        {
            isTouchPinchGestureActive = false;
            return;
        }

        float currentDistance = Vector2.Distance(firstTouch.position, secondTouch.position);
        if (currentDistance <= Mathf.Epsilon)
        {
            isTouchPinchGestureActive = false;
            return;
        }

        Vector2 midpoint = (firstTouch.position + secondTouch.position) * 0.5f;
        PinchGestureScreenPosition = midpoint;

        if (!isTouchPinchGestureActive || firstTouch.phase == TouchPhase.Began || secondTouch.phase == TouchPhase.Began)
        {
            isTouchPinchGestureActive = true;
            previousTouchPinchDistance = currentDistance;
            return;
        }

        PinchGestureZoomScale = previousTouchPinchDistance / currentDistance;
        previousTouchPinchDistance = currentDistance;
        HasPinchGesture = true;
    }

    void UpdateTrackedTouch()
    {
        if (GetGameplayTouchCount() > 1)
            HadMultiplePointersDuringCurrentGesture = true;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != primaryFingerId)
                continue;

            ScreenPosition = touch.position;

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                EndPointer(touch.position, touch.phase == TouchPhase.Canceled);
            else
                IsHeld = true;

            return;
        }

        // The tracked touch disappeared without an Ended event.
        EndPointer(ScreenPosition, true);
    }

    void UpdateMouse()
    {
        ScreenPosition = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
            BeginPointer(ScreenPosition);

        IsHeld = Input.GetMouseButton(0);

        if (Input.GetMouseButtonUp(0))
            EndPointer(ScreenPosition);
    }

    void BeginPointer(Vector2 screenPosition)
    {
        ScreenPosition = screenPosition;
        PressStartScreenPosition = screenPosition;
        PressStartUnscaledTime = Time.unscaledTime;
        WasPressedThisFrame = true;
        IsHeld = true;
        HadMultiplePointersDuringCurrentGesture = GetGameplayTouchCount() > 1;

        GetUiStateAtScreenPosition(screenPosition, out bool isOverUi, out bool isOverSelectableUi);
        CurrentGestureStartedOverUi = isOverUi;
        CurrentGestureStartedOverSelectableUi = isOverSelectableUi;
    }

    void EndPointer(Vector2 screenPosition, bool wasCanceled = false)
    {
        ScreenPosition = screenPosition;
        pointerReleaseUnscaledTime = Time.unscaledTime;
        WasReleasedThisFrame = true;
        WasCanceledThisFrame = wasCanceled;
        IsHeld = false;
        isTrackingTouch = false;
        primaryFingerId = -1;

        GetUiStateAtScreenPosition(screenPosition, out bool isOverUi, out bool isOverSelectableUi);
        WasReleasedOverUi = isOverUi;
        WasReleasedOverSelectableUi = isOverSelectableUi;
    }

    void GetUiStateAtScreenPosition(Vector2 screenPosition, out bool isOverUi, out bool isOverSelectableUi)
    {
        isOverUi = false;
        isOverSelectableUi = false;

        if (EventSystem.current == null)
            return;

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            if (uiRaycastResults[i].gameObject.GetComponentInParent<Selectable>() != null)
                isOverSelectableUi = true;
        }

        isOverUi = uiRaycastResults.Count > 0;
    }

    void UpdateExcludedModifierTouches()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && IsScreenPositionOverEditorModifierButton(touch.position))
                excludedTouchFingerIds.Add(touch.fingerId);
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                excludedTouchFingerIds.Remove(touch.fingerId);
        }

        if (Input.touchCount == 0)
            excludedTouchFingerIds.Clear();
    }

    bool IsExcludedTouch(Touch touch)
    {
        return excludedTouchFingerIds.Contains(touch.fingerId);
    }

    int GetGameplayTouchCount()
    {
        int gameplayTouchCount = 0;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (!IsExcludedTouch(touch) &&
                touch.phase != TouchPhase.Ended &&
                touch.phase != TouchPhase.Canceled)
            {
                gameplayTouchCount++;
            }
        }

        return gameplayTouchCount;
    }

    bool TryGetTwoGameplayTouches(out Touch firstGameplayTouch, out Touch secondGameplayTouch)
    {
        firstGameplayTouch = default;
        secondGameplayTouch = default;
        int gameplayTouchCount = 0;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (IsExcludedTouch(touch) ||
                touch.phase == TouchPhase.Ended ||
                touch.phase == TouchPhase.Canceled)
            {
                continue;
            }

            if (gameplayTouchCount == 0)
                firstGameplayTouch = touch;
            else if (gameplayTouchCount == 1)
                secondGameplayTouch = touch;

            gameplayTouchCount++;
        }

        return gameplayTouchCount == 2;
    }

    bool IsScreenPositionOverEditorModifierButton(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, uiRaycastResults);
        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            if (uiRaycastResults[i].gameObject.GetComponentInParent<EditorModifierButton>() != null)
                return true;
        }

        return false;
    }
}
