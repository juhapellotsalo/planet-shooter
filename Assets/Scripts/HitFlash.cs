using UnityEngine;

/// <summary>
/// Brief white "I got hit" flash. Uses a MaterialPropertyBlock so ONLY this renderer flashes,
/// not others sharing the same material. Drives a colour property (_EmissionColor for Lit,
/// _BaseColor for Unlit) from a bright flash colour back to its base over a short duration.
/// Call <see cref="Flash"/> from the enemy's TakeDamage on a non-lethal hit.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class HitFlash : MonoBehaviour
{
    [Tooltip("Colour property to flash: _EmissionColor (Lit) or _BaseColor (Unlit).")]
    public string colorProperty = "_EmissionColor";
    [ColorUsage(true, true)]
    [Tooltip("Peak flash colour (HDR -> blooms white).")]
    public Color flashColor = new Color(4f, 4f, 4f, 1f);
    [Tooltip("Flash fade time in seconds.")]
    public float duration = 0.08f;

    Renderer _r;
    MaterialPropertyBlock _mpb;
    int _id;
    Color _base;
    float _t;
    bool _flashing;

    void Awake()
    {
        _r = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _id = Shader.PropertyToID(colorProperty);
        var m = _r.sharedMaterial;
        _base = (m != null && m.HasProperty(_id)) ? m.GetColor(_id) : Color.black;
    }

    public void Flash() { _t = duration; _flashing = true; }

    void LateUpdate()
    {
        if (!_flashing) return;
        _t -= Time.deltaTime;
        float k = Mathf.Clamp01(_t / duration); // 1 at hit -> 0 at end
        _r.GetPropertyBlock(_mpb);
        _mpb.SetColor(_id, Color.Lerp(_base, flashColor, k));
        _r.SetPropertyBlock(_mpb);
        if (_t <= 0f)
        {
            _r.GetPropertyBlock(_mpb);
            _mpb.SetColor(_id, _base);
            _r.SetPropertyBlock(_mpb);
            _flashing = false;
        }
    }
}
