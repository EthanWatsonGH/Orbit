using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Keeps UI hit testing separate from PointerInput's raw pointer state. Systems
// that need a UI policy can ask this helper at the point they make that decision.
public static class UiHitTest
{
    static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    public static bool IsScreenPositionOverUi(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, raycastResults);
        return raycastResults.Count > 0;
    }
}
