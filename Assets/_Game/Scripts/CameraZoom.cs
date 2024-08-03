using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] float scrollZoomIncrement = 10f;

    Vector2[] zoomStartTouchPositions = new Vector2[2];

    float minZoom = 2f;
    float maxZoom = 100f;

    float cameraZoomAtStart;

    void Start()
    {
        // set the default zoom of the camera
        cam.orthographicSize = 10f;

        cameraZoomAtStart = Camera.main.orthographicSize;
    }

    void Update()
    {
        HandleZoom();
    }

    void HandleZoom()
    {
        // touchscreen
        if (Input.touchCount == 2)
        {

        }

        // desktop
        float zoomRatio = (Camera.main.orthographicSize / cameraZoomAtStart); // makes zoom increment per scroll exponential with zoom level
        if (Input.GetAxisRaw("Mouse ScrollWheel") != 0)
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + Input.GetAxisRaw("Mouse ScrollWheel") * scrollZoomIncrement * zoomRatio * -1f, minZoom, maxZoom);
    }
}
