using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class ARPrefabPlacer : MonoBehaviour
{
    [Header("References")]
    public ARRaycastManager raycastManager;
    public PlacementReticle placementReticle;
    public SnapchatStyleMenu selectionMenu;

    [Header("Air Placement Distance")]
    public float airDistance = 2.0f;
    public float minAirDistance = 0.5f;
    public float maxAirDistance = 8.0f;
    public float pinchSensitivity = 0.018f;

    [Header("Scale")]
    public float targetRealWorldSize = 0.15f;
    public float fallbackScale = 0.12f;

    [Header("Physics")]
    public float mass = 0.5f;
    public float drag = 0.4f;
    public float angularDrag = 0.4f;

    static readonly List<ARRaycastHit> _hits = new();
    float _prevPinchDist = -1f;

    float lastTapTime = 0f;
    float tapCooldown = 0.2f;

    void Update()
    {
        HandlePinch();
        HandleTap();
    }

    // ───────────────────────── PINCH ─────────────────────────
    void HandlePinch()
    {
        if (Input.touchCount != 2)
        {
            _prevPinchDist = -1f;
            return;
        }

        float dist = Vector2.Distance(
            Input.GetTouch(0).position,
            Input.GetTouch(1).position);

        if (_prevPinchDist < 0f)
        {
            _prevPinchDist = dist;
            return;
        }

        float delta = dist - _prevPinchDist;
        _prevPinchDist = dist;

        airDistance = Mathf.Clamp(
            airDistance + delta * pinchSensitivity,
            minAirDistance, maxAirDistance);

        selectionMenu?.ShowDistanceLabel(airDistance);
    }

    // ───────────────────────── TAP ─────────────────────────
    void HandleTap()
    {
        if (Input.touchCount != 1) return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase != TouchPhase.Began) return;

        // ✅ Prevent double taps
        if (Time.time - lastTapTime < tapCooldown) return;
        lastTapTime = Time.time;

        // ✅ STRONG UI BLOCK (IMPORTANT FIX)
        if (IsTouchOverUI(touch)) return;

        if (PrefabSelector.SelectedPrefab == null)
        {
            Debug.LogWarning("❌ No prefab selected");
            return;
        }

        Debug.Log("🔥 Spawning: " + PrefabSelector.SelectedPrefab.name);

        bool hitPlane = raycastManager.Raycast(
            touch.position, _hits, TrackableType.PlaneWithinPolygon);

        if (hitPlane)
            PlaceOnSurface(_hits[0].pose, _hits[0].distance);
        else
            PlaceInAir();
    }

    // ───────────────────────── UI BLOCK FIX ─────────────────────────
    bool IsTouchOverUI(Touch touch)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = touch.position;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0;
    }

    // ───────────────────────── PLACEMENT ─────────────────────────
    void PlaceOnSurface(Pose pose, float dist)
    {
        var obj = SpawnObject(pose.position, pose.rotation);

        if (obj == null) return;

        var rb = SetupRigidbody(obj);
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void PlaceInAir()
    {
        var cam = Camera.main;

        var pos = cam.transform.position + cam.transform.forward * airDistance;

        var obj = SpawnObject(pos, Quaternion.identity);

        if (obj == null) return;

        var rb = SetupRigidbody(obj);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    // ───────────────────────── SPAWN ─────────────────────────
    GameObject SpawnObject(Vector3 pos, Quaternion rot)
    {
        if (PrefabSelector.SelectedPrefab == null) return null;

        var obj = Instantiate(PrefabSelector.SelectedPrefab, pos, rot);

        obj.transform.localScale = Vector3.one;

        float scale = ComputeScale(obj);
        obj.transform.localScale = Vector3.one * scale;

        if (!obj.GetComponent<ARObjectManipulator>())
            obj.AddComponent<ARObjectManipulator>();

        return obj;
    }

    float ComputeScale(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return fallbackScale;

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers)
            b.Encapsulate(r.bounds);

        float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);

        if (largest < 0.0001f) return fallbackScale;

        return Mathf.Clamp(targetRealWorldSize / largest, 0.005f, 3f);
    }

    // ───────────────────────── PHYSICS ─────────────────────────
    Rigidbody SetupRigidbody(GameObject obj)
    {
        var rb = obj.GetComponent<Rigidbody>() ?? obj.AddComponent<Rigidbody>();

        rb.mass = mass;
        rb.drag = drag;
        rb.angularDrag = angularDrag;

        bool hasCollider =
            obj.GetComponent<Collider>() != null ||
            obj.GetComponentInChildren<Collider>() != null;

        if (!hasCollider)
        {
            Debug.LogWarning($"[ARPlacer] No collider on {obj.name}, adding BoxCollider");
            obj.AddComponent<BoxCollider>();
        }

        return rb;
    }


}