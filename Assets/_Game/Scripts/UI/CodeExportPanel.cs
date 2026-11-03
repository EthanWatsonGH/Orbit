using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CodeExportPanel : MonoBehaviour
{
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button clipboardButton;
    [SerializeField] CodeClipboard clipboard;

    public void Show(string code)
    {
        codeInput.readOnly = true;
        codeInput.richText = false;
        codeInput.onFocusSelectAll = true;
        codeInput.SetTextWithoutNotify(code ?? string.Empty);
        statusText.text = string.Empty;
        clipboardButton.gameObject.SetActive(true);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void CopyToClipboard()
    {
        clipboard.Write(codeInput.text, () => statusText.text = string.Empty,
            () => statusText.text = "Could not copy the code. Select the field and copy it manually.");
    }
}
