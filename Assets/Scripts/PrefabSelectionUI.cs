using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// Bottom carousel prefab selector — mimics the default Unity AR template style.
///
/// FULL SETUP GUIDE:
/// ─────────────────────────────────────────────────────────────────────────────
///
/// 1. CANVAS SETUP
///    • Create a Canvas (Screen Space – Overlay, or Camera if you prefer)
///    • Add a SafeArea script on Canvas (you already have this)
///    • Set Canvas Scaler → Scale With Screen Size → 1080x1920
///
/// 2. BOTTOM BAR  (anchor: bottom-stretch)
///    • Child of Canvas → name "BottomBar"
///    • RectTransform: Anchor Min (0, 0) Max (1, 0), Pivot (0.5, 0)
///    • Height: 180
///    • Add Image component → color #1A1A1A at alpha ~200 (dark pill)
///    • Optional: add a CanvasGroup for fade-in on start
///
/// 3. SCROLL RECT  (inside BottomBar)
///    • Add child → name "ScrollRect"
///    • Add ScrollRect component → Horizontal=ON, Vertical=OFF
///    • Drag=0.92 (feels natural), Inertia=ON, Deceleration=0.135
///    • Leave Viewport and Content empty for now
///
/// 4. VIEWPORT  (child of ScrollRect GameObject)
///    • Name "Viewport", add Mask + Image (any sprite, color alpha=0 to hide)
///    • Assign to ScrollRect → Viewport field
///    • RectTransform: stretch inside ScrollRect, small horizontal padding (16px each side)
///
/// 5. CONTENT  (child of Viewport)
///    • Name "Content"
///    • Add Horizontal Layout Group:
///      Spacing=16, Child Alignment=Middle Center
///      Child Force Expand Width=OFF, Height=OFF
///    • Add Content Size Fitter → Horizontal=Preferred Size
///    • Assign to ScrollRect → Content field
///
/// 6. BUTTON PREFAB  (save as prefab in Assets/Prefabs)
///    • Root: empty GameObject, RectTransform 120x120
///    • Add Button component
///    • Add CanvasGroup component (for alpha fading)
///    • Child 1: "Icon" → Image (120x120) → your prefab thumbnail sprite
///    • Child 2: "Label" → TextMeshPro → font size 14, center-aligned, white
///    • Add a subtle rounded rect Image as background on root (optional)
///
/// 7. WIRE UP
///    • Attach THIS script (PrefabSelectionUI) to the BottomBar
///    • Assign:
///        buttonPrefab   → your Button Prefab from step 6
///        contentParent  → Content from step 5
///        scrollRect     → ScrollRect component from step 3
///        prefabs        → drag your AR prefabs in the list
///        icons          → matching thumbnail sprites (optional, same order)
///
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
public class PrefabSelectionUI : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("References — REQUIRED")]
    [Tooltip("The button prefab (root: Button + CanvasGroup; child 'Icon': Image; child 'Label': TMP_Text)")]
    public GameObject buttonPrefab;

    [Tooltip("The Content RectTransform inside your ScrollRect → Viewport → Content")]
    public RectTransform contentParent;

    [Tooltip("The ScrollRect component")]
    public ScrollRect scrollRect;

    [Header("Prefabs to display")]
    [Tooltip("Your AR object prefabs — shown in this order")]
    public List<GameObject> prefabs = new();

    [Tooltip("Optional thumbnail sprites — must match prefabs list order. Leave empty to use text-only buttons.")]
    public List<Sprite> icons = new();

    // ── Carousel visuals ──────────────────────────────────────────────────────

    [Header("Carousel Effect")]
    [Tooltip("Scale of the centered (selected) item")]
    [Range(1f, 2f)]
    public float centerScale = 1.25f;

    [Tooltip("Scale of the furthest items")]
    [Range(0.4f, 1f)]
    public float edgeScale = 0.7f;

    [Tooltip("Alpha of the furthest items")]
    [Range(0f, 1f)]
    public float edgeAlpha = 0.4f;

    [Tooltip("How fast the carousel snaps to center")]
    public float snapSpeed = 12f;

    [Tooltip("Pixel distance over which scale/alpha falls off")]
    public float falloffDistance = 300f;

    // ── State ─────────────────────────────────────────────────────────────────

    readonly List<RectTransform> _buttons = new();
    int _selectedIndex = 0;
    bool _isDragging = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        BuildMenu();

        // Select first item by default
        if (prefabs.Count > 0)
            SelectAt(0);
    }

    void Update()
    {
        UpdateCarouselVisuals();

        if (!_isDragging)
            SnapToCenter();

        AutoSelectCenterItem();
    }

    // ── Menu Generation ───────────────────────────────────────────────────────

    void BuildMenu()
    {
        // Clear any existing children (useful for Editor reload)
        foreach (Transform t in contentParent)
            Destroy(t.gameObject);

        _buttons.Clear();

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            int index = i; // capture for closure

            GameObject btnObj = Instantiate(buttonPrefab, contentParent);
            btnObj.name = $"Btn_{prefab.name}";

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            _buttons.Add(rect);

            // ── Set label ────────────────────────────────────────────────────
            TMP_Text label = btnObj.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = prefab.name;

            // ── Set icon ─────────────────────────────────────────────────────
            if (icons != null && i < icons.Count && icons[i] != null)
            {
                // Find child named "Icon" or fall back to first Image on root
                Image iconImage = FindChildImage(btnObj, "Icon");
                if (iconImage != null)
                    iconImage.sprite = icons[i];
            }

            // ── Wire up click ─────────────────────────────────────────────────
            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => SelectAt(index));
        }

        // Tell ScrollRect to track drag state so we pause snapping while dragging
        EventTriggerHelper.AddDragCallbacks(
            scrollRect,
            onBegin: () => _isDragging = true,
            onEnd:   () => _isDragging = false
        );
    }

    // ── Selection ──────────────────────────────────────────────────────────────

    void SelectAt(int index)
    {
        if (index < 0 || index >= prefabs.Count) return;
        _selectedIndex = index;
        PrefabSelector.Select(prefabs[index]);
    }

    // ── Carousel Visuals ───────────────────────────────────────────────────────

    void UpdateCarouselVisuals()
    {
        foreach (RectTransform btn in _buttons)
        {
            float dist = Mathf.Abs(
                scrollRect.viewport.transform.position.x -
                btn.transform.position.x
            );

            float t = Mathf.Clamp01(dist / falloffDistance);

            // Scale
            float scale = Mathf.Lerp(centerScale, edgeScale, t);
            btn.localScale = Vector3.one * scale;

            // Alpha
            CanvasGroup cg = btn.GetComponent<CanvasGroup>();
            if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = Mathf.Lerp(1f, edgeAlpha, t);
        }
    }

    // ── Snap To Center ─────────────────────────────────────────────────────────

    void SnapToCenter()
    {
        RectTransform closest = GetClosestButton();
        if (closest == null) return;

        Vector2 delta =
            (Vector2)scrollRect.transform.InverseTransformPoint(contentParent.position) -
            (Vector2)scrollRect.transform.InverseTransformPoint(closest.position);

        contentParent.anchoredPosition = Vector2.Lerp(
            contentParent.anchoredPosition,
            contentParent.anchoredPosition + delta,
            Time.deltaTime * snapSpeed
        );
    }

    // ── Auto-select whichever item is in the center ────────────────────────────

    void AutoSelectCenterItem()
    {
        RectTransform closest = GetClosestButton();
        if (closest == null) return;

        int index = _buttons.IndexOf(closest);
        if (index >= 0 && index < prefabs.Count && index != _selectedIndex)
            SelectAt(index);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    RectTransform GetClosestButton()
    {
        float closestDist = float.MaxValue;
        RectTransform result = null;

        foreach (RectTransform btn in _buttons)
        {
            float dist = Mathf.Abs(
                scrollRect.viewport.transform.position.x -
                btn.transform.position.x
            );
            if (dist < closestDist)
            {
                closestDist = dist;
                result = btn;
            }
        }

        return result;
    }

    Image FindChildImage(GameObject root, string childName)
    {
        Transform t = root.transform.Find(childName);
        if (t != null) return t.GetComponent<Image>();
        return root.GetComponent<Image>(); // fallback
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Helper: adds begin/end drag callbacks to a ScrollRect without needing
//         a separate MonoBehaviour on it.
// ─────────────────────────────────────────────────────────────────────────────

public static class EventTriggerHelper
{
    public static void AddDragCallbacks(
        ScrollRect scrollRect,
        Action onBegin,
        Action onEnd)
    {
        EventTrigger trigger = scrollRect.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = scrollRect.gameObject.AddComponent<EventTrigger>();

        // Begin drag
        var beginEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.BeginDrag
        };
        beginEntry.callback.AddListener(_ => onBegin());
        trigger.triggers.Add(beginEntry);

        // End drag
        var endEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.EndDrag
        };
        endEntry.callback.AddListener(_ => onEnd());
        trigger.triggers.Add(endEntry);
    }
}