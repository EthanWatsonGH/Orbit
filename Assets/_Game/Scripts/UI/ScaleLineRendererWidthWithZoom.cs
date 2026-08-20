using UnityEngine;

public class ScaleLineRendererWidthWithZoom : MonoBehaviour
{
    LineRenderer lr;
    float cameraZoomAtAwake;
    float lrStartWidthAtAwake;
    float lrEndWidthAtAwake;
    bool hasCameraZoomReference;

    void Awake()
    {
        lr = gameObject.GetComponent<LineRenderer>();
        lrStartWidthAtAwake = lr.startWidth;
        lrEndWidthAtAwake = lr.endWidth;
    }

    void OnEnable()
    {
        ScaleWidth();
    }

    void Update()
    {
        ScaleWidth();
    }

    void ScaleWidth()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null)
            return;

        // CameraViewManager enables the correct camera after Awake has completed.
        // Capture the initial zoom only once a camera is actually available.
        if (!hasCameraZoomReference)
        {
            cameraZoomAtAwake = activeCamera.orthographicSize;
            hasCameraZoomReference = true;
        }

        // ratio between zoom at awake and current zoom
        float cameraZoomRatio = cameraZoomAtAwake / activeCamera.orthographicSize;
        // invert so its bigger when farther
        cameraZoomRatio = 1 / cameraZoomRatio;

        lr.startWidth = lrStartWidthAtAwake * cameraZoomRatio;
        lr.endWidth = lrEndWidthAtAwake * cameraZoomRatio;
    }
}
