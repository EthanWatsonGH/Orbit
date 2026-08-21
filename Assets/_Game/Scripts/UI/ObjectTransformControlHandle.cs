using UnityEngine;
using UnityEngine.EventSystems;

public class ObjectTransformControlHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] ObjectTransformControl control;

    SelectionControlsUI selectionControls;
    int activePointerId = int.MinValue;

    void Awake()
    {
        selectionControls = GetComponentInParent<SelectionControlsUI>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Right and middle mouse buttons are reserved for camera panning. Touch input is
        // reported as the primary button, so it can still start a transform control.
        if (eventData.button != PointerEventData.InputButton.Left ||
            selectionControls == null || activePointerId != int.MinValue)
            return;

        activePointerId = eventData.pointerId;
        selectionControls.BeginControl(control, activePointerId, eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (selectionControls != null && eventData.pointerId == activePointerId)
            selectionControls.EndControl(control, activePointerId);

        if (eventData.pointerId == activePointerId)
            activePointerId = int.MinValue;
    }
}
