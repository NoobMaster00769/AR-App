using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// FIXED ARPrefabPlacer — attach to XR Origin (Mobile AR)
///
/// Fixes:
///   1. ContinuousDynamic collision — objects no longer fall through AR planes
///   2. Bounds-aware scaling — object appears the correct real-world size regardless of prefab scale
///   3. Two-finger pinch while one finger holds → push/pull air placement distance (0.5m – 8m)
///
/// INSPECTOR SETUP:
///   raycastManager  → ARRaycastManager on XR Origin
///   placementReticle → PlacementReticle GameObject
///   selectionMenu   → SnapchatStyleMenu component
///   targetRealWorldSize → desired height in metres (e.g. 0.15 for a 15 cm bottle)
/// </summary>
public class ARPrefabPlacer : MonoBehaviour
{
    [Header("References")]
    public ARRaycastManager  raycastManager;
    public PlacementReticle  placementReticle;
    public SnapchatStyleMenu selectionMenu;   // optional — for distance label

    [Header("Air Placement Distance")]
    public float airDistance      = 2.0f;
    public float minAirDistance   = 0.5f;
    public float maxAirDistance   = 8.0f;
    public float pinchSensitivity = 0.018f;

    [Header("Scale")]
    [Tooltip("Desired real-world size of the largest axis in metres")]
    public float targetRealWorldSize = 0.15f;
    public float fallbackScale       = 0.12f;

    [Header("Physics")]
    public float mass        = 0.5f;
    public float drag        = 0.4f;
    public float angularDrag = 0.4f;

    // ─────────────────────────────────────────────────────────────────────────
    static readonly List<ARRaycastHit> _hits = new();
    float _prevPinchDist = -1f;

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        HandlePinch();
        HandleTap();
    }

    // ── Two-finger pinch: adjust air distance ─────────────────────────────────
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

        if (_prevPinchDist < 0f) { _prevPinchDist = dist; return; }

        float delta = dist - _prevPinchDist;
        _prevPinchDist = dist;

        airDistance = Mathf.Clamp(
            airDistance + delta * pinchSensitivity,
            minAirDistance, maxAirDistance);

        selectionMenu?.ShowDistanceLabel(airDistance);
    }

    // ── Single tap: place object ───────────────────────────────────────────────
    void HandleTap()
    {
        if (Input.touchCount != 1) return;
        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began) return;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return;

        if (PrefabSelector.SelectedPrefab == null) return;

        bool hitPlane = raycastManager.Raycast(
            touch.position, _hits, TrackableType.PlaneWithinPolygon);

        if (hitPlane)
            PlaceOnSurface(_hits[0].pose, _hits[0].distance);
        else
            PlaceInAir();
    }

    // ─────────────────────────────────────────────────────────────────────────
    void PlaceOnSurface(Pose pose, float dist)
    {
        var obj = SpawnObject(pose.position, pose.rotation, dist);
        var rb  = SetupRigidbody(obj);
        rb.isKinematic = true;
        rb.useGravity  = false;
    }

    void PlaceInAir()
    {
        var cam = Camera.main;
        var pos = cam.transform.position + cam.transform.forward * airDistance;
        var obj = SpawnObject(pos, Quaternion.identity, airDistance);
        var rb  = SetupRigidbody(obj);

        rb.isKinematic            = false;
        rb.useGravity             = true;
        // ★ FIX 1 — prevents tunnelling through thin AR plane colliders
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    // ─────────────────────────────────────────────────────────────────────────
    GameObject SpawnObject(Vector3 pos, Quaternion rot, float distFromCam)
    {
        var obj = Instantiate(PrefabSelector.SelectedPrefab, pos, rot);

        // ★ FIX 2 — compute scale from actual mesh bounds, not a hardcoded value
        obj.transform.localScale = Vector3.one;             // reset first
        float scale = ComputeNormalisedScale(obj);
        obj.transform.localScale = Vector3.one * scale;

        if (!obj.GetComponent<ARObjectManipulator>())
            obj.AddComponent<ARObjectManipulator>();

        return obj;
    }

    // Measures the prefab's actual renderer bounds after resetting scale to 1,
    // then returns the scale factor that makes its largest axis = targetRealWorldSize.
    float ComputeNormalisedScale(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return fallbackScale;

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (largest < 0.0001f) return fallbackScale;

        return Mathf.Clamp(targetRealWorldSize / largest, 0.005f, 3f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    Rigidbody SetupRigidbody(GameObject obj)
    {
        var rb = obj.GetComponent<Rigidbody>() ?? obj.AddComponent<Rigidbody>();

        rb.mass        = mass;
        rb.drag        = drag;
        rb.angularDrag = angularDrag;

        bool hasCollider = obj.GetComponent<Collider>() != null ||
                           obj.GetComponentInChildren<Collider>() != null;
        if (!hasCollider)
        {
            Debug.LogWarning($"[ARPlacer] No collider found on {obj.name} — adding BoxCollider");
            obj.AddComponent<BoxCollider>();
        }

        return rb;
    }
}