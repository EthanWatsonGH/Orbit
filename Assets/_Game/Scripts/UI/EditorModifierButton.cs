using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum EditorModifier
{
    Shift,
    Ctrl
}

// Add this to a held Shift or Ctrl screen button. PointerInput recognizes this
// component too, so the finger holding the modifier does not become a world
// gesture or interfere with the finger editing the level.
public class EditorModifierButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] EditorModifier modifier;
    [SerializeField] EditorHUD editorHUD;

    readonly HashSet<int> heldPointerIds = new HashSet<int>();

    void Awake()
    {
        if (editorHUD == null)
            editorHUD = GetComponentInParent<EditorHUD>();

        if (editorHUD == null)
            Debug.LogError("EditorModifierButton requires an EditorHUD reference.", this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        heldPointerIds.Add(eventData.pointerId);
        SetHeldState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        heldPointerIds.Remove(eventData.pointerId);
        SetHeldState();
    }

    void OnDisable()
    {
        heldPointerIds.Clear();
        SetHeldState();
    }

    void SetHeldState()
    {
        editorHUD?.SetModifierButtonHeld(modifier, heldPointerIds.Count > 0);
    }
}
