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
            ApplyPinchZoom(pinchScreenPosition, pinchZoomScale, pointerInput);
            return;
        }

        float scrollInput = Input.GetAxisRaw("Mouse ScrollWheel");
        if (Mathf.Approximately(scrollInput, 0f))
            return;

        float zoomRatio = cam.orthographicSize / GameManager.Instance.DefaultCameraZoom;
        float requestedZoom = cam.orthographicSize + scrollInput * GameManager.Instance.ScrollZoomIncrement * zoomRatio * -1f;
        cam.orthographicSize = Mathf.Clamp(requestedZoom, GameManager.Instance.MinCameraZoom, GameManager.Instance.MaxCameraZoom);
    }

    void ApplyPinchZoom(Vector2 pinchScreenPosition, float pinchZoomScale, PointerInput pointerInput)
    {
        float requestedZoom = cam.orthographicSize * pinchZoomScale;
        float clampedZoom = Mathf.Clamp(requestedZoom, GameManager.Instance.MinCameraZoom, GameManager.Instance.MaxCameraZoom);
        if (Mathf.Approximately(cam.orthographicSize, clampedZoom))
            return;

        // Keep the world point below the pinch midpoint fixed on screen as the orthographic size changes.
        bool hasWorldPositionBeforeZoom = pointerInput.TryGetWorldPositionNoDepth(pinchScreenPosition, out Vector3 worldPositionBeforeZoom);
        cam.orthographicSize = clampedZoom;

        if (!hasWorldPositionBeforeZoom ||
            !pointerInput.TryGetWorldPositionNoDepth(pinchScreenPosition, out Vector3 worldPositionAfterZoom))
            return;

        Vector3 cameraPositionAdjustment = worldPositionBeforeZoom - worldPositionAfterZoom;
        if (cameraPan != null)
            cameraPan.PanByWorldDelta(cameraPositionAdjustment);
        else
            transform.position += cameraPositionAdjustment;
    }
}
