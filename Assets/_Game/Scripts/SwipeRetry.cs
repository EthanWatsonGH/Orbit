using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwipeRetry : MonoBehaviour
{
    Vector2 touchStartPos; // Variable to store the touch start position

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // get touch start point
        // get touch end point
        // if distance between the 2 is greater than a certain distance, reset

        if (Input.touchCount == 1)
        {
            // get touch start point
            Touch touch = Input.GetTouch(0);

            // Check if the touch phase is the beginning of a touch
            if (touch.phase == TouchPhase.Began)
            {
                // Store the touch start position
                touchStartPos = touch.position;
            }
            // Check if the touch phase is the end of a touch
            else if (touch.phase == TouchPhase.Ended)
            {
                // Get the touch end position
                Vector2 touchEndPos = touch.position;

                // Calculate the distance between the start and end positions
                float touchDistance = Vector2.Distance(touchStartPos, touchEndPos);

                // Use touchDistance as needed
                Debug.Log("Touch distance: " + touchDistance);

                if (touchDistance >= 1000f)
                {

                }
            }
        }
    }
}
