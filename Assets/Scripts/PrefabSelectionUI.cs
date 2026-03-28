using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System;


public class PrefabSelectionUI : MonoBehaviour
{
   
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

    // ── State ───────────────────────────────────────────────────────────────

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