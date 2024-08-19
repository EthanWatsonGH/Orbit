using UnityEngine;

public class ScaleLineRendererWidthWithZoom : MonoBehaviour
{
    LineRenderer lr;
    float cameraZoomAtAwake;
    float lrStartWidthAtAwake;
    float lrEndWidthAtAwake;

    void Awake()
    {
        lr = gameObject.GetComponent<LineRenderer>();
        cameraZoomAtAwake = Camera.main.orthographicSize;
        lrStartWidthAtAwake = lr.startWidth;
        lrEndWidthAtAwake = lr.startWidth;
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
        // ratio between zoom at awake and current zoom
        float cameraZoomRatio = cameraZoomAtAwake / Camera.main.orthographicSize;
        // invert so its bigger when farther
        cameraZoomRatio = 1 / cameraZoomRatio;

        lr.startWidth = lrStartWidthAtAwake * cameraZoomRatio;
        lr.endWidth = lrEndWidthAtAwake * cameraZoomRatio;
    }
}
