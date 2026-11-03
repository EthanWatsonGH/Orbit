using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CodeImportPanel : MonoBehaviour
{
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button clipboardButton;
    [SerializeField] CodeClipboard clipboard;

    Func<string, string> importCode;
    public void Show(Func<string, string> onImport)
    {
        importCode = onImport;
        codeInput.richText = false;
        codeInput.SetTextWithoutNotify(string.Empty);
        statusText.text = string.Empty;
        clipboardButton.gameObject.SetActive(true);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        importCode = null;
    }

    public void Import()
    {
        if (importCode == null) return;
        if (string.IsNullOrWhiteSpace(codeInput.text))
        {
            statusText.text = "Enter a code to import.";
            return;
        }
        try
        {
            string error = importCode(codeInput.text);
            if (!string.IsNullOrEmpty(error)) statusText.text = error;
        }
        catch (Exception exception)
        {
            Debug.LogError("Code could not be imported: " + exception.Message);
            statusText.text = "Could not import the code.";
        }
    }

    public void ImportFromClipboard()
    {
        if (importCode == null) return;
        clipboard.Read(text =>
        {
            codeInput.text = text;
            Import();
        }, () => statusText.text = "Could not read the clipboard. Paste the code into the field instead.");
    }
}
