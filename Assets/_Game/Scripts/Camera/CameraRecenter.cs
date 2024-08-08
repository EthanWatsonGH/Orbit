using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRecenter : MonoBehaviour
{
    void Start()
    {
        
    }
    void Update()
    {
        
    }

    void OnEnable()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.RecenterCameraEvent.AddListener(RecenterCamera);
        }
        else
            Debug.Log("event manager is null");
    }

    void OnDisable()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.RecenterCameraEvent.RemoveListener(RecenterCamera);
        }
    }

    void RecenterCamera()
    {
        transform.position = new Vector3(transform.parent.position.x, transform.parent.position.y, transform.position.z);
    }
}
