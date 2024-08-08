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
        transform.position = new Vector3(transform.parent.position.x, transform.parent.position.y, transform.position.z);
    }
}
