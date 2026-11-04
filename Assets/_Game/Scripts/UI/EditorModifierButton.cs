using System;
using UnityEngine;
using UnityEngine.UI;

[Flags]
public enum EditorModifier
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 4
}

// Identifies both the modifiers under a pointer and a touch that should not
// become a gameplay gesture when it begins on this control.
public class EditorModifierButton : PointerGestureExclusion
{
    [SerializeField] EditorModifier modifiers;
    Button button;
    Graphic targetGraphic;
    Color normalColor;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null && button.transition == Selectable.Transition.ColorTint && button.targetGraphic != null)
        {
            targetGraphic = button.targetGraphic;
            normalColor = targetGraphic.color;
            // The normal Button press state follows where a drag began. These controls
            // show the current pointer location instead, including when fingers slide.
            button.transition = Selectable.Transition.None;
        }
    }

    public EditorModifier Modifiers => isActiveAndEnabled && (button == null || (button.isActiveAndEnabled && button.IsInteractable()))
        ? modifiers
        : EditorModifier.None;

    public void SetPointerOver(bool pointerOver)
    {
        if (targetGraphic != null)
            targetGraphic.color = pointerOver ? normalColor * button.colors.pressedColor : normalColor;
    }

    void OnDisable() => SetPointerOver(false);
}
