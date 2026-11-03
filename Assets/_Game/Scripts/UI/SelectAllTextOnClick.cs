using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TMP_InputField))]
public class SelectAllTextOnClick : MonoBehaviour, IPointerClickHandler
{
    TMP_InputField input;

    void Awake() => input = GetComponent<TMP_InputField>();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        input.ActivateInputField();
        input.selectionStringAnchorPosition = 0;
        input.selectionStringFocusPosition = input.text.Length;
        input.ForceLabelUpdate();
    }
}
