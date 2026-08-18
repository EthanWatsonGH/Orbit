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
        if (!TryGetRecenterPosition(out Vector3 recenterPosition))
            return;

        transform.position = new Vector3(recenterPosition.x, recenterPosition.y, transform.position.z);
    }

    public bool TryGetRecenterPosition(out Vector3 recenterPosition)
    {
        if (!isLevelEditorCamera)
        {
            if (transform.parent == null)
            {
                recenterPosition = default;
                return false;
            }

            recenterPosition = transform.parent.position;
            return true;
        }

        if (playerStartPoint == null)
        {
            GameObject playerStartPointObject = GameObject.Find("PlayerStartPoint");
            playerStartPoint = playerStartPointObject != null ? playerStartPointObject.transform : null;
        }

        if (playerStartPoint == null)
        {
            recenterPosition = default;
            return false;
        }

        recenterPosition = playerStartPoint.position;
        return true;
    }
}
