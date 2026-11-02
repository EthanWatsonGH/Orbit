using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PrefabLibraryCard : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image preview;
    [SerializeField] TMP_Text title;
    [SerializeField] GameObject gameBadge;

    public void Configure(string displayName, Sprite sprite, bool builtIn, System.Action onSelect)
    {
        title.text = displayName;
        title.enableAutoSizing = true;
        title.fontSizeMin = 11;
        title.fontSizeMax = 18;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMax = new Vector2(1, sprite != null ? .25f : 1f);
        titleRect.offsetMin = new Vector2(4, 2);
        titleRect.offsetMax = new Vector2(-4, -2);
        preview.sprite = sprite;
        preview.enabled = sprite != null;
        gameBadge.SetActive(builtIn);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSelect());
    }
}
