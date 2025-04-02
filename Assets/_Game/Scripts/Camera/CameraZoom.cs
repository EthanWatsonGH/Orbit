using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    Camera cam;

    float newCameraZoom;
    float distanceBetweenTouchesAtTouchBegan;
    float cameraZoomAtTouchBegan;

    void Start()
    {
        cam = gameObject.transform.GetComponent<Camera>();

        // set the default zoom of the camera
        cam.orthographicSize = GameManager.Instance.DefaultCameraZoom;

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
                distanceBetweenTouchesAtTouchBegan = Vector3.Distance(touch1.position, touch0.position);
                cameraZoomAtTouchBegan = cam.orthographicSize;
            }
            else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                float currentDistanceBetweenTouches = Vector3.Distance(touch1.position, touch0.position);
                float touchZoomRatio = distanceBetweenTouchesAtTouchBegan / currentDistanceBetweenTouches;
                newCameraZoom = cameraZoomAtTouchBegan * touchZoomRatio;
            }
        }

        // desktop
        float desktopZoomRatio = cam.orthographicSize / GameManager.Instance.DefaultCameraZoom; // makes zoom increment per scroll exponential with zoom level
        if (Input.GetAxisRaw("Mouse ScrollWheel") != 0 && !UIManager.Instance.IsInControlBlockingMenu)
            newCameraZoom = cam.orthographicSize + Input.GetAxisRaw("Mouse ScrollWheel") * GameManager.Instance.ScrollZoomIncrement * desktopZoomRatio * -1f;

        // apply new zoom
        cam.orthographicSize = Mathf.Clamp(newCameraZoom, GameManager.Instance.MinCameraZoom, GameManager.Instance.MaxCameraZoom);
    }
}
