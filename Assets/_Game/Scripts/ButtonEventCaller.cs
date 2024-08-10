using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonEventCaller : MonoBehaviour
{
    public void RecenterCamera()
    {
        EventManager.Instance.RecenterCamera();
    }
}
