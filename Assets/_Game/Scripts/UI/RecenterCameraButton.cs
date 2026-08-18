using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class RecenterCameraButton : MonoBehaviour
{
    [SerializeField] RectTransform icon;

    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
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
        EventManager.Instance.RecenterCamera();
    }

    void UpdateIconRotation()
    {
        if (icon == null)
            return;

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
            return;

        CameraRecenter activeCameraRecenter = activeCamera.GetComponent<CameraRecenter>();
        if (activeCameraRecenter == null || !activeCameraRecenter.TryGetRecenterPosition(out Vector3 recenterPosition))
            return;

        Vector3 targetViewportPosition = activeCamera.WorldToViewportPoint(recenterPosition);
        if (targetViewportPosition.z <= 0f || !TryGetIconViewportPosition(out Vector2 iconViewportPosition))
            return;

        Vector2 directionToTarget = (Vector2)targetViewportPosition - iconViewportPosition;

        if (directionToTarget.sqrMagnitude < 0.01f)
            return;

        float rotation = Mathf.Atan2(directionToTarget.y, directionToTarget.x) - 90f;
        icon.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    bool TryGetIconViewportPosition(out Vector2 viewportPosition)
    {
        Canvas canvas = icon.GetComponentInParent<Canvas>();
        RectTransform canvasTransform = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasTransform == null || canvasTransform.rect.width <= 0f || canvasTransform.rect.height <= 0f)
        {
            viewportPosition = default;
            return false;
        }

        // The overlay canvases are children of world objects, so their world transforms do not match
        // their rendered screen locations. Build the icon's position from its UI layout instead.
        Vector3 iconPositionInCanvas = icon.localPosition;
        Transform currentParent = icon.parent;

        while (currentParent != null && currentParent != canvasTransform)
        {
            if (!(currentParent is RectTransform parentRect))
            {
                viewportPosition = default;
                return false;
            }

            iconPositionInCanvas = parentRect.localPosition + parentRect.localRotation * Vector3.Scale(iconPositionInCanvas, parentRect.localScale);
            currentParent = parentRect.parent;
        }

        if (currentParent != canvasTransform)
        {
            viewportPosition = default;
            return false;
        }

        Rect canvasRect = canvasTransform.rect;
        viewportPosition = new Vector2(
            Mathf.InverseLerp(canvasRect.xMin, canvasRect.xMax, iconPositionInCanvas.x),
            Mathf.InverseLerp(canvasRect.yMin, canvasRect.yMax, iconPositionInCanvas.y));
        return true;
    }
}
