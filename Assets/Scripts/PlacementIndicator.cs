using UnityEngine;

/// <summary>
/// Optional animated visual behavior for the reticle ring.
/// Attach this to the reticle's visual child GameObject.
///
/// SETUP:
///   1. This goes on the child visual object (the ring mesh / quad)
///      NOT on the PlacementReticle root.
///   2. It animates scale and opacity to give feedback:
///      - Pulses gently when on a valid surface
///      - Fades out when no surface
///   3. Works with any material that has a _Color or _BaseColor property.
///
/// MATERIAL:
///   - Use the "AR Feathered Plane" shader from AR Foundation Samples, OR
///   - Any Unlit/Transparent material with a ring texture
///   - Set the material's color alpha in the Inspector (start at ~200)
/// </summary>
public class PlacementIndicator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PlacementReticle script on the parent")]
    public PlacementReticle reticle;

    [Header("Pulse Animation")]
    public float pulseSpeed  = 2f;
    public float pulseAmount = 0.08f;   // how much it breathes in scale

    [Header("Appearance")]
    public float validAlpha   = 0.85f;
    public float invalidAlpha = 0f;
    public float fadeSpeed    = 8f;

    Renderer _renderer;
    float _baseScale;
    float _currentAlpha;

    static readonly int ColorProp     = Shader.PropertyToID("_Color");
    static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        _renderer  = GetComponent<Renderer>();
        _baseScale = transform.localScale.x;

        if (reticle == null)
            reticle = GetComponentInParent<PlacementReticle>();
    }

    void Update()
    {
        bool valid = reticle != null && reticle.IsOnValidSurface;

        // ── Fade alpha ────────────────────────────────────────────────────────
        float targetAlpha = valid ? validAlpha : invalidAlpha;
        _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

        if (_renderer != null)
        {
            Material mat = _renderer.material;

            Color c;
            if (mat.HasProperty(BaseColorProp))
            {
                c = mat.GetColor(BaseColorProp);
                c.a = _currentAlpha;
                mat.SetColor(BaseColorProp, c);
            }
            else if (mat.HasProperty(ColorProp))
            {
                c = mat.GetColor(ColorProp);
                c.a = _currentAlpha;
                mat.SetColor(ColorProp, c);
            }
        }

        // ── Pulse scale when valid ─────────────────────────────────────────────
        if (valid)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = Vector3.one * (_baseScale * pulse);
        }
        else
        {
            transform.localScale = Vector3.one * _baseScale;
        }
    }
}