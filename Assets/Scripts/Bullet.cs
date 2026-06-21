using UnityEngine;

/// <summary>
/// A projectile travelling along a great circle on the planet's surface (child of the planet,
/// works in local space, so it rides the planet's rotation). Its long axis (+Z) points along
/// travel.
///
/// Lifecycle (Geometry-Wars style):
///   * Despawns after sweeping <see cref="maxArcDegrees"/> of arc — far enough to reach the
///     back of the globe and hit enemies there, but well under 360° so it never circles back.
///   * Its renderer is switched off while on the FAR hemisphere (behind the sphere) so it's
///     invisible behind the planet, yet still alive for collisions.
/// </summary>
public class Bullet : MonoBehaviour
{
    public float speed = 24f;          // units/second along the surface
    public float maxArcDegrees = 170f; // total arc before despawn (anti-circumnavigation)
    public float life = 4f;            // safety cap in seconds
    public int damage = 1;             // damage dealt per hit

    float _radius;
    Vector3 _localPos; // on the sphere, in parent (planet) local space
    Vector3 _localDir; // unit tangent travel direction, local space
    float _age;
    float _traveled;   // accumulated arc, degrees

    Camera _cam;
    MeshRenderer _mr;
    Vector3 _center;   // planet centre (world)

    public void Init(Vector3 localPos, Vector3 localDir, float radius, float speed, Camera cam)
    {
        _radius = radius;
        this.speed = speed;
        _cam = cam != null ? cam : Camera.main;
        _mr = GetComponent<MeshRenderer>();
        _center = transform.parent != null ? transform.parent.position : Vector3.zero;

        _localPos = localPos.normalized * radius;
        Vector3 up = _localPos.normalized;
        _localDir = Vector3.ProjectOnPlane(localDir, up).normalized;
        if (_localDir.sqrMagnitude < 1e-6f)
            _localDir = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;

        Apply();
        UpdateVisibility();
    }

    void Update()
    {
        _age += Time.deltaTime;

        Vector3 up = _localPos.normalized;
        Vector3 axis = Vector3.Cross(up, _localDir); // rotating about this advances along _localDir
        float ang = (speed * Time.deltaTime / _radius) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.AngleAxis(ang, axis);

        _localPos = rot * _localPos;
        _localDir = (rot * _localDir).normalized;
        up = _localPos.normalized;
        _localDir = Vector3.ProjectOnPlane(_localDir, up).normalized;
        _traveled += ang;

        Apply();
        UpdateVisibility();

        // Hit test against active enemies (works even while hidden behind the planet).
        Vector3 wp = transform.position;
        var enemies = Enemies.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            var en = enemies[i];
            if (en == null) continue;
            float r = en.HitRadius;
            if ((en.Position - wp).sqrMagnitude <= r * r)
            {
                en.TakeDamage(damage);
                ExplosionFX.Hit(wp, -transform.forward, Color.white); // directional impact sparks (ricochet back)
                Destroy(gameObject);
                return;
            }
        }

        if (_traveled >= maxArcDegrees || _age >= life)
            Destroy(gameObject);
    }

    void Apply()
    {
        transform.localPosition = _localPos;
        transform.localRotation = Quaternion.LookRotation(_localDir, _localPos.normalized);
    }

    /// <summary>Visible only on the near (camera-facing) hemisphere; still alive when hidden.</summary>
    void UpdateVisibility()
    {
        if (_mr == null || _cam == null) return;
        Vector3 wUp = (transform.position - _center).normalized;
        Vector3 camDir = (_cam.transform.position - _center).normalized;
        bool front = Vector3.Dot(wUp, camDir) > 0f;
        if (_mr.enabled != front) _mr.enabled = front;
    }
}
