using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    [SerializeField] Camera cam;

    void Start()
    {
        cam.orthographicSize = 10f;
    }

    void Update()
    {
        HandleZoom();
    }

    void HandleZoom()
    {
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + Input.GetAxisRaw("Mouse ScrollWheel") * 10f * -1f, 2f, 100f);
    }
}
