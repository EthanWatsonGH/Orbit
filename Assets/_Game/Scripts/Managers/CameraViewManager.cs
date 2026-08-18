using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class CameraViewManager : MonoBehaviour
{
    public static CameraViewManager Instance { get; private set; }

    Camera menuCamera;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Duplicate CameraViewManager in the scene.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        menuCamera = GetComponent<Camera>();
        DeactivateMenuCamera();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool ActivateMenuCameraFromCurrentView()
    {
        Camera currentCamera = Camera.main;
        if (currentCamera == null)
        {
            Debug.LogError("CameraViewManager could not find an active MainCamera to copy.", this);
            return false;
        }

        CopyView(currentCamera, menuCamera);
        menuCamera.gameObject.tag = "MainCamera";
        menuCamera.enabled = true;
        return true;
    }

    public void DeactivateMenuCamera()
    {
        if (menuCamera == null)
            return;

        menuCamera.enabled = false;
        menuCamera.gameObject.tag = "Untagged";
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
