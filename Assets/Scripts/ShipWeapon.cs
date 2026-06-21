using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fires bullets from the ship along <see cref="ShipController.aimDirection"/> while
/// <see cref="ShipController.firing"/> is true. Bullets are parented to the planet so they
/// ride its rotation, and travel along the surface. Builds the elongated-diamond mesh and
/// an unlit yellow material at runtime (shared across all bullets).
/// </summary>
public class ShipWeapon : MonoBehaviour
{
    [Header("References")]
    public ShipController ship;
    public Transform planet;

    [Header("Fire")]
    [Tooltip("Shots per second while firing.")]
    public float fireRate = 9f;
    [Tooltip("Bullet travel speed in units/second along the surface.")]
    public float bulletSpeed = 24f;
    [Tooltip("Height above the planet centre the bullets ride at (match the ship hover).")]
    public float bulletRadius = 4.25f;
    [Tooltip("How far ahead of the ship a bullet spawns.")]
    public float muzzleOffset = 0.3f;
    [Tooltip("Safety lifetime cap in seconds.")]
    public float bulletLife = 4f;
    [Tooltip("Arc swept before a bullet despawns (degrees). <360 so shots never circle back.")]
    public float bulletMaxArc = 170f;

    [Header("Shape (elongated diamond)")]
    public float diamondHalfLength = 0.32f;
    public float diamondHalfWidth = 0.06f;

    [Header("Look / damage")]
    [ColorUsage(true, true)]
    [Tooltip("Bullet body colour. HDR (>1) so the global bloom makes it glow.")]
    public Color bulletColor = new Color(2.6f, 2.1f, 0.5f);
    [Tooltip("Damage each bullet deals on hit.")]
    public int damage = 1;

    static Mesh _mesh;
    static Material _mat;
    static Material _trailMat;
    float _cooldown;

    // Rebuild cached mesh/material fresh each Play start (needed when Domain Reload is disabled).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _mesh = null; _mat = null; _trailMat = null; }

    void Start()
    {
        if (ship == null) ship = GetComponent<ShipController>();
        if (planet == null) { var p = GameObject.Find("Planet"); if (p != null) planet = p.transform; }
        if (_mesh == null) _mesh = BuildDiamondMesh(diamondHalfWidth, diamondHalfLength);
        if (_mat == null) _mat = BuildBulletMaterial(bulletColor);
    }

    void Update()
    {
        _cooldown -= Time.deltaTime;
        if (ship != null && ship.firing && _cooldown <= 0f)
        {
            Fire();
            _cooldown = 1f / Mathf.Max(1f, fireRate);
        }
    }

    void Fire()
    {
        if (planet == null || ship == null) return;

        Vector3 aimWorld = ship.aimDirection.sqrMagnitude > 1e-4f ? ship.aimDirection : transform.forward;
        Vector3 muzzleWorld = transform.position + aimWorld * muzzleOffset;

        Vector3 localPos = planet.InverseTransformPoint(muzzleWorld);
        Vector3 localDir = planet.InverseTransformDirection(aimWorld);

        Camera cam = (ship != null && ship.cam != null) ? ship.cam : Camera.main;
        var go = SpawnDiamond(planet, _mesh, _mat);
        var b = go.AddComponent<Bullet>();
        b.life = bulletLife;
        b.maxArcDegrees = bulletMaxArc;
        b.damage = damage;
        b.Init(localPos, localDir, bulletRadius, bulletSpeed, cam);
    }

    /// <summary>Creates a diamond GameObject (mesh + material) parented under the planet, with a glowing trail.</summary>
    public static GameObject SpawnDiamond(Transform parent, Mesh mesh, Material mat)
    {
        var go = new GameObject("Bullet");
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Glowing tracer trail on a child (so it doesn't clash with the mesh renderer).
        var trailGo = new GameObject("Trail");
        trailGo.transform.SetParent(go.transform, false);
        var tr = trailGo.AddComponent<TrailRenderer>();
        tr.sharedMaterial = BulletTrailMaterial();
        tr.time = 0.12f;                       // short, fast streak
        tr.minVertexDistance = 0.02f;
        tr.numCapVertices = 2;
        tr.alignment = LineAlignment.View;
        tr.textureMode = LineTextureMode.Stretch;
        tr.shadowCastingMode = ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.lightProbeUsage = LightProbeUsage.Off;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)); // full at bullet -> point at tail
        tr.widthMultiplier = 0.12f;            // ~bullet width
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = grad;

        return go;
    }

    /// <summary>An elongated octahedron (diamond) pointing along +Z.</summary>
    public static Mesh BuildDiamondMesh(float w, float len)
    {
        var m = new Mesh();
        m.vertices = new Vector3[]
        {
            new Vector3(0, 0,  len), // 0 front tip
            new Vector3(0, 0, -len), // 1 back tip
            new Vector3( w, 0, 0),   // 2 +x
            new Vector3(0,  w, 0),   // 3 +y
            new Vector3(-w, 0, 0),   // 4 -x
            new Vector3(0, -w, 0),   // 5 -y
        };
        m.triangles = new int[]
        {
            0,2,3, 0,3,4, 0,4,5, 0,5,2, // front
            1,3,2, 1,4,3, 1,5,4, 1,2,5, // back
        };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Unlit, double-sided, HDR colour so the global bloom makes the bullet glow.</summary>
    public static Material BuildBulletMaterial(Color color)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var mat = new Material(sh);
        Color y = color; // HDR (>1) -> blooms
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", y);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", y);
        mat.color = y;
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // double-sided
        return mat;
    }

    /// <summary>Shared additive, HDR-gold material for bullet tracer trails.</summary>
    public static Material BulletTrailMaterial()
    {
        if (_trailMat != null) return _trailMat;
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        _trailMat = new Material(sh) { name = "BulletTrail" };
        if (_trailMat.HasProperty("_Surface")) _trailMat.SetFloat("_Surface", 1f);   // Transparent
        if (_trailMat.HasProperty("_Blend")) _trailMat.SetFloat("_Blend", 1f);       // Additive
        if (_trailMat.HasProperty("_SrcBlend")) _trailMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (_trailMat.HasProperty("_DstBlend")) _trailMat.SetFloat("_DstBlend", (float)BlendMode.One);
        if (_trailMat.HasProperty("_ZWrite")) _trailMat.SetFloat("_ZWrite", 0f);
        _trailMat.renderQueue = (int)RenderQueue.Transparent;
        Color c = new Color(2.4f, 1.9f, 0.5f, 1f); // HDR gold
        _trailMat.SetColor("_BaseColor", c);
        _trailMat.SetColor("_Color", c);
        return _trailMat;
    }
}
