using UnityEngine;

public class CameraPan : MonoBehaviour
{
    Camera cam;

    Vector3 touchStartPosition;
    bool isMousePanning = false;
    Vector3 mouseWorldPositionAtStartMousePan;
    Vector3 parentObjectPositionAtStartPan;
    Vector3 newPosition;

    void Start()
    {
        cam = gameObject.transform.GetComponent<Camera>();

        // failsafe for first touch
        touchStartPosition = Vector3.zero;
    }

    void Update()
    {
        newPosition = transform.position;

        #region Touchscreen
        // 2 finger drag panning
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            Vector2 touchMidpoint = (touch0.position + touch1.position) / 2f;

            if (touch1.phase == TouchPhase.Began)
            {
                touchStartPosition = cam.ScreenToWorldPoint(touchMidpoint);
                parentObjectPositionAtStartPan = gameObject.transform.parent.position;
            }
            else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                Vector3 touchPositionDelta = cam.ScreenToWorldPoint(touchMidpoint) - touchStartPosition;
                // make camera move relative its parent object as player pans
                Vector3 parentObjectOffsetFromStartPan = parentObjectPositionAtStartPan - gameObject.transform.parent.position;

                newPosition -= touchPositionDelta + parentObjectOffsetFromStartPan;

                // TODO: i should only have this at the end of Update, but if i dont have this here then the camera jitters as i touch pan and im not sure why. could it be because this is an else if?
                // don't move z
                newPosition.z = transform.position.z;
                // apply movement to camera
                transform.position = newPosition;
            }
        }
        #endregion

        #region Desktop
        // mouse panning
        // TODO: mouse panning and zooming are fighting each other. when i scoll in a step, it has to move the camera to where the mouse is on the new zoom level, causing a jittering effect. im not sure if this is some execution ordering issue where for some reason they aren't being done on the same frame, or if there's somthing about my code that makes the panning have to wait to be executed after the zooming.
        if (Input.GetMouseButtonDown(1))
        {
            isMousePanning = true;
            mouseWorldPositionAtStartMousePan = cam.ScreenToWorldPoint(Input.mousePosition);
            parentObjectPositionAtStartPan = gameObject.transform.parent.position;
        }
        if (Input.GetMouseButtonUp(1))
        {
            isMousePanning = false;
        }

        if (isMousePanning)
        {
            Vector3 mousePositionDelta = cam.ScreenToWorldPoint(Input.mousePosition) - mouseWorldPositionAtStartMousePan;
            // make camera move relative its parent object as player pans
            Vector3 parentObjectOffsetFromStartPan = parentObjectPositionAtStartPan - gameObject.transform.parent.position;

            newPosition -= mousePositionDelta + parentObjectOffsetFromStartPan;
        }

        // keyboard panning
        // make pan speed at different zoom levels exponentially more
        float zoomRatio = (cam.orthographicSize / GameManager.Instance.DefaultCameraZoom) + 1;

        newPosition.x += Input.GetAxisRaw("Horizontal") * GameManager.Instance.KeyboardPanSpeed * Time.deltaTime * zoomRatio;
        newPosition.y += Input.GetAxisRaw("Vertical") * GameManager.Instance.KeyboardPanSpeed * Time.deltaTime * zoomRatio;
        #endregion

        // apply movement
        // don't move z
        newPosition.z = transform.position.z;
        // apply movement to camera
        transform.position = newPosition;
    }
}