using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    [SerializeField] Camera cam;

    // Start is called before the first frame update
    void Start()
    {
        cam.orthographicSize = 10f;
    }

    // Update is called once per frame
    void Update()
    {
        HandleZoom();
    }

    void HandleZoom()
    {
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + Input.GetAxisRaw("Mouse ScrollWheel") * 10f * -1f, 2f, 100f);
    }
}
