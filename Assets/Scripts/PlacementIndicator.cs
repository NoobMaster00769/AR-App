using UnityEngine;

public class PlacementIndicator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PlacementReticle script on the parent")]
    public PlacementReticle reticle;

    [Header("Pulse Animation")]
    public float pulseSpeed  = 2f;
    public float pulseAmount = 0.08f;  
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