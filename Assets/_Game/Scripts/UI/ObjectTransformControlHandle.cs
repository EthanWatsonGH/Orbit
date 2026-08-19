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
        if (selectionControls == null || activePointerId != int.MinValue)
            return;

        activePointerId = eventData.pointerId;
        selectionControls.BeginControl(control, eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (selectionControls != null && eventData.pointerId == activePointerId)
            selectionControls.EndControl(control, eventData.position, eventData.pressPosition);

        if (eventData.pointerId == activePointerId)
            activePointerId = int.MinValue;
    }
}
