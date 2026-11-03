using UnityEngine;

// The browser warning belongs to the clipboard button, not to a particular panel.
public class ClipboardButtonWarning : MonoBehaviour
{
    [SerializeField] RectTransform label;
    [SerializeField] GameObject browserWarning;

    void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        browserWarning.SetActive(true);
        label.anchorMin = new Vector2(0f, 0.45f);
#else
        browserWarning.SetActive(false);
        label.anchorMin = Vector2.zero;
#endif
        label.anchorMax = Vector2.one;
        label.offsetMin = Vector2.zero;
        label.offsetMax = Vector2.zero;
    }
}
