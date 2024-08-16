using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRecenter : MonoBehaviour
{
    void OnEnable()
    {
        EventManager.Instance.RecenterCameraEvent.AddListener(RecenterCamera);
    }

    void OnDisable()
    {
        EventManager.Instance.RecenterCameraEvent.RemoveListener(RecenterCamera);
    }

    void RecenterCamera()
    {
        if (transform.parent.name == "LevelEditor")
        {
            Transform recenterLocation = GameObject.Find("PlayerStartPoint").transform;

            transform.position = new Vector3(recenterLocation.position.x, recenterLocation.position.y, transform.position.z);
        }
        else
            transform.position = new Vector3(transform.parent.position.x, transform.parent.position.y, transform.position.z);
    }
}
