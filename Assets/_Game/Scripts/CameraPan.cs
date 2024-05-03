using UnityEngine;

public class CameraPan : MonoBehaviour
{
    Vector3 touchStartPosition;

    void Start()
    {
        
    }

    void Update()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                touchStartPosition = Camera.main.ScreenToWorldPoint(touch.position);
                touchStartPosition.z = transform.position.z; // keep z position
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                Vector3 touchPositionDelta = Camera.main.ScreenToWorldPoint(touch.position) - touchStartPosition;

                transform.position -= touchPositionDelta;
            }
        }
    }
}
