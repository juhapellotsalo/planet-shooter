using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A glowing "warp-in" reticle that telegraphs where an enemy is about to spawn: a ring that
/// contracts onto the spawn point while blinking faster and brighter, then removes itself.
/// Lies tangent to the planet surface (its local +Z points radially outward). HDR additive,
/// so the global bloom makes it glow. Per-instance brightness is driven via a MaterialPropertyBlock.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class SpawnWarning : MonoBehaviour
{
    public float duration = 0.8f;
    public float startRadius = 0.85f;
    public float endRadius = 0.12f;
    [ColorUsage(true, true)] public Color color = new Color(2.4f, 1.8f, 0.5f, 1f);
    public int segments = 40;

    static Material s_mat;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_mat = null;

    LineRenderer _lr;
    MaterialPropertyBlock _mpb;
    int _id;
    float _t;

    /// <summary>Spawn a warning at a planet-local position; +Z is oriented radially outward.</summary>
    public static void Spawn(Transform parent, Vector3 localPos, Color color, float duration)
    {
        var go = new GameObject("SpawnWarning");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.LookRotation(localPos.normalized);
        var w = go.AddComponent<SpawnWarning>();
        w.color = color;
        w.duration = duration;
    }

    static Material Mat()
    {
        if (s_mat != null) return s_mat;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        s_mat = new Material(sh) { name = "SpawnWarning" };
        if (s_mat.HasProperty("_Surface")) s_mat.SetFloat("_Surface", 1f);
        if (s_mat.HasProperty("_Blend")) s_mat.SetFloat("_Blend", 1f);
        if (s_mat.HasProperty("_SrcBlend")) s_mat.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (s_mat.HasProperty("_DstBlend")) s_mat.SetFloat("_DstBlend", (float)BlendMode.One); // additive
        if (s_mat.HasProperty("_ZWrite")) s_mat.SetFloat("_ZWrite", 0f);
        s_mat.renderQueue = (int)RenderQueue.Transparent;
        s_mat.SetColor("_BaseColor", Color.white);
        return s_mat;
    }

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.useWorldSpace = false;
        _lr.loop = true;
        _lr.positionCount = segments;
        _lr.widthMultiplier = 0.05f;
        _lr.numCornerVertices = 2;
        _lr.alignment = LineAlignment.View;
        _lr.textureMode = LineTextureMode.Stretch;
        _lr.shadowCastingMode = ShadowCastingMode.Off;
        _lr.receiveShadows = false;
        _lr.lightProbeUsage = LightProbeUsage.Off;
        _lr.sharedMaterial = Mat();
        _mpb = new MaterialPropertyBlock();
        _id = Shader.PropertyToID("_BaseColor");
    }

    void Update()
    {
        _t += Time.deltaTime;
        float k = Mathf.Clamp01(_t / duration); // 0 -> 1

        // ease-in contraction: drifts inward, then snaps onto the point
        float r = Mathf.Lerp(startRadius, endRadius, k * k);
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            _lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
        }

        // SMOOTH brightness: quick fade-in, steady ramp toward spawn, calm low-frequency pulse.
        // Time-based constant frequency => no frame-rate aliasing (the old sin(k*k*55) flicker).
        float fadeIn = Mathf.Clamp01(k / 0.12f);
        float ramp = Mathf.Lerp(0.8f, 1.7f, k);
        float pulse = 1f + 0.18f * Mathf.Sin(_t * 16f); // ~2.5 Hz, gentle
        _lr.GetPropertyBlock(_mpb);
        _mpb.SetColor(_id, color * (fadeIn * ramp * pulse));
        _lr.SetPropertyBlock(_mpb);

        if (_t >= duration) Destroy(gameObject);
    }
}
