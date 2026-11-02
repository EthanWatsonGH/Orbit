using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class UIBackgroundTint : MonoBehaviour
{
    [SerializeField] UIBackgroundStyle style;
    UIBackgroundStyle subscribedStyle;
    Image image;

    void OnEnable()
    {
        Subscribe();
        Apply();
    }

    void OnDisable()
    {
        if (subscribedStyle != null)
            subscribedStyle.Changed -= Apply;
        subscribedStyle = null;
    }

    void OnValidate()
    {
        if (isActiveAndEnabled)
            Subscribe();
        Apply();
    }

    void Subscribe()
    {
        if (subscribedStyle == style) return;
        if (subscribedStyle != null)
            subscribedStyle.Changed -= Apply;
        subscribedStyle = style;
        if (subscribedStyle != null)
            subscribedStyle.Changed += Apply;
    }

    void Apply()
    {
        if (image == null)
            image = GetComponent<Image>();
        if (image != null && style != null)
            image.color = style.Tint;
    }
}
