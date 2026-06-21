using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GW-style "materialize" spawn glow: a soft, blurry round blob in the enemy's colour fades up,
/// swells and brightens to a hot bloom, then fades as the enemy forms out of it. A camera-facing
/// additive billboard with a generated soft radial texture; per-instance brightness via MPB.
/// </summary>
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class SpawnGlow : MonoBehaviour
{
    public float duration = 0.8f;       // charge time; the enemy is spawned by the spawner at the end of this
    public float startSize = 0.45f;
    public float endSize = 1.2f;
    [ColorUsage(true, true)] public Color color = new Color(2.4f, 1.8f, 0.5f, 1f);

    static Texture2D s_tex;
    static UnityEngine.Mesh s_quad;
    static Material s_mat;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { s_tex = null; s_quad = null; s_mat = null; }

    MeshRenderer _mr;
    MaterialPropertyBlock _mpb;
    int _id;
    Camera _cam;
    float _t;
    float _fade;

    public static void Spawn(Transform parent, Vector3 localPos, Color color, float duration)
    {
        var go = new GameObject("SpawnGlow");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var g = go.AddComponent<SpawnGlow>();
        g.color = color;
        g.duration = duration;
    }

    void Awake()
    {
        GetComponent<MeshFilter>().sharedMesh = Quad();
        _mr = GetComponent<MeshRenderer>();
        _mr.sharedMaterial = Mat();
        _mr.shadowCastingMode = ShadowCastingMode.Off;
        _mr.receiveShadows = false;
        _mpb = new MaterialPropertyBlock();
        _id = Shader.PropertyToID("_BaseColor");
        _cam = Camera.main;
        _fade = duration * 0.3f;
    }

    void LateUpdate()
    {
        _t += Time.deltaTime;
        if (_cam == null) _cam = Camera.main;
        if (_cam != null) transform.rotation = _cam.transform.rotation; // billboard

        float size, intensity;
        if (_t <= duration)
        {
            float k = _t / duration;
            size = Mathf.Lerp(startSize, endSize, k);
            intensity = Mathf.Lerp(0.35f, 2.6f, k * k); // charge up
        }
        else
        {
            float f = Mathf.Clamp01((_t - duration) / _fade);
            size = Mathf.Lerp(endSize, endSize * 0.5f, f);
            intensity = Mathf.Lerp(2.6f, 0f, f); // fade as the enemy takes over
        }

        transform.localScale = Vector3.one * size;
        _mr.GetPropertyBlock(_mpb);
        _mpb.SetColor(_id, color * intensity);
        _mr.SetPropertyBlock(_mpb);

        if (_t >= duration + _fade) Destroy(gameObject);
    }

    static Material Mat()
    {
        if (s_mat != null) return s_mat;
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        s_mat = new Material(sh) { name = "SpawnGlow" };
        if (s_mat.HasProperty("_BaseMap")) s_mat.SetTexture("_BaseMap", SoftTex());
        if (s_mat.HasProperty("_Surface")) s_mat.SetFloat("_Surface", 1f);
        if (s_mat.HasProperty("_Blend")) s_mat.SetFloat("_Blend", 1f);
        if (s_mat.HasProperty("_SrcBlend")) s_mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (s_mat.HasProperty("_DstBlend")) s_mat.SetFloat("_DstBlend", (float)BlendMode.One); // additive
        if (s_mat.HasProperty("_ZWrite")) s_mat.SetFloat("_ZWrite", 0f);
        s_mat.renderQueue = (int)RenderQueue.Transparent;
        s_mat.SetColor("_BaseColor", Color.white);
        return s_mat;
    }

    static Texture2D SoftTex()
    {
        if (s_tex != null) return s_tex;
        int n = 64;
        s_tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "SoftGlow", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * a; // soft falloff toward the edge
                s_tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        s_tex.Apply();
        return s_tex;
    }

    static UnityEngine.Mesh Quad()
    {
        if (s_quad != null) return s_quad;
        s_quad = new UnityEngine.Mesh { name = "GlowQuad" };
        s_quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
        s_quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        s_quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        s_quad.RecalculateBounds();
        return s_quad;
    }
}
