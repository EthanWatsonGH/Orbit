using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Orbit/UI Background Style")]
public class UIBackgroundStyle : ScriptableObject
{
    [SerializeField] Color tint = new Color(0f, 0f, 0f, 0.15f);

    public event Action Changed;
    public Color Tint
    {
        get => tint;
        set
        {
            if (tint == value) return;
            tint = value;
            Changed?.Invoke();
        }
    }

    void OnValidate() => Changed?.Invoke();
}
