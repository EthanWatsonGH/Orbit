using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

public class SanitizeStringForFileName : MonoBehaviour
{
    public void SanitizeTextInput()
    {
        TMP_InputField inputField = gameObject.GetComponent<TMP_InputField>();

        inputField.text = new string(inputField.text.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray());
    }
}
