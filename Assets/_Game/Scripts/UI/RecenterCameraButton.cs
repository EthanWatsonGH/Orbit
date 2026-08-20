using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class RecenterCameraButton : MonoBehaviour
{
    [SerializeField] RectTransform icon;
    [SerializeField] float iconFacingAngleOffset = -90f;

    Button button;
    RectTransform buttonRect;
    Canvas canvas;

    void Awake()
    {
        button = GetComponent<Button>();
        buttonRect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        button.onClick.AddListener(RecenterCamera);
    }

    void LateUpdate()
    {
        UpdateIconRotation();
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(RecenterCamera);
    }

    void RecenterCamera()
    {
        if (EventManager.Instance != null)
            EventManager.Instance.RecenterCamera();
    }

    void UpdateIconRotation()
    {
        if (icon == null)
            return;

        if (CameraViewManager.Instance == null ||
            !CameraViewManager.Instance.TryGetActiveWorldCamera(out Camera activeCamera))
            return;

        CameraRecenter activeCameraRecenter = activeCamera.GetComponent<CameraRecenter>();
        if (activeCameraRecenter == null || !activeCameraRecenter.TryGetTargetPosition(out Vector3 targetPosition))
            return;

        Vector3 targetScreenPosition = activeCamera.WorldToScreenPoint(targetPosition);
        if (targetScreenPosition.z <= 0f)
            return;

        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector3 buttonCenterWorldPosition = buttonRect.TransformPoint(buttonRect.rect.center);
        Vector2 buttonScreenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, buttonCenterWorldPosition);
        Vector2 directionToTarget = (Vector2)targetScreenPosition - buttonScreenPosition;

        if (directionToTarget.sqrMagnitude < 0.01f)
            return;

        float rotation = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg + iconFacingAngleOffset;
        icon.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }
}
