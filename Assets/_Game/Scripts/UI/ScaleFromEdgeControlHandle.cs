using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class ScaleFromEdgeControlHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    ScaleFromEdgeHandle handle;
    ScaleFromEdgeControlsUI controls;
    int activePointerId = int.MinValue;

    void Awake()
    {
        controls = GetComponentInParent<ScaleFromEdgeControlsUI>();

        // The prefab's direct child names are the source of truth for their handles,
        // so it remains setup-free when a visual is replaced.
        if (!Enum.TryParse(gameObject.name, out handle))
        {
            Debug.LogError($"Scale From Edge control '{gameObject.name}' must use one of the ScaleFromEdgeHandle names.", this);
            enabled = false;
        }
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
