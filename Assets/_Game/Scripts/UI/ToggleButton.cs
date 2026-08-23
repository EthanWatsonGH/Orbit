using System;
using UnityEngine;
using UnityEngine.UI;

public class ToggleButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Button button;
    [SerializeField] GameObject onVisual;
    [SerializeField] GameObject offVisual;

    [Header("State")]
    [SerializeField] bool isOn;

    public bool IsOn => isOn;
    public event Action<bool> ValueChanged;

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
        if (isOn == value)
        {
            ApplyVisualState();
            return;
        }

        isOn = value;
        ApplyVisualState();
        ValueChanged?.Invoke(isOn);
    }

    void RequestToggle()
    {
        SetIsOn(!isOn);
    }

    void ApplyVisualState()
    {
        if (onVisual != null)
            onVisual.SetActive(isOn);
        if (offVisual != null)
            offVisual.SetActive(!isOn);
    }
}
