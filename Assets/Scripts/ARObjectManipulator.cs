// ARObjectManipulator.cs — now purely a per-object component, no touch handling
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARObjectManipulator : MonoBehaviour
{
    Rigidbody _rb;
    Camera    _cam;

    bool    _isHeld   = false;
    float   _holdDist = 2f;
    Vector3 _prevPos;
    Vector3 _velocity;

    float _prevPinchDist      = -1f;
    float _prevTwoFingerAngle = float.NaN;

    // ── Surface placement support ─────────────────────────────────────────────
    // Returns world-space Y of the top of this object's bounding box.
    // ARTouchRouter uses this to know where to spawn something ON TOP of us.
    public float GetTopY()
    {
        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return transform.position.y;
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b.max.y;
    }

    public Vector3 GetTopSurfacePoint(Vector3 worldHitPoint)
    {
        return new Vector3(worldHitPoint.x, GetTopY(), worldHitPoint.z);
    }

    // ── Highlight state ───────────────────────────────────────────────────────
    bool _isHighlighted = false;
    readonly List<(Renderer r, Material mat, Color orig)> _origColors = new();

    public void SetHighlight(bool on)
    {
        if (_isHighlighted == on) return;
        _isHighlighted = on;

        if (on)
        {
            _origColors.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                // Use sharedMaterial instances so we don't permanently dirty them
                var mats = r.materials; // this returns copies (instanced)
                foreach (var mat in mats)
                {
                    _origColors.Add((r, mat, mat.color));
                    mat.color = Color.Lerp(mat.color, new Color(0.4f, 0.9f, 1f), 0.45f);
                }
                r.materials = mats;
            }
        }
        else
        {
            int idx = 0;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                foreach (var mat in mats)
                {
                    if (idx < _origColors.Count)
                        mat.color = _origColors[idx++].orig;
                }
                r.materials = mats;
            }
            _origColors.Clear();
        }
    }

    void Awake()
    {
        _cam = Camera.main;
        _rb  = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity  = false;
    }

    // ── Called by ARTouchRouter ───────────────────────────────────────────────
    public void BeginDrag(float hitDistance)
    {
        _isHeld   = true;
        _holdDist = hitDistance;
        StopAllCoroutines();
        SetHighlight(false);
        _rb.isKinematic = true;
        _rb.useGravity  = false;
        _rb.velocity        = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _prevPos  = transform.position;
        _velocity = Vector3.zero;
    }

    public void ContinueDrag(Vector2 screenPos, ARRaycastManager rrm)
    {
        if (!_isHeld) return;

        _velocity = (transform.position - _prevPos) / Time.deltaTime;
        _prevPos  = transform.position;

        // Prefer snapping to AR plane under finger
        var arHits = new List<ARRaycastHit>();
        if (rrm != null && rrm.Raycast(screenPos, arHits, TrackableType.PlaneWithinPolygon))
        {
            var normal = arHits[0].pose.rotation * Vector3.up;
            if (Vector3.Dot(normal, Vector3.up) > 0.7f)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    arHits[0].pose.position,
                    Time.deltaTime * 30f);
                return;
            }
        }

        // No plane — move at held distance from camera
        Ray     ray    = Camera.main.ScreenPointToRay(screenPos);
        Vector3 target = ray.GetPoint(_holdDist);
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 30f);
    }

    public void EndDrag()
    {
        if (!_isHeld) return;
        _isHeld = false;

        float speed = _velocity.magnitude;
        _rb.isKinematic = false;
        _rb.useGravity  = true;

        if (speed > 1.5f)
        {
            _rb.velocity        = _velocity * 0.5f;
            _rb.angularVelocity = Random.insideUnitSphere * Mathf.Clamp(speed * 0.3f, 0f, 5f);
        }
        // No else-branch freezing — just let it fall/settle naturally

        StartCoroutine(SettleOnLand());
    }

    public void StartSettling() => StartCoroutine(SettleOnLand());

    IEnumerator SettleOnLand()
    {
        yield return new WaitForSeconds(0.15f);

        while (true)
        {
            yield return new WaitForSeconds(0.05f);
            if (_isHeld) continue;

            if (_rb.velocity.magnitude < 0.05f && _rb.angularVelocity.magnitude < 0.05f)
            {
                _rb.velocity        = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                // DO NOT set isKinematic = true — let Unity sleep it
                yield break;
            }
        }
    }

    // ── Two finger gestures ───────────────────────────────────────────────────
    public void HandleTwoFingerGesture()
    {
        if (Input.touchCount < 2) return;
        HandlePinchScale();
        HandleTwoFingerRotate();
    }

    void HandlePinchScale()
    {
        Touch t0 = Input.GetTouch(0), t1 = Input.GetTouch(1);
        float dist = Vector2.Distance(t0.position, t1.position);
        if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
        { _prevPinchDist = dist; return; }
        if (_prevPinchDist < 0f) { _prevPinchDist = dist; return; }
        float delta = dist - _prevPinchDist;
        _prevPinchDist = dist;
        Vector3 next = transform.localScale * (1f + delta * 0.005f);
        if (next.magnitude > 0.01f && next.magnitude < 30f)
            transform.localScale = next;
    }

    void HandleTwoFingerRotate()
    {
        Touch t0 = Input.GetTouch(0), t1 = Input.GetTouch(1);
        Vector2 dir   = t1.position - t0.position;
        float   angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
        { _prevTwoFingerAngle = angle; return; }
        if (float.IsNaN(_prevTwoFingerAngle)) { _prevTwoFingerAngle = angle; return; }
        float delta = angle - _prevTwoFingerAngle;
        _prevTwoFingerAngle = angle;
        transform.Rotate(Vector3.up, -delta, Space.World);
    }

    // ── Impulse (flick / force push from ARTouchRouter) ───────────────────────
    public void ApplyImpulse(Vector3 worldForce)
    {
        StopAllCoroutines();
        _rb.isKinematic = false;
        _rb.useGravity  = true;
        _rb.velocity    = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.AddForce(worldForce, ForceMode.Impulse);
        _rb.angularVelocity = Random.insideUnitSphere * Mathf.Clamp(worldForce.magnitude * 0.4f, 0f, 6f);
        StartCoroutine(SettleOnLand());
    }

    // ── Visual feedback ───────────────────────────────────────────────────────
    public void OnSelected()
    {
        StopAllCoroutines();
        StartCoroutine(PulseScale());
    }

    public void OnDeselected()
    {
        SetHighlight(false);
    }

    IEnumerator PulseScale()
    {
        Vector3 orig = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 8f;
            transform.localScale = Vector3.Lerp(orig, orig * 1.08f, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        transform.localScale = orig;
    }
}