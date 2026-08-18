using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ToggleButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Button button;
    [SerializeField] GameObject onVisual;
    [SerializeField] GameObject offVisual;

    [Header("Events")]
    [SerializeField] UnityEvent<bool> valueChangeRequested;

    bool isOn;

    void Awake()
    {
        if (button == null)
        {
            Debug.LogError("ERROR: ToggleButton is missing its Button reference.", this);
            return;
        }

        button.onClick.AddListener(RequestToggle);
        ApplyVisualState();
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(RequestToggle);
    }

    public void SetIsOn(bool value)
    {
        isOn = value;
        ApplyVisualState();
    }

    void RequestToggle()
    {
        // The owning setting decides whether to accept this change, then calls SetIsOn to update the visuals.
        valueChangeRequested?.Invoke(!isOn);
    }

    void ApplyVisualState()
    {
        if (onVisual != null)
            onVisual.SetActive(isOn);
        if (offVisual != null)
            offVisual.SetActive(!isOn);
    }
}
