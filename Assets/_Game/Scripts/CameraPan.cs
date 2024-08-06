using UnityEngine;

public class CameraPan : MonoBehaviour
{
    Vector3 touchStartPosition;
    bool isMousePanning = false;
    Vector3 mouseWorldPositionAtStartMousePan;
    Vector3 parentObjectPositionAtStartPan;
    Vector3 newPosition;

    void Start()
    {
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
                touchStartPosition = Camera.main.ScreenToWorldPoint(touchMidpoint);
                parentObjectPositionAtStartPan = gameObject.transform.parent.position;
            }
            else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                Vector3 touchPositionDelta = Camera.main.ScreenToWorldPoint(touchMidpoint) - touchStartPosition;
                // make camera move relative its parent object as player pans
                Vector3 parentObjectOffsetFromStartPan = parentObjectPositionAtStartPan - gameObject.transform.parent.position;

                newPosition -= touchPositionDelta + parentObjectOffsetFromStartPan;

                // TODO: i should only have this in LateUpdate, but if i dont have this here then the camera jitters as i touch pan and im not sure why. could it be because this is an else if?
                // don't move z
                newPosition.z = transform.position.z;
                // apply movement to camera
                transform.position = newPosition;
            }
        }
        #endregion

        #region Desktop
        // keyboard panning
        // make pan speed at different zoom levels exponentially more
        float zoomRatio = (Camera.main.orthographicSize / GameManager.Instance.DefaultCameraZoom) + 1;

        newPosition.x += Input.GetAxisRaw("Horizontal") * GameManager.Instance.KeyboardPanSpeed * Time.deltaTime * zoomRatio;
        newPosition.y += Input.GetAxisRaw("Vertical") * GameManager.Instance.KeyboardPanSpeed * Time.deltaTime * zoomRatio;

        // mouse panning
        if (Input.GetMouseButtonDown(1))
        {
            isMousePanning = true;
            mouseWorldPositionAtStartMousePan = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            parentObjectPositionAtStartPan = gameObject.transform.parent.position;
        }
        if (Input.GetMouseButtonUp(1))
        {
            isMousePanning = false;
        }

        if (isMousePanning)
        {
            Vector3 mousePositionDelta = Camera.main.ScreenToWorldPoint(Input.mousePosition) - mouseWorldPositionAtStartMousePan;
            // make camera move relative its parent object as player pans
            Vector3 parentObjectOffsetFromStartPan = parentObjectPositionAtStartPan - gameObject.transform.parent.position;

            newPosition -= mousePositionDelta + parentObjectOffsetFromStartPan;
        }
        #endregion
    }

    void LateUpdate()
    {
        // don't move z
        newPosition.z = transform.position.z;
        // apply movement to camera
        transform.position = newPosition;
    }
}