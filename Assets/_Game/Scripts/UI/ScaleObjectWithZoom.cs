using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScaleObjectWithZoom : MonoBehaviour
{
    float cameraZoomAtStart;

    void Start()
    {
        cameraZoomAtStart = Camera.main.orthographicSize;
    }

    void Update()
    {
        // makes object always be the same size it was relative to the screen at start, at all camera zoom levels
        float cameraScaleRatio = cameraZoomAtStart / Camera.main.orthographicSize;
        // invert so object is bigger when zoomed out instead of smaller
        cameraScaleRatio = 1 / cameraScaleRatio;

        Vector3 newScale = 
            new Vector3(GameManager.Instance.UIScale * cameraScaleRatio, 
            GameManager.Instance.UIScale * cameraScaleRatio, 
            transform.localScale.z);
        transform.localScale = newScale;
    }
}