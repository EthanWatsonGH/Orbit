using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScaleLineRendererWidthWithZoom : MonoBehaviour
{
    LineRenderer lr;
    float cameraZoomAtAwake;
    float lrWidthAtAwake;

    void Awake()
    {
        lr = gameObject.GetComponent<LineRenderer>();
        cameraZoomAtAwake = Camera.main.orthographicSize;
        lrWidthAtAwake = lr.startWidth;
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
        float cameraZoomRatio = cameraZoomAtAwake / Camera.main.orthographicSize;
        cameraZoomRatio = 1 / cameraZoomRatio;

        lr.startWidth = lrWidthAtAwake * cameraZoomRatio;
        lr.endWidth = lrWidthAtAwake * cameraZoomRatio;
    }
}
