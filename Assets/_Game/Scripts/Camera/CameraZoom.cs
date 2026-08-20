using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraZoom : MonoBehaviour
{
    Camera cam;
    CameraPan cameraPan;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cameraPan = GetComponent<CameraPan>();
    }

    void Start()
    {
        cam.orthographicSize = GameManager.Instance.DefaultCameraZoom;
    }

    void Update()
    {
        if (cam == null || !cam.enabled)
            return;

        PointerInput pointerInput = PointerInput.Instance;
        if (pointerInput != null &&
            pointerInput.TryGetPinchGesture(out Vector2 pinchScreenPosition, out float pinchZoomScale))
        {
            ApplyZoomAtScreenPosition(cam.orthographicSize * pinchZoomScale, pinchScreenPosition, pointerInput);
            return;
        }

        float scrollInput = Input.GetAxisRaw("Mouse ScrollWheel");
        if (Mathf.Approximately(scrollInput, 0f))
            return;

        float zoomRatio = cam.orthographicSize / GameManager.Instance.DefaultCameraZoom;
        float requestedZoom = cam.orthographicSize + scrollInput * GameManager.Instance.ScrollZoomIncrement * zoomRatio * -1f;
        Vector2 scrollZoomScreenPosition = pointerInput != null ? pointerInput.ScreenPosition : (Vector2)Input.mousePosition;
        ApplyZoomAtScreenPosition(requestedZoom, scrollZoomScreenPosition, pointerInput);
    }

    void ApplyZoomAtScreenPosition(float requestedZoom, Vector2 zoomScreenPosition, PointerInput pointerInput)
    {
        float clampedZoom = Mathf.Clamp(requestedZoom, GameManager.Instance.MinCameraZoom, GameManager.Instance.MaxCameraZoom);
        if (Mathf.Approximately(cam.orthographicSize, clampedZoom))
            return;

        // Keep the world point below the zoom gesture fixed on screen as the orthographic size changes.
        Vector3 worldPositionBeforeZoom = default;
        bool hasWorldPositionBeforeZoom = pointerInput != null &&
            pointerInput.TryGetWorldPositionNoDepth(zoomScreenPosition, out worldPositionBeforeZoom);
        cam.orthographicSize = clampedZoom;

        if (!hasWorldPositionBeforeZoom ||
            !pointerInput.TryGetWorldPositionNoDepth(zoomScreenPosition, out Vector3 worldPositionAfterZoom))
            return;

        Vector3 cameraPositionAdjustment = worldPositionBeforeZoom - worldPositionAfterZoom;
        if (cameraPan != null)
            cameraPan.PanByWorldDelta(cameraPositionAdjustment);
        else
            transform.position += cameraPositionAdjustment;
    }
}
