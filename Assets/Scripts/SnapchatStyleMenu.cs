using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Snapchat-style prefab selection menu.
///
/// HIERARCHY TO BUILD:
/// ─────────────────────────────────────────────────────────────────────
/// Canvas (Screen Space Overlay, Scaler → Scale With Screen Size 1080×1920)
///   └── SafeArea               ← your existing SafeArea.cs
///         └── SelectionTray    ← this script goes HERE
///               ├── ScrollRect
///               │     ├── Viewport (Mask + Image alpha=0)
///               │     └── Content  (HorizontalLayoutGroup, ContentSizeFitter)
///               ├── LeftArrow  (Button)
///               ├── RightArrow (Button)
///               └── DistanceLabel (TMP_Text, hidden by default)
/// ─────────────────────────────────────────────────────────────────────
///
/// SelectionTray RectTransform:
///   Anchor: bottom-center   (Min 0,0 Max 1,0  Pivot 0.5,0)
///   Height: 220
///   Left/Right: 0
///
/// ScrollRect settings:
///   Horizontal=ON  Vertical=OFF  MovementType=Elastic
///   Inertia=ON  DecelerationRate=0.135  ScrollSensitivity=20
///
/// Content RectTransform:
///   HorizontalLayoutGroup: Spacing=12  Padding Left/Right=24
///   ChildForceExpand Width=OFF Height=OFF
///   ChildAlignment=MiddleCenter
///   ContentSizeFitter: Horizontal=PreferredSize
///
/// CARD PREFAB (saved as prefab → assign to cardPrefab):
///   Root: RectTransform 140×180, add Button + CanvasGroup
///   ├── Background: Image, full stretch, sprite=rounded-rect (or none)
///   ├── Icon: Image, anchored centre, size 110×110
///   ├── Label: TMP_Text, anchored bottom, height 30, font-size 13, center
///   └── SelectedRing: Image, full stretch, sprite=ring/border, color=accent, default alpha=0
/// ─────────────────────────────────────────────────────────────────────
/// </summary>
public class SnapchatStyleMenu : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Required references")]
    public GameObject   cardPrefab;
    public RectTransform contentParent;
    public ScrollRect   scrollRect;
    public Button       leftArrow;
    public Button       rightArrow;

    [Header("Prefabs + Thumbnails")]
    public List<GameObject> prefabs = new();
    public List<Sprite>     icons   = new();   // same order, can be empty

    [Header("Card sizing")]
    public float cardWidth    = 140f;
    public float cardSpacing  = 12f;

    [Header("Snap / selection")]
    public float snapSpeed    = 14f;
    public float scaleCenter  = 1.18f;   // scale of the centred card
    public float scaleEdge    = 0.82f;
    public float alphaCenter  = 1f;
    public float alphaEdge    = 0.45f;
    public float falloff      = 260f;    // px from centre before fully shrunk

    [Header("Optional distance label")]
    public TMP_Text distanceLabel;
    public float    distanceLabelDuration = 2f;

    // ── State ─────────────────────────────────────────────────────────────────
    readonly List<CardEntry> _cards = new();
    int   _selectedIndex  = 0;
    bool  _isDragging     = false;
    float _distanceTimer  = 0f;

    struct CardEntry
    {
        public RectTransform rect;
        public CanvasGroup   group;
        public Image         ring;        // selection highlight
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        BuildCards();
        RegisterScrollDrag();

        if (leftArrow)  leftArrow.onClick.AddListener(() => StepTo(_selectedIndex - 1));
        if (rightArrow) rightArrow.onClick.AddListener(() => StepTo(_selectedIndex + 1));

        if (distanceLabel) distanceLabel.gameObject.SetActive(false);

        // Select first item
        StepTo(0, instant: true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        ApplyCarouselVisuals();
        if (!_isDragging) SnapToSelected();
        AutoSelectCentre();

        if (_distanceTimer > 0f)
        {
            _distanceTimer -= Time.deltaTime;
            if (_distanceTimer <= 0f && distanceLabel)
                distanceLabel.gameObject.SetActive(false);
        }
    }

    // ── Build ─────────────────────────────────────────────────────────────────
    void BuildCards()
    {
        foreach (Transform t in contentParent) Destroy(t.gameObject);
        _cards.Clear();

        for (int i = 0; i < prefabs.Count; i++)
        {
            int idx = i;

            var go = Instantiate(cardPrefab, contentParent);
            go.name = $"Card_{prefabs[i].name}";

            // Label
            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = prefabs[i].name;

            // Icon — find child named "Icon"
            var iconT = go.transform.Find("Icon");
            if (iconT && icons != null && i < icons.Count && icons[i])
                iconT.GetComponent<Image>().sprite = icons[i];

            // Selection ring — find child named "SelectedRing"
            Image ring = null;
            var ringT = go.transform.Find("SelectedRing");
            if (ringT) ring = ringT.GetComponent<Image>();
            if (ring) ring.color = new Color(ring.color.r, ring.color.g, ring.color.b, 0f);

            // Click
            go.GetComponent<Button>().onClick.AddListener(() => StepTo(idx));

            var entry = new CardEntry
            {
                rect  = go.GetComponent<RectTransform>(),
                group = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>(),
                ring  = ring
            };
            _cards.Add(entry);
        }
    }

    // ── Step to index ─────────────────────────────────────────────────────────
    public void StepTo(int index, bool instant = false)
    {
        index = Mathf.Clamp(index, 0, prefabs.Count - 1);
        _selectedIndex = index;
        PrefabSelector.Select(prefabs[index]);

        if (instant) ScrollToIndex(index, snap: true);

        // Arrow visibility
        if (leftArrow)  leftArrow.interactable  = index > 0;
        if (rightArrow) rightArrow.interactable = index < prefabs.Count - 1;
    }

    // ── Visuals ───────────────────────────────────────────────────────────────
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

            // Selection ring
            if (c.ring != null)
            {
                float targetAlpha = (i == _selectedIndex) ? 1f : 0f;
                var col = c.ring.color;
                col.a = Mathf.Lerp(col.a, targetAlpha, Time.deltaTime * 10f);
                c.ring.color = col;
            }
        }
    }

    // ── Snap ──────────────────────────────────────────────────────────────────
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

    // ── Auto-select whatever is closest to centre ─────────────────────────────
    void AutoSelectCentre()
    {
        if (scrollRect == null || _isDragging) return;

        float vx = scrollRect.viewport.transform.position.x;
        float closest = float.MaxValue;
        int   closestIdx = _selectedIndex;

        for (int i = 0; i < _cards.Count; i++)
        {
            float d = Mathf.Abs(vx - _cards[i].rect.transform.position.x);
            if (d < closest) { closest = d; closestIdx = i; }
        }

        if (closestIdx != _selectedIndex)
            StepTo(closestIdx);
    }

    // ── Drag tracking ──────────────────────────────────────────────────────────
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
        end.callback.AddListener(_ => _isDragging = false);
        trigger.triggers.Add(end);
    }

    // ── Distance label (called from ARPrefabPlacer pinch) ─────────────────────
    public void ShowDistanceLabel(float metres)
    {
        if (!distanceLabel) return;
        distanceLabel.text = $"{metres:F1}m";
        distanceLabel.gameObject.SetActive(true);
        _distanceTimer = distanceLabelDuration;
    }
}