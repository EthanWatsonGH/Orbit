using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// One pointer owns an entry gesture until it becomes either scrolling or placement.
public class PrefabBarEntry : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const string BuiltInIconResourceFolder = "BaseObjectIcons";

    [SerializeField] string builtInType;
    [SerializeField] bool scrollOnly;
    string savedId;
    PrefabBar bar;
    int pointerId = int.MinValue;
    Vector2 pressPosition;
    enum Gesture { Undecided, Scroll, Upward, Place, Rejected }
    Gesture gesture;

    public string BuiltInType => builtInType;
    public Sprite BuiltInIcon { get; private set; }

    void Awake() => bar = GetComponentInParent<PrefabBar>();

    public void ConfigureSaved(string id)
    {
        savedId = id;
        builtInType = null;
    }

    public void SetBuiltInIcon(Sprite icon)
    {
        BuiltInIcon = icon;
        if (icon == null)
            return;

        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.color = Color.clear;
            // The transparent image remains the pointer target for dragging the entry.
            background.canvasRenderer.cullTransparentMesh = false;
        }
        TMP_Text label = GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.gameObject.SetActive(false);

        GameObject iconObject = new GameObject("Built-in Icon", typeof(RectTransform), typeof(Image));
        iconObject.layer = gameObject.layer;
        iconObject.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)iconObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = iconObject.GetComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || bar == null || pointerId != int.MinValue ||
            !bar.BeginGesture(eventData.pointerId))
            return;
        pointerId = eventData.pointerId;
        pressPosition = eventData.position;
        gesture = Gesture.Undecided;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (bar != null && eventData.button == PointerEventData.InputButton.Left)
            bar.ScrollView?.OnInitializePotentialDrag(eventData);
        eventData.useDragThreshold = false;
    }
    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (bar == null || eventData.pointerId != pointerId)
            return;

        Vector2 delta = eventData.position - pressPosition;
        if (gesture == Gesture.Undecided)
        {
            float horizontal = Mathf.Abs(delta.x);
            float vertical = Mathf.Abs(delta.y);
            if (horizontal >= bar.DragThresholdPixels && horizontal > vertical)
            {
                gesture = Gesture.Scroll;
                bar.ScrollView?.OnBeginDrag(eventData);
            }
            else if (vertical >= bar.DragThresholdPixels && vertical >= horizontal)
                gesture = delta.y > 0f ? Gesture.Upward : Gesture.Rejected;
        }

        if (gesture == Gesture.Scroll)
            bar.ScrollView?.OnDrag(eventData);
        else if (gesture == Gesture.Upward && bar.IsAboveBar(eventData.position, eventData.pressEventCamera))
        {
            gesture = !scrollOnly && bar.BeginPlacement(builtInType, savedId, pointerId, eventData.position)
                ? Gesture.Place : Gesture.Rejected;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (bar == null || eventData.pointerId != pointerId)
            return;
        if (gesture == Gesture.Place)
            bar.EndPlacement(pointerId, eventData.position, WasTouchCanceled(pointerId));
        else if (gesture == Gesture.Scroll)
            bar.ScrollView?.OnEndDrag(eventData);
        EndGesture();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (bar == null || eventData.pointerId != pointerId)
            return;
        if (gesture == Gesture.Scroll)
            bar.ScrollView?.OnEndDrag(eventData);
        EndGesture();
    }

    void OnDisable()
    {
        if (bar != null && pointerId != int.MinValue)
        {
            if (gesture == Gesture.Place)
                bar.EndPlacement(pointerId, Vector2.zero, true);
            EndGesture();
        }
    }

    void EndGesture()
    {
        if (pointerId == int.MinValue)
            return;
        int releasedPointerId = pointerId;
        pointerId = int.MinValue;
        gesture = Gesture.Undecided;
        bar.EndGesture(releasedPointerId);
    }

    static bool WasTouchCanceled(int id)
    {
        if (id < 0)
            return false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == id)
                return touch.phase == TouchPhase.Canceled;
        }
        // Some input modules deliver pointer-up after removing the ended touch.
        // Only an explicit canceled phase should discard an otherwise valid drop.
        return false;
    }
}
