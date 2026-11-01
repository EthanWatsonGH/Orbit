using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class ScaleFromEdgeControlHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ICanvasRaycastFilter
{
    ScaleFromEdgeHandle handle;
    SelectionControlsUI controls;
    int activePointerId = int.MinValue;

    void Awake()
    {
        controls = GetComponentInParent<SelectionControlsUI>();

        // The prefab's direct child names are the source of truth for their handles,
        // so it remains setup-free when a visual is replaced.
        if (!Enum.TryParse(gameObject.name, out handle))
        {
            Debug.LogError($"Scale From Edge control '{gameObject.name}' must use one of the ScaleFromEdgeHandle names.", this);
            enabled = false;
        }
    }

    public bool IsRaycastLocationValid(Vector2 screenPosition, Camera eventCamera)
    {
        if (!(transform is RectTransform rectTransform) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, eventCamera, out Vector2 local))
            return false;

        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return false;

        // Match the circular icon so the corners of rotated UI rectangles cannot
        // intercept touches intended for a neighboring control.
        Vector2 offset = local - rect.center;
        float x = offset.x / (rect.width * 0.5f);
        float y = offset.y / (rect.height * 0.5f);
        return x * x + y * y <= 1f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Right and middle mouse buttons remain reserved for camera panning. Touch
        // input is reported as the primary button, so it can start a scale gesture.
        if (eventData.button != PointerEventData.InputButton.Left ||
            controls == null || activePointerId != int.MinValue)
            return;

        activePointerId = eventData.pointerId;
        controls.BeginControl(handle, activePointerId, eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (controls != null && eventData.pointerId == activePointerId)
            controls.EndControl(handle, activePointerId);

        if (eventData.pointerId == activePointerId)
            activePointerId = int.MinValue;
    }
}
