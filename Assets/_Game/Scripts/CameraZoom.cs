using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    [SerializeField] Camera cam;

    Vector2[] zoomStartTouchPositions = new Vector2[2];

    float minZoom = 2f;
    float maxZoom = 100f;

    void Start()
    {
        // set the default zoom of the camera
        cam.orthographicSize = 10f;
    }

    void Update()
    {
        HandleZoom();
    }

    void HandleZoom()
    {
        // desktop zoom
        if (Input.GetAxisRaw("Mouse ScrollWheel") != 0)
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + Input.GetAxisRaw("Mouse ScrollWheel") * 10f * -1f, minZoom, maxZoom);

        // mobile zoom
        if (Input.touchCount == 2)
        {
            
        }
    }
}
