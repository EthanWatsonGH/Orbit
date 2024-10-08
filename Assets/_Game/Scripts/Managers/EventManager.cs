using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EventManager : MonoBehaviour
{
    #region Singleton Setup
    private static EventManager instance;
    public static EventManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<EventManager>();
                if (instance == null)
                {
                    GameObject eventManager = new GameObject("EventManager");
                    instance = eventManager.AddComponent<EventManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        // singleton setup
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            // TODO: this is always being destroyed for some reason. if i delete the object and place a new one in the scene it fixes it, until i restart the editor and it starts doing it again
            //Destroy(gameObject);
        }
    }
    #endregion

    public UnityEvent RecenterCameraEvent;

    void Start()
    {
        
    }

    void Update()
    {

    }

    public void RecenterCamera()
    {
        RecenterCameraEvent?.Invoke();
    }
}
