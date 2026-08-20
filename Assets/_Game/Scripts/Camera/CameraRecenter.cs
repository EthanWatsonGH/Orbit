using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraRecenter : MonoBehaviour
{
    [SerializeField] Transform recenterTarget;

    Camera cam;
    CameraPan cameraPan;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cameraPan = GetComponent<CameraPan>();
    }

    void OnEnable()
    {
        if (EventManager.Instance != null)
            EventManager.Instance.RecenterCameraEvent.AddListener(RecenterCamera);
    }

    void OnDisable()
    {
        if (EventManager.Instance != null)
            EventManager.Instance.RecenterCameraEvent.RemoveListener(RecenterCamera);
    }

    void RecenterCamera()
    {
        if (cam == null || !cam.enabled || !TryGetTargetPosition(out Vector3 targetPosition))
            return;

        if (cameraPan != null)
            cameraPan.RecenterOn(targetPosition);
        else
            transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
    }

    public bool TryGetTargetPosition(out Vector3 targetPosition)
    {
        if (recenterTarget == null)
        {
            targetPosition = default;
            return false;
        }

        targetPosition = recenterTarget.position;
        return true;
    }
}
