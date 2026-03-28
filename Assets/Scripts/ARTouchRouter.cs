// ARTouchRouter.cs  — add this to ANY gameobject in the scene (e.g. XR Origin)
// DELETE all touch handling from ARPrefabPlacer and ARObjectManipulator
// This script owns ALL touch input and routes it correctly

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class ARTouchRouter : MonoBehaviour
{
    [Header("References")]
    public ARRaycastManager  raycastManager;
    public SnapchatStyleMenu selectionMenu;

    [Header("Scale")]
    public float targetRealWorldSize = 0.15f;
    public float fallbackScale       = 0.12f;
    public float airDistance         = 2.0f;

    // ── Interaction mode ──────────────────────────────────────────────────────
    // PLACE  : tap empty space / object surface → spawn new object
    // MOVE   : tap existing object → drag it (original behaviour)
    // Always starts in PLACE mode. Toggle via the UI button or
    // the public SwitchMode() method.
    public enum InteractionMode { Place, Move }
    [Header("Mode (runtime readable)")]
    public InteractionMode CurrentMode = InteractionMode.Place;

    // ── Flick detection ───────────────────────────────────────────────────────
    [Header("Flick / throw")]
    [Tooltip("Minimum swipe speed (screen px/s) to count as a flick")]
    public float flickThreshold       = 900f;
    [Tooltip("World-force multiplier applied to flick velocity")]
    public float flickForceMultiplier = 0.018f;

    // ── Long press highlight ──────────────────────────────────────────────────
    [Header("Long press")]
    [Tooltip("Seconds of stillness to trigger surface-placement highlight")]
    public float longPressDuration = 0.55f;

    // ── Air-drop ──────────────────────────────────────────────────────────────
    [Header("Air drop")]
    [Tooltip("How far above the target point to spawn when dropping from air")]
    public float airDropHeight = 0.35f;

    // ── Force push / pull ─────────────────────────────────────────────────────
    [Header("Force push / pull (3-finger)")]
    [Tooltip("Three-finger pinch/spread applies a push or pull force to selected object")]
    public float forcePushMultiplier = 4.5f;

    // ─────────────────────────────────────────────────────────────────────────
    ARObjectManipulator _selected;
    static readonly List<ARRaycastHit> _arHits = new();

    // Long press state
    float   _touchDownTime;
    Vector2 _touchDownPos;
    bool    _longPressTriggered;
    bool    _dragStarted;

    // Flick state
    Vector2 _prevTouchPos;
    float   _prevTouchTime;
    Vector2 _flickVelocity; // screen px/s

    // Three-finger force push/pull state
    float _prev3FingerDist = -1f;

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (Input.touchCount == 0)
        {
            _longPressTriggered = false;
            _dragStarted        = false;
            _prev3FingerDist    = -1f;
            return;
        }

        // ── 3-finger force push/pull ──────────────────────────────────────
        if (Input.touchCount == 3)
        {
            HandleThreeFingerForcePush();
            return;
        }

        // ── 2-finger gestures → selected object (scale + rotate, unchanged) ─
        if (Input.touchCount == 2)
        {
            _selected?.HandleTwoFingerGesture();
            return;
        }

        if (Input.touchCount != 1) return;
        Touch t = Input.GetTouch(0);
        if (IsOverUI(t.position)) return;

        switch (t.phase)
        {
            case TouchPhase.Began:    OnFingerDown(t); break;
            case TouchPhase.Moved:
            case TouchPhase.Stationary: OnFingerMove(t); break;
            case TouchPhase.Ended:
            case TouchPhase.Canceled: OnFingerUp(t);   break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    void OnFingerDown(Touch t)
    {
        _touchDownTime      = Time.time;
        _touchDownPos       = t.position;
        _longPressTriggered = false;
        _dragStarted        = false;
        _prevTouchPos       = t.position;
        _prevTouchTime      = Time.time;
        _flickVelocity      = Vector2.zero;

        // Raycast against physics objects
        Ray ray = Camera.main.ScreenPointToRay(t.position);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            var manip = hit.transform.GetComponentInParent<ARObjectManipulator>();
            if (manip != null)
            {
                if (CurrentMode == InteractionMode.Move)
                {
                    // Move mode: grab immediately
                    Debug.Log("✅ Selected (Move): " + manip.name);
                    Select(manip);
                    manip.BeginDrag(hit.distance);
                    WakeNearbyObjects(manip);
                    _dragStarted = true;
                }
                else
                {
                    // Place mode: select for long-press highlight, DON'T drag
                    Debug.Log("✅ Touched object (Place mode): " + manip.name);
                    Select(manip);
                    // Long press will be evaluated in OnFingerMove/Up
                }
                return;
            }
        }

        // Nothing hit — in Move mode we do nothing extra on down
        // In Place mode we'll spawn on finger-up (handled in OnFingerUp)
    }

    void OnFingerMove(Touch t)
    {
        // Track flick velocity (rolling last-frame delta)
        float dt = Time.time - _prevTouchTime;
        if (dt > 0f)
            _flickVelocity = (t.position - _prevTouchPos) / dt;
        _prevTouchPos  = t.position;
        _prevTouchTime = Time.time;

        // Long press: if finger barely moved, check hold duration
        float moveDist = Vector2.Distance(t.position, _touchDownPos);
        if (!_longPressTriggered && !_dragStarted && moveDist < 18f)
        {
            float held = Time.time - _touchDownTime;
            if (held >= longPressDuration && _selected != null)
            {
                _longPressTriggered = true;
                // Highlight to signal "you can place on top of me"
                _selected.SetHighlight(true);
                Debug.Log("🔵 Long press — surface placement ready on " + _selected.name);
            }
        }

        // If moved beyond threshold, cancel long press and start drag in Move mode
        if (moveDist >= 18f && !_dragStarted && _selected != null)
        {
            _selected.SetHighlight(false);
            _longPressTriggered = false;

            if (CurrentMode == InteractionMode.Move)
            {
                Ray ray = Camera.main.ScreenPointToRay(_touchDownPos);
                float dist = Physics.Raycast(ray, out RaycastHit h, 100f) ? h.distance : airDistance;
                _selected.BeginDrag(dist);
                WakeNearbyObjects(_selected);
                _dragStarted = true;
            }
        }

        // Continue drag if already dragging
        if (_dragStarted && _selected != null)
            _selected.ContinueDrag(t.position, raycastManager);
    }

    void OnFingerUp(Touch t)
    {
        float moveDist = Vector2.Distance(t.position, _touchDownPos);

        // ── Flick detection (Move mode, dragging) ─────────────────────────
        if (_dragStarted && _selected != null)
        {
            float flickSpeed = _flickVelocity.magnitude;
            if (flickSpeed > flickThreshold)
            {
                // Convert screen-space flick into world-space force
                Vector3 camRight   = Camera.main.transform.right;
                Vector3 camUp      = Camera.main.transform.up;
                Vector3 flickWorld = (camRight   * _flickVelocity.x
                                   + camUp       * _flickVelocity.y)
                                   * flickForceMultiplier;
                // Add a little forward component so it flies away from camera
                flickWorld += Camera.main.transform.forward * flickSpeed * flickForceMultiplier * 0.3f;
                Debug.Log($"🏹 Flick! speed={flickSpeed:F0} force={flickWorld}");
                _selected.ApplyImpulse(flickWorld);
                _selected = null;
            }
            else
            {
                _selected.EndDrag();
            }
            _dragStarted = false;
            return;
        }

        // ── Long press ended: spawn on top of selected object ─────────────
        if (_longPressTriggered && _selected != null)
        {
            _selected.SetHighlight(false);
            _longPressTriggered = false;

            // Raycast to find exact hit point on the object
            Ray ray = Camera.main.ScreenPointToRay(t.position);
            Vector3 spawnPos;
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var manip = hit.transform.GetComponentInParent<ARObjectManipulator>();
                if (manip != null && manip == _selected)
                {
                    // Spawn above the top surface of the hit object
                    spawnPos = _selected.GetTopSurfacePoint(hit.point);
                    Debug.Log($"📦 Spawning on top of {_selected.name} at Y={spawnPos.y:F3}");
                    SpawnOnSurface(spawnPos);
                    return;
                }
            }
        }

        // ── Tap in Place mode: spawn on plane or object ───────────────────
        if (CurrentMode == InteractionMode.Place && moveDist < 18f)
        {
            Ray ray = Camera.main.ScreenPointToRay(t.position);

            // First check: did we tap an existing object?
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var manip = hit.transform.GetComponentInParent<ARObjectManipulator>();
                if (manip != null)
                {
                    // Spawn on top of it
                    Vector3 spawnPos = manip.GetTopSurfacePoint(hit.point);
                    Debug.Log($"📦 Place-tap on object {manip.name}, spawning at Y={spawnPos.y:F3}");
                    SpawnOnSurface(spawnPos);
                    return;
                }
            }

            // Second check: AR plane
            bool hitPlane = raycastManager.Raycast(
                t.position, _arHits, TrackableType.PlaneWithinPolygon);
            if (hitPlane)
            {
                var normal = _arHits[0].pose.rotation * Vector3.up;
                if (Vector3.Dot(normal, Vector3.up) > 0.7f)
                {
                    SpawnOnPlane(_arHits[0].pose.position);
                    return;
                }
            }

            // Fallback: air spawn
            SpawnInAir(t.position);
        }
    }

    // ── 3-finger force push / pull ────────────────────────────────────────────
    // Three fingers on screen: spreading pushes the selected object away,
    // pinching pulls it toward the camera.
    void HandleThreeFingerForcePush()
    {
        if (_selected == null) return;

        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);
        Touch t2 = Input.GetTouch(2);

        // Average distance between all pairs
        float d01 = Vector2.Distance(t0.position, t1.position);
        float d02 = Vector2.Distance(t0.position, t2.position);
        float d12 = Vector2.Distance(t1.position, t2.position);
        float avgDist = (d01 + d02 + d12) / 3f;

        if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began || t2.phase == TouchPhase.Began)
        {
            _prev3FingerDist = avgDist;
            return;
        }
        if (_prev3FingerDist < 0f) { _prev3FingerDist = avgDist; return; }

        float delta = avgDist - _prev3FingerDist;
        _prev3FingerDist = avgDist;

        if (Mathf.Abs(delta) < 0.5f) return; // dead zone

        Vector3 dir = (_selected.transform.position - Camera.main.transform.position).normalized;
        // Spread = push away (positive delta), pinch = pull toward (negative delta)
        Vector3 force = dir * delta * forcePushMultiplier;

        var rb = _selected.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity  = true;
            rb.AddForce(force, ForceMode.Impulse);
        }

        Debug.Log($"💨 Force push/pull delta={delta:F1} force={force}");
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────
    void SpawnOnPlane(Vector3 planePos)
    {
        var obj = CreateObject(planePos, Quaternion.identity);
        if (obj == null) return;
        StartCoroutine(EnableGravityNextFrame(obj, planePos));
    }

    // Spawn on the surface of an existing object — drop from slightly above
    void SpawnOnSurface(Vector3 surfacePos)
    {
        if (PrefabSelector.SelectedPrefab == null) return;
        // Spawn airDropHeight above the surface so it falls and lands naturally
        Vector3 spawnPos = surfacePos + Vector3.up * airDropHeight;
        var obj = CreateObject(spawnPos, Quaternion.identity);
        if (obj == null) return;
        // Enable physics immediately — let it fall onto the surface
        StartCoroutine(EnableGravityNextFrame(obj, null));
    }

    void SpawnInAir(Vector2 screenPos)
    {
        float dist = airDistance;
        if (raycastManager.Raycast(screenPos, _arHits, TrackableType.PlaneWithinPolygon))
            dist = _arHits[0].distance;

        // Spawn above where they tapped so it falls down into place
        Vector3 basePos = Camera.main.transform.position
                        + Camera.main.transform.forward * dist;
        Vector3 pos = basePos + Vector3.up * airDropHeight;

        var obj = CreateObject(pos, Quaternion.identity);
        if (obj == null) return;
        StartCoroutine(EnableGravityNextFrame(obj, null));
    }

    IEnumerator EnableGravityNextFrame(GameObject obj, Vector3? snapPos)
    {
        yield return null; // frame 1
        yield return null; // frame 2

        if (obj == null) yield break;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null) yield break;

        if (snapPos.HasValue)
        {
            obj.transform.position = snapPos.Value + Vector3.up * 0.001f;
        }

        rb.isKinematic = false;
        rb.useGravity  = true;

        var manip = obj.GetComponent<ARObjectManipulator>();
        manip?.StartSettling();
    }

    GameObject CreateObject(Vector3 pos, Quaternion rot)
    {
        if (PrefabSelector.SelectedPrefab == null) return null;

        var obj = Instantiate(PrefabSelector.SelectedPrefab, pos, rot);
        float scale = ComputeScale(obj);
        obj.transform.localScale = Vector3.one * scale;

        // ── Colliders ──────────────────────────────────────────────────────
        foreach (var mc in obj.GetComponentsInChildren<MeshCollider>())
            mc.convex = true;

        var rootBox = obj.AddComponent<BoxCollider>();
        var rends = obj.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            rootBox.center = obj.transform.InverseTransformPoint(b.center);
            rootBox.size   = b.size / scale;
        }

        // ── Rigidbody ─────────────────────────────────────────────────────
        var rb = obj.GetComponent<Rigidbody>() ?? obj.AddComponent<Rigidbody>();
        rb.mass                   = 1f;
        rb.drag                   = 5f;
        rb.angularDrag            = 5f;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.isKinematic            = true;
        rb.useGravity             = false;

        // ── Scripts ────────────────────────────────────────────────────────
        var oi = obj.GetComponent<ARInteractable>();      if (oi) Destroy(oi);
        var ot = obj.GetComponent<Throwable>();           if (ot) Destroy(ot);
        var om = obj.GetComponent<ARObjectManipulator>(); if (om) Destroy(om);

        var manip = obj.AddComponent<ARObjectManipulator>();
        Select(manip);

        Debug.Log("✅ Spawned: " + obj.name);
        return obj;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Select(ARObjectManipulator manip)
    {
        if (_selected != null && _selected != manip)
            _selected.OnDeselected();
        _selected = manip;
        manip.OnSelected();
    }

    void WakeNearbyObjects(ARObjectManipulator manip)
    {
        var cols = Physics.OverlapSphere(manip.transform.position, 0.5f);
        foreach (var col in cols)
        {
            var rb = col.attachedRigidbody;
            if (rb != null && rb.isKinematic == false)
                rb.WakeUp();
        }
    }

    // ── Public API for UI button ──────────────────────────────────────────────
    /// <summary>
    /// Call this from your UI toggle button to switch between Place and Move modes.
    /// </summary>
    public void SwitchMode()
    {
        CurrentMode = CurrentMode == InteractionMode.Place
            ? InteractionMode.Move
            : InteractionMode.Place;
        Debug.Log("🔄 Mode switched to: " + CurrentMode);
    }

    public void SetMode(InteractionMode mode)
    {
        CurrentMode = mode;
        Debug.Log("🔄 Mode set to: " + mode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    float ComputeScale(GameObject obj)
    {
        var rends = obj.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return fallbackScale;
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (largest < 0.0001f) return fallbackScale;
        return Mathf.Clamp(targetRealWorldSize / largest, 0.005f, 3f);
    }

    bool IsOverUI(Vector2 pos)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = pos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        return results.Count > 0;
    }
}