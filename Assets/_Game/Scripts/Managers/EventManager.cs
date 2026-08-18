using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DefaultExecutionOrder(-100)]
public class EventManager : MonoBehaviour
{
    #region Singleton Setup
    public static EventManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Duplicate EventManager in the scene.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    #endregion

    public UnityEvent RecenterCameraEvent;
    public UnityEvent UnselectObjectEvent;
    public UnityEvent ShowPlayerInWorldUiElementsEvent;
    public UnityEvent HidePlayerInWorldUiElementsEvent;
    public UnityEvent OnLevelLoadEvent;

    public void RecenterCamera()
    {
        RecenterCameraEvent?.Invoke();
    }

    public void UnselectObject()
    {
        UnselectObjectEvent?.Invoke();
    }

    public void ShowPlayerInWorldUiElements()
    {
        ShowPlayerInWorldUiElementsEvent?.Invoke();
    }

    public void HidePlayerInWorldUiElements() 
    {
        HidePlayerInWorldUiElementsEvent?.Invoke();
    }

    public void OnLevelLoad()
    {
        OnLevelLoadEvent?.Invoke();
    }
}
