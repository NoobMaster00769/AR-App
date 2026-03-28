
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

    
    public enum InteractionMode { Place, Move }
    [Header("Mode (runtime readable)")]
    public InteractionMode CurrentMode = InteractionMode.Place;

    [Header("Flick / throw")]
    [Tooltip("Minimum swipe speed (screen px/s) to count as a flick")]
    public float flickThreshold = 900f;

    [Tooltip("Scales flick screen-velocity to world force. 0.005–0.008 feels natural.")]
    public float flickForceMultiplier = 0.006f;

    [Tooltip("Hard cap on flick impulse in world-units/s. Prevents objects teleporting.")]
    public float flickMaxForce = 3.5f;

    [Header("Long press")]
    public float longPressDuration = 0.55f;

    [Header("Air drop")]
    public float airDropHeight = 0.35f;

    [Header("Force push / pull (3-finger)")]
    public float forcePushMultiplier = 4.5f;

    ARObjectManipulator _selected;
    static readonly List<ARRaycastHit> _arHits = new();

    float   _touchDownTime;
    Vector2 _touchDownPos;
    bool    _longPressTriggered;
    bool    _dragStarted;

    const int   FLICK_SAMPLES   = 5;
    Vector2[]   _flickPosBuf    = new Vector2[FLICK_SAMPLES];
    float[]     _flickTimeBuf   = new float[FLICK_SAMPLES];
    int         _flickHead      = 0;
    bool        _flickBufFull   = false;

    float _prev3FingerDist = -1f;

    void Update()
    {
        if (Input.touchCount == 0)
        {
            _longPressTriggered = false;
            _dragStarted        = false;
            _prev3FingerDist    = -1f;
            return;
        }

        if (Input.touchCount == 3)  { HandleThreeFingerForcePush(); return; }
        if (Input.touchCount == 2)  { _selected?.HandleTwoFingerGesture(); return; }
        if (Input.touchCount != 1)  return;

        Touch t = Input.GetTouch(0);
        if (IsOverUI(t.position)) return;

        switch (t.phase)
        {
            case TouchPhase.Began:
                OnFingerDown(t); break;
            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                OnFingerMove(t); break;
            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                OnFingerUp(t); break;
        }
    }

    void OnFingerDown(Touch t)
    {
        _touchDownTime      = Time.time;
        _touchDownPos       = t.position;
        _longPressTriggered = false;
        _dragStarted        = false;

        _flickHead    = 0;
        _flickBufFull = false;
        RecordFlick(t.position);

        Ray ray = Camera.main.ScreenPointToRay(t.position);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            var manip = hit.transform.GetComponentInParent<ARObjectManipulator>();
            if (manip != null)
            {
                Select(manip);

                if (CurrentMode == InteractionMode.Move)
                {
                    manip.BeginDrag(hit.distance);
                    WakeNearbyObjects(manip);
                    _dragStarted = true;
                }
                return;
            }
        }
    }

    void OnFingerMove(Touch t)
    {
        RecordFlick(t.position);

        float moveDist = Vector2.Distance(t.position, _touchDownPos);

        
        if (!_longPressTriggered && !_dragStarted && moveDist < 18f)
        {
            if (Time.time - _touchDownTime >= longPressDuration && _selected != null)
            {
                _longPressTriggered = true;
                _selected.SetHighlight(true);
            }
        }

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

        if (_dragStarted && _selected != null)
            _selected.ContinueDrag(t.position, raycastManager);
    }

    void OnFingerUp(Touch t)
    {
        RecordFlick(t.position);
        float moveDist = Vector2.Distance(t.position, _touchDownPos);

        if (_dragStarted && _selected != null)
        {
            Vector2 vel   = SmoothedFlickVelocity();
            float   speed = vel.magnitude;

            if (speed > flickThreshold)
            {
                Vector3 right   = Camera.main.transform.right;
                Vector3 up      = Camera.main.transform.up;
                Vector3 forward = Camera.main.transform.forward;

                Vector3 force = (right * vel.x + up * vel.y) * flickForceMultiplier
                              + forward * (speed * flickForceMultiplier * 0.25f);

                if (force.magnitude > flickMaxForce)
                    force = force.normalized * flickMaxForce;

                Debug.Log($"🏹 Flick spd={speed:F0} force={force.magnitude:F2}");
                _selected.ApplyImpulse(force);
                _selected = null;
            }
            else
            {
                _selected.EndDrag();
            }

            _dragStarted = false;
            return;
        }

        if (_longPressTriggered && _selected != null)
        {
            _selected.SetHighlight(false);
            _longPressTriggered = false;

            Ray ray = Camera.main.ScreenPointToRay(t.position);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var manip = hit.transform.GetComponentInParent<ARObjectManipulator>();
                if (manip != null && manip == _selected)
                {
                    SpawnOnSurface(_selected.GetTopSurfacePoint(hit.point));
                    return;
                }
            }
        }

        if (CurrentMode == InteractionMode.Place && moveDist < 18f)
        {
            Ray ray = Camera.main.ScreenPointToRay(t.position);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var manip = hit.transform.GetComponentInParent<ARObjectManipulator>();
                if (manip != null)
                {
                    SpawnOnSurface(manip.GetTopSurfacePoint(hit.point));
                    return;
                }
            }

            if (raycastManager.Raycast(t.position, _arHits, TrackableType.PlaneWithinPolygon))
            {
                var normal = _arHits[0].pose.rotation * Vector3.up;
                if (Vector3.Dot(normal, Vector3.up) > 0.7f)
                {
                    SpawnOnPlane(_arHits[0].pose.position);
                    return;
                }
            }

            SpawnInAir(t.position);
        }
    }

    void RecordFlick(Vector2 pos)
    {
        _flickPosBuf [_flickHead] = pos;
        _flickTimeBuf[_flickHead] = Time.time;
        _flickHead = (_flickHead + 1) % FLICK_SAMPLES;
        if (_flickHead == 0) _flickBufFull = true;
    }

    Vector2 SmoothedFlickVelocity()
    {
        int count  = _flickBufFull ? FLICK_SAMPLES : _flickHead;
        if (count < 2) return Vector2.zero;
        int oldest = _flickBufFull ? _flickHead : 0;
        int newest = (_flickHead - 1 + FLICK_SAMPLES) % FLICK_SAMPLES;
        float dt   = _flickTimeBuf[newest] - _flickTimeBuf[oldest];
        if (dt < 0.0001f) return Vector2.zero;
        return (_flickPosBuf[newest] - _flickPosBuf[oldest]) / dt;
    }

    void HandleThreeFingerForcePush()
    {
        if (_selected == null) return;

        Touch t0 = Input.GetTouch(0), t1 = Input.GetTouch(1), t2 = Input.GetTouch(2);
        float avg = (Vector2.Distance(t0.position, t1.position)
                   + Vector2.Distance(t0.position, t2.position)
                   + Vector2.Distance(t1.position, t2.position)) / 3f;

        if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began || t2.phase == TouchPhase.Began)
        { _prev3FingerDist = avg; return; }
        if (_prev3FingerDist < 0f) { _prev3FingerDist = avg; return; }

        float delta = avg - _prev3FingerDist;
        _prev3FingerDist = avg;
        if (Mathf.Abs(delta) < 0.5f) return;

        Vector3 dir   = (_selected.transform.position - Camera.main.transform.position).normalized;
        Vector3 force = dir * delta * forcePushMultiplier;

        var rb = _selected.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = false; rb.useGravity = true; rb.AddForce(force, ForceMode.Impulse); }
    }

    void SpawnOnPlane(Vector3 planePos)
    {
        var obj = CreateObject(planePos, Quaternion.identity);
        if (obj) StartCoroutine(EnableGravity(obj, planePos));
    }

    void SpawnOnSurface(Vector3 surfacePos)
    {
        if (PrefabSelector.SelectedPrefab == null) return;
        var obj = CreateObject(surfacePos + Vector3.up * airDropHeight, Quaternion.identity);
        if (obj) StartCoroutine(EnableGravity(obj, null));
    }

    void SpawnInAir(Vector2 screenPos)
    {
        float dist = airDistance;
        if (raycastManager.Raycast(screenPos, _arHits, TrackableType.PlaneWithinPolygon))
            dist = _arHits[0].distance;

        Vector3 pos = Camera.main.transform.position
                    + Camera.main.transform.forward * dist
                    + Vector3.up * airDropHeight;

        var obj = CreateObject(pos, Quaternion.identity);
        if (obj) StartCoroutine(EnableGravity(obj, null));
    }

    IEnumerator EnableGravity(GameObject obj, Vector3? snapPos)
    {
        yield return null;
        yield return null;
        if (obj == null) yield break;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null) yield break;

        if (snapPos.HasValue)
            obj.transform.position = snapPos.Value + Vector3.up * 0.001f;

        rb.isKinematic = false;
        rb.useGravity  = true;
        obj.GetComponent<ARObjectManipulator>()?.StartSettling();
    }

    GameObject CreateObject(Vector3 pos, Quaternion rot)
    {
        if (PrefabSelector.SelectedPrefab == null) return null;

        var obj = Instantiate(PrefabSelector.SelectedPrefab, pos, rot);
        float scale = ComputeScale(obj);
        obj.transform.localScale = Vector3.one * scale;

        var rends = obj.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float bottomOffset = obj.transform.position.y - b.min.y;
            obj.transform.position += Vector3.up * bottomOffset;
        }
        ConvexHullColliderBuilder.Build(obj);

        var rb = obj.GetComponent<Rigidbody>() ?? obj.AddComponent<Rigidbody>();
        rb.mass                   = 1f;
        rb.drag                   = 5f;
        rb.angularDrag            = 5f;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.isKinematic            = true;
        rb.useGravity             = false;

        var oi = obj.GetComponent<ARInteractable>();      if (oi) Destroy(oi);
        var ot = obj.GetComponent<Throwable>();           if (ot) Destroy(ot);
        var om = obj.GetComponent<ARObjectManipulator>(); if (om) Destroy(om);

        var manip = obj.AddComponent<ARObjectManipulator>();
        Select(manip);

        Debug.Log("✅ Spawned: " + obj.name);
        return obj;
    }

    void Select(ARObjectManipulator manip)
    {
        if (_selected != null && _selected != manip) _selected.OnDeselected();
        _selected = manip;
        manip.OnSelected();
    }

    void WakeNearbyObjects(ARObjectManipulator manip)
    {
        foreach (var col in Physics.OverlapSphere(manip.transform.position, 0.5f))
        {
            var rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic) rb.WakeUp();
        }
    }

    public void SwitchMode()
    {
        CurrentMode = CurrentMode == InteractionMode.Place
            ? InteractionMode.Move : InteractionMode.Place;
        Debug.Log("🔄 Mode: " + CurrentMode);
    }

    public void SetMode(InteractionMode mode) => CurrentMode = mode;

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