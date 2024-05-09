using UnityEngine;

public class CameraPan : MonoBehaviour
{
    Vector3 touchStartPosition;

    void Start()
    {
        // failsafe for first touch
        touchStartPosition = Vector3.zero;
        touchStartPosition.z = transform.position.z; // keep z position
    }

    void Update()
    {
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
    }
}
