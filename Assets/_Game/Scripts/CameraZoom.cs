using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    float newCameraZoom;

    void Start()
    {
        // set the default zoom of the camera
        Camera.main.orthographicSize = GameManager.Instance.DefaultCameraZoom;

        // initialize value
        newCameraZoom = GameManager.Instance.DefaultCameraZoom;
    }

    void Update()
    {
        // touchscreen
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            if (touch1.phase == TouchPhase.Began)
            {
                
            }

            // get distance between touches at start of zoom
            // compare distance between touches at start to the distance on this frame
        }

        // desktop
        float zoomRatio = Camera.main.orthographicSize / GameManager.Instance.DefaultCameraZoom; // makes zoom increment per scroll exponential with zoom level
        if (Input.GetAxisRaw("Mouse ScrollWheel") != 0)
            newCameraZoom = Mathf.Clamp(Camera.main.orthographicSize + Input.GetAxisRaw("Mouse ScrollWheel") * GameManager.Instance.ScrollZoomIncrement * zoomRatio * -1f, GameManager.Instance.MinCameraZoom, GameManager.Instance.MaxCameraZoom);
    }

    void LateUpdate()
    {
        // TODO: if it's jittering when i add touchscreen zoom, put this in Update
        Camera.main.orthographicSize = newCameraZoom;
    }
}
