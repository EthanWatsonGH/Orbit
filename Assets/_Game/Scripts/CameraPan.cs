using UnityEngine;

public class CameraPan : MonoBehaviour
{
    [SerializeField] float keyboardPanSpeed = 10f;
    Vector3 touchStartPosition;
    bool isMousePanning = false;
    Vector3 mouseWorldPositionAtStartMousePan;
    float cameraZoomAtStart;

    void Start()
    {
        // failsafe for first touch
        touchStartPosition = Vector3.zero;
        touchStartPosition.z = transform.position.z; // keep z position

        cameraZoomAtStart = Camera.main.orthographicSize;
    }

    void Update()
    {
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
                touchStartPosition.z = transform.position.z; // keep z position
            }
            else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                Vector3 touchPositionDelta = Camera.main.ScreenToWorldPoint(touchMidpoint) - touchStartPosition;

                transform.position -= touchPositionDelta;
            }
        }
        #endregion

        #region Desktop
        Vector3 newPosition = transform.position;

        // keyboard panning
        float zoomRatio = (Camera.main.orthographicSize / cameraZoomAtStart) + 1;

        if (Input.GetKey(KeyCode.W))
        {
            newPosition.y += keyboardPanSpeed * Time.deltaTime * zoomRatio;
        }
        if (Input.GetKey(KeyCode.S))
        {
            newPosition.y -= keyboardPanSpeed * Time.deltaTime * zoomRatio;
        }
        if (Input.GetKey(KeyCode.A))
        {
            newPosition.x -= keyboardPanSpeed * Time.deltaTime * zoomRatio;
        }
        if (Input.GetKey(KeyCode.D))
        {
            newPosition.x += keyboardPanSpeed * Time.deltaTime * zoomRatio;
        }
        transform.position = newPosition;

        // mouse panning
        if (Input.GetMouseButtonDown(1))
        {
            isMousePanning = true;
            mouseWorldPositionAtStartMousePan = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        }
        if (Input.GetMouseButtonUp(1))
            isMousePanning = false;

        if (isMousePanning)
        {
            Vector3 mousePositionDelta = Camera.main.ScreenToWorldPoint(Input.mousePosition) - mouseWorldPositionAtStartMousePan;

            newPosition -= mousePositionDelta;

            // don't move z
            newPosition.z = transform.position.z;

            transform.position = newPosition;
        }
        #endregion
    }
}