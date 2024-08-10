using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OffsetObjectWithZoom : MonoBehaviour
{
    float offsetAtStartX;
    float offsetAtStartY;
    float cameraZoomAtStart;

    void Start()
    {
        offsetAtStartX = transform.localPosition.x;
        offsetAtStartY = transform.localPosition.y;
        cameraZoomAtStart = Camera.main.orthographicSize;
    }

    void Update()
    {
        float cameraScaleRatio = cameraZoomAtStart / Camera.main.orthographicSize;
        cameraScaleRatio = 1 / cameraScaleRatio;

        Vector3 newPosition = 
            new Vector3(offsetAtStartX * cameraScaleRatio * GameManager.Instance.ObjectTransformControlsOffsetMultiplier, 
            offsetAtStartY * cameraScaleRatio * GameManager.Instance.ObjectTransformControlsOffsetMultiplier, 
            transform.localPosition.z);
        transform.localPosition = newPosition;
    }
}
