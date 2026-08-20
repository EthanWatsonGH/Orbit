using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CameraViewManager : MonoBehaviour
{
    public static CameraViewManager Instance { get; private set; }

    [Header("Cameras")]
    [SerializeField] Camera menuCamera;
    [SerializeField] Camera playerCamera;
    [SerializeField] Camera levelEditorCamera;

    Camera activeWorldCamera;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Duplicate CameraViewManager in the scene.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DeactivateMenuCamera();
        DeactivateWorldCameras();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool ActivatePlayerCamera()
    {
        return ActivateWorldCamera(playerCamera, "Player");
    }

    public bool ActivateLevelEditorCamera()
    {
        return ActivateWorldCamera(levelEditorCamera, "Level Editor");
    }

    public bool ActivateMenuCameraFromCurrentView()
    {
        if (menuCamera == null)
        {
            Debug.LogError("CameraViewManager is missing its Menu Camera reference.", this);
            return false;
        }

        if (!TryGetActiveWorldCamera(out Camera currentCamera))
        {
            Debug.LogError("CameraViewManager could not find an active world camera to copy.", this);
            return false;
        }

        CopyView(currentCamera, menuCamera);
        DeactivateWorldCameras();
        SetCameraActive(menuCamera, true);
        return true;
    }

    public void DeactivateMenuCamera()
    {
        SetCameraActive(menuCamera, false);
    }

    public void DeactivateWorldCameras()
    {
        SetCameraActive(playerCamera, false);
        SetCameraActive(levelEditorCamera, false);
        activeWorldCamera = null;
    }

    public bool TryGetActiveWorldCamera(out Camera activeCamera)
    {
        activeCamera = activeWorldCamera;
        return activeCamera != null && activeCamera.enabled;
    }

    bool ActivateWorldCamera(Camera targetCamera, string cameraName)
    {
        if (targetCamera == null)
        {
            Debug.LogError("CameraViewManager is missing its " + cameraName + " Camera reference.", this);
            return false;
        }

        SetCameraActive(playerCamera, targetCamera == playerCamera);
        SetCameraActive(levelEditorCamera, targetCamera == levelEditorCamera);
        activeWorldCamera = targetCamera;
        return true;
    }

    static void SetCameraActive(Camera camera, bool isActive)
    {
        if (camera == null)
            return;

        camera.gameObject.tag = isActive ? "MainCamera" : "Untagged";
        camera.enabled = isActive;
        camera.gameObject.SetActive(isActive);
    }

    static void CopyView(Camera source, Camera destination)
    {
        destination.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        destination.orthographic = source.orthographic;
        destination.orthographicSize = source.orthographicSize;
        destination.fieldOfView = source.fieldOfView;
        destination.clearFlags = source.clearFlags;
        destination.backgroundColor = source.backgroundColor;
        destination.cullingMask = source.cullingMask;
        destination.nearClipPlane = source.nearClipPlane;
        destination.farClipPlane = source.farClipPlane;
    }
}
