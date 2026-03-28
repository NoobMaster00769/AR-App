using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SnapchatStyleMenu : MonoBehaviour
{
    [Header("Required references")]
    public GameObject    cardPrefab;
    public RectTransform contentParent;
    public ScrollRect    scrollRect;
    public Button        leftArrow;
    public Button        rightArrow;

    [Header("Prefabs + Thumbnails")]
    public List<GameObject> prefabs = new();
    public List<Sprite>     icons   = new();

    [Header("Card sizing")]
    public float cardWidth   = 140f;
    public float cardSpacing = 12f;

    [Header("Snap / selection")]
    public float snapSpeed   = 14f;
    public float scaleCenter = 1.18f;
    public float scaleEdge   = 0.82f;
    public float alphaCenter = 1f;
    public float alphaEdge   = 0.45f;
    public float falloff     = 260f;

    [Header("Optional distance label")]
    public TMP_Text distanceLabel;
    public float    distanceLabelDuration = 2f;

    readonly List<CardEntry> _cards = new();
    int   _selectedIndex = 0;
    bool  _isDragging    = false;
    float _distanceTimer = 0f;

    struct CardEntry
    {
        public RectTransform rect;
        public CanvasGroup   group;
        public Image         ring;
    }

    void Start()
    {
        Debug.Log("🚀 Snapchat menu started. Prefabs count: " + prefabs.Count);

        BuildCards();
        RegisterScrollDrag();

        if (leftArrow)
        {
            leftArrow.onClick.RemoveAllListeners();
            leftArrow.onClick.AddListener(() => StepTo(_selectedIndex - 1));
        }

        if (rightArrow)
        {
            rightArrow.onClick.RemoveAllListeners();
            rightArrow.onClick.AddListener(() => StepTo(_selectedIndex + 1));
        }

        if (distanceLabel) distanceLabel.gameObject.SetActive(false);

        StepTo(0, instant: true);
    }

    void Update()
    {
        ApplyCarouselVisuals();

        // Only snap scroll position — never change _selectedIndex here
        if (!_isDragging) SnapToSelected();

        if (_distanceTimer > 0f)
        {
            _distanceTimer -= Time.deltaTime;
            if (_distanceTimer <= 0f && distanceLabel)
                distanceLabel.gameObject.SetActive(false);
        }
    }

    void BuildCards()
    {
        foreach (Transform t in contentParent) Destroy(t.gameObject);
        _cards.Clear();

        for (int i = 0; i < prefabs.Count; i++)
        {
            int idx = i;
            var go = Instantiate(cardPrefab, contentParent);
            go.name = $"Card_{prefabs[i].name}";

            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = prefabs[i].name;

            var iconT = go.transform.Find("Icon");
            if (iconT && icons != null && i < icons.Count && icons[i])
                iconT.GetComponent<Image>().sprite = icons[i];

            Image ring = null;
            var ringT = go.transform.Find("SelectedRing");
            if (ringT) ring = ringT.GetComponent<Image>();
            if (ring) ring.color = new Color(ring.color.r, ring.color.g, ring.color.b, 0f);

            go.GetComponent<Button>().onClick.AddListener(() => StepTo(idx));

            _cards.Add(new CardEntry
            {
                rect  = go.GetComponent<RectTransform>(),
                group = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>(),
                ring  = ring
            });
        }
    }

    public void StepTo(int index, bool instant = false)
    {
        if (prefabs.Count == 0) { Debug.LogError("❌ Prefabs list is EMPTY"); return; }

        index = Mathf.Clamp(index, 0, prefabs.Count - 1);
        _selectedIndex = index;

        Debug.Log("👉 Selected: " + index + " | " + prefabs[index].name);
        PrefabSelector.Select(prefabs[index]);

        ScrollToIndex(index, snap: instant);

        if (leftArrow)  leftArrow.interactable  = index > 0;
        if (rightArrow) rightArrow.interactable = index < prefabs.Count - 1;
    }

    void ApplyCarouselVisuals()
    {
        if (scrollRect == null) return;
        float viewportX = scrollRect.viewport.transform.position.x;

        for (int i = 0; i < _cards.Count; i++)
        {
            var c = _cards[i];
            float dist = Mathf.Abs(viewportX - c.rect.transform.position.x);
            float t    = Mathf.Clamp01(dist / falloff);

            c.rect.localScale = Vector3.one * Mathf.Lerp(scaleCenter, scaleEdge, t);
            c.group.alpha     = Mathf.Lerp(alphaCenter, alphaEdge, t);

            if (c.ring != null)
            {
                float targetAlpha = (i == _selectedIndex) ? 1f : 0f;
                var col = c.ring.color;
                col.a = Mathf.Lerp(col.a, targetAlpha, Time.deltaTime * 10f);
                c.ring.color = col;
            }
        }
    }

    void SnapToSelected()
    {
        if (_cards.Count == 0 || _selectedIndex >= _cards.Count) return;
        ScrollToIndex(_selectedIndex);
    }

    void ScrollToIndex(int index, bool snap = false)
    {
        if (_cards.Count == 0) return;
        var target = _cards[index].rect;

        Vector2 delta =
            (Vector2)scrollRect.transform.InverseTransformPoint(contentParent.position) -
            (Vector2)scrollRect.transform.InverseTransformPoint(target.position);

        if (snap)
            contentParent.anchoredPosition += delta;
        else
            contentParent.anchoredPosition = Vector2.Lerp(
                contentParent.anchoredPosition,
                contentParent.anchoredPosition + delta,
                Time.deltaTime * snapSpeed);
    }

    // After dragging ends, snap to whichever card is closest — this is the
    // ONLY place selection changes from physical scroll position
    void OnDragEnd()
    {
        _isDragging = false;

        if (scrollRect == null) return;
        float vx = scrollRect.viewport.transform.position.x;
        float closest = float.MaxValue;
        int   closestIdx = _selectedIndex;

        for (int i = 0; i < _cards.Count; i++)
        {
            float d = Mathf.Abs(vx - _cards[i].rect.transform.position.x);
            if (d < closest) { closest = d; closestIdx = i; }
        }

        StepTo(closestIdx);
    }

    void RegisterScrollDrag()
    {
        if (!scrollRect) return;

        var trigger = scrollRect.GetComponent<UnityEngine.EventSystems.EventTrigger>()
                   ?? scrollRect.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var begin = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.BeginDrag };
        begin.callback.AddListener(_ => _isDragging = true);
        trigger.triggers.Add(begin);

        var end = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.EndDrag };
        end.callback.AddListener(_ => OnDragEnd()); // ← calls OnDragEnd, not just a flag
        trigger.triggers.Add(end);
    }

    public void ShowDistanceLabel(float metres)
    {
        if (!distanceLabel) return;
        distanceLabel.text = $"{metres:F1}m";
        distanceLabel.gameObject.SetActive(true);
        _distanceTimer = distanceLabelDuration;
    }
}