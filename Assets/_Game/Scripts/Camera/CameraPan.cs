using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraPan : MonoBehaviour
{
    [SerializeField] Transform followTarget;

    Camera cam;
    Vector3 followOffset;
    bool followTargetEnabled = true;

    Vector3 touchStartPosition;
    bool isMousePanning;
    Vector3 mouseWorldPositionAtStartMousePan;

    void Awake()
    {
        cam = GetComponent<Camera>();
        CaptureFollowOffset();
    }

    void Start()
    {
        // Failsafe for initial touch.
        touchStartPosition = Vector3.zero;
    }

    void Update()
    {
        if (cam == null || !cam.enabled)
            return;

        ApplyFollowTargetPosition();

        Vector3 newPosition = transform.position;

        #region Touchscreen
        // Two-finger drag panning.
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);
            Vector2 touchMidpoint = (touch0.position + touch1.position) / 2f;

            if (touch1.phase == TouchPhase.Began)
            {
                touchStartPosition = cam.ScreenToWorldPoint(touchMidpoint);
            }
            else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                Vector3 touchPositionDelta = cam.ScreenToWorldPoint(touchMidpoint) - touchStartPosition;
                newPosition -= touchPositionDelta;
            }
        }
        #endregion

        #region Desktop
        // Mouse panning.
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            isMousePanning = true;
            mouseWorldPositionAtStartMousePan = cam.ScreenToWorldPoint(Input.mousePosition);
        }
        if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
            isMousePanning = false;

        if (isMousePanning)
        {
            Vector3 mousePositionDelta = cam.ScreenToWorldPoint(Input.mousePosition) - mouseWorldPositionAtStartMousePan;
            newPosition -= mousePositionDelta;
        }

        // Keyboard panning. Pan speed scales with zoom.
        float zoomRatio = (cam.orthographicSize / GameManager.Instance.DefaultCameraZoom) + 1f;
        newPosition.x += Input.GetAxisRaw("Horizontal") * GameManager.Instance.KeyboardPanSpeed * Time.unscaledDeltaTime * zoomRatio;
        newPosition.y += Input.GetAxisRaw("Vertical") * GameManager.Instance.KeyboardPanSpeed * Time.unscaledDeltaTime * zoomRatio;
        #endregion

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
