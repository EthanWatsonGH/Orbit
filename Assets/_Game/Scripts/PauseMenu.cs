using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // ensure PauseMenu object is toggled off when starting the game
        this.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnEnable()
    {
        // pause
        Time.timeScale = 0.0f;
    }

    private void OnDisable()
    {
        // unpause
        Time.timeScale = 1.0f;
    }
}
