using UnityEngine;
using UnityEngine.UI;

// Keeps a fixed number of cards across while adapting their size to the viewport.
[RequireComponent(typeof(GridLayoutGroup))]
public class ResponsiveCardGrid : MonoBehaviour
{
    [SerializeField] RectTransform viewport;
    [SerializeField, Min(1)] int columnCount = 5;

    GridLayoutGroup grid;
    float heightToWidth;
    float lastWidth = -1f;

    void Awake() => Initialize();

    void Initialize()
    {
        if (grid != null) return;
        grid = GetComponent<GridLayoutGroup>();
        heightToWidth = grid.cellSize.x > 0f ? grid.cellSize.y / grid.cellSize.x : 1f;
    }

    void LateUpdate()
    {
        if (viewport != null && !Mathf.Approximately(lastWidth, viewport.rect.width))
            Refresh();
    }

    public void Refresh()
    {
        Initialize();
        if (viewport == null) return;

        lastWidth = viewport.rect.width;
        int columns = Mathf.Max(1, columnCount);
        float width = lastWidth - grid.padding.horizontal - grid.spacing.x * (columns - 1);
        float cardWidth = Mathf.Max(1f, width / columns);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2(cardWidth, cardWidth * heightToWidth);
    }
}
