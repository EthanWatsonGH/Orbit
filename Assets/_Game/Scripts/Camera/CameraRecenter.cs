using UnityEngine;

public class CameraRecenter : MonoBehaviour
{
    Transform playerStartPoint;
    bool isLevelEditorCamera;

    void Awake()
    {
        isLevelEditorCamera = GetComponentInParent<LevelEditor>() != null;
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
        if (!TryGetTargetPosition(out Vector3 targetPosition))
            return;

        transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
    }

    public bool TryGetTargetPosition(out Vector3 targetPosition)
    {
        if (!isLevelEditorCamera)
        {
            if (transform.parent == null)
            {
                targetPosition = default;
                return false;
            }

            targetPosition = transform.parent.position;
            return true;
        }

        if (playerStartPoint == null)
        {
            GameObject playerStartPointObject = GameObject.Find("PlayerStartPoint");
            playerStartPoint = playerStartPointObject != null ? playerStartPointObject.transform : null;
        }

        if (playerStartPoint == null)
        {
            targetPosition = default;
            return false;
        }

        targetPosition = playerStartPoint.position;
        return true;
    }
}
