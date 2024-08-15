using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OffsetObjectWithZoom : MonoBehaviour
{
    float offsetAtAwakeX;
    float offsetAtAwakeY;
    float cameraZoomAtAwake;

    void Awake()
    {
        offsetAtAwakeX = transform.localPosition.x;
        offsetAtAwakeY = transform.localPosition.y;
        cameraZoomAtAwake = Camera.main.orthographicSize;
    }

    void OnEnable()
    {
        Offset();
    }

    void Update()
    {
        Offset();

    }

    void Offset()
    {
        float cameraScaleRatio = cameraZoomAtAwake / Camera.main.orthographicSize;
        cameraScaleRatio = 1 / cameraScaleRatio;

        Vector3 newPosition =
            new Vector3(offsetAtAwakeX * cameraScaleRatio * GameManager.Instance.ObjectTransformControlsOffsetMultiplier,
            offsetAtAwakeY * cameraScaleRatio * GameManager.Instance.ObjectTransformControlsOffsetMultiplier,
            transform.localPosition.z);
        transform.localPosition = newPosition;
    }
}
