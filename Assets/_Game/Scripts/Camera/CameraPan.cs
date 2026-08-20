using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraPan : MonoBehaviour
{
    [SerializeField] Transform followTarget;

    Camera cam;
    Vector3 followOffset;
    bool followTargetEnabled = true;

    void Awake()
    {
        cam = GetComponent<Camera>();
        CaptureFollowOffset();
    }

    void Update()
    {
        if (cam == null || !cam.enabled)
            return;

        ApplyFollowTargetPosition();

        Vector3 newPosition = transform.position;

        PointerInput pointerInput = PointerInput.Instance;
        if (pointerInput != null &&
            pointerInput.TryGetPanGestureScreenDelta(out Vector2 panScreenPosition, out Vector2 panScreenDelta) &&
            pointerInput.TryGetWorldPositionNoDepth(panScreenPosition, out Vector3 currentPanWorldPosition) &&
            pointerInput.TryGetWorldPositionNoDepth(panScreenPosition - panScreenDelta, out Vector3 previousPanWorldPosition))
        {
            newPosition -= currentPanWorldPosition - previousPanWorldPosition;
        }

        // Keyboard panning. Pan speed scales with zoom.
        float zoomRatio = (cam.orthographicSize / GameManager.Instance.DefaultCameraZoom) + 1f;
        Vector3 keyboardPanDelta = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"),
            0f) * GameManager.Instance.KeyboardPanSpeed * Time.unscaledDeltaTime * zoomRatio;
        newPosition += keyboardPanDelta;

        newPosition.z = transform.position.z;
        SetCameraPosition(newPosition);
    }

    void LateUpdate()
    {
        if (cam != null && cam.enabled)
            ApplyFollowTargetPosition();
    }

    public void SetFollowTargetEnabled(bool isEnabled)
    {
        if (followTargetEnabled == isEnabled)
            return;

        followTargetEnabled = isEnabled;

        // Resuming follow preserves the framing the player currently chose instead of snapping the camera.
        if (followTargetEnabled)
            CaptureFollowOffset();
    }

    public void RecenterOn(Vector3 targetPosition)
    {
        SetCameraPosition(new Vector3(targetPosition.x, targetPosition.y, transform.position.z));
    }

    void ApplyFollowTargetPosition()
    {
        if (!followTargetEnabled || followTarget == null)
            return;

        transform.position = followTarget.position + followOffset;
    }

    void SetCameraPosition(Vector3 newPosition)
    {
        transform.position = newPosition;
        CaptureFollowOffset();
    }

    void CaptureFollowOffset()
    {
        if (followTarget != null)
            followOffset = transform.position - followTarget.position;
    }
}
