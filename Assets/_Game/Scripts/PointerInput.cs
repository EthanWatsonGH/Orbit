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
    public bool IsSinglePointerHeld => IsHeld && Input.touchCount <= 1;
    public float PointerDurationSeconds => (IsHeld ? Time.unscaledTime : pointerReleaseUnscaledTime) - PressStartUnscaledTime;
    public float DragDistancePixels => Vector2.Distance(PressStartScreenPosition, ScreenPosition);

    bool isTrackingTouch;
    int primaryFingerId = -1;
    float pointerReleaseUnscaledTime;
    Vector3 currentWorldPosition;
    bool hasCurrentWorldPosition;
    readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

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

        if (isTrackingTouch)
        {
            UpdateTrackedTouch();
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began)
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

    // The cached position is refreshed after cameras move, so it remains correct when the
    // pointer is stationary while the camera zooms or pans.
    public bool TryGetCurrentWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = currentWorldPosition;
        return hasCurrentWorldPosition;
    }

    void UpdateTrackedTouch()
    {
        if (Input.touchCount > 1)
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
        HadMultiplePointersDuringCurrentGesture = Input.touchCount > 1;

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
}
