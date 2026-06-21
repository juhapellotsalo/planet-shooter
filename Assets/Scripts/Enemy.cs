using UnityEngine;

/// <summary>
/// A surface-crawling enemy with a wandering patrol (move a fixed distance, then turn 90°
/// left or right at random), tumbling like an asteroid as it goes. Lives as a child of the
/// planet (local space) so it rides the planet's rotation.
///
/// Takes multiple hits before dying. Registers itself in <see cref="Active"/> so bullets can
/// find it with a cheap distance test.
/// </summary>
public class Enemy : MonoBehaviour, IEnemy
{
    [Header("Movement")]
    public float speed = 3f;
    public float segmentLength = 4f;
    public float turnAngle = 90f;
    public float radius = 6.6f;
    public float spinSpeed = 120f;

    [Header("Combat")]
    [Tooltip("Hits required to destroy this enemy.")]
    public int maxHealth = 12;
    [Tooltip("Collision radius for bullet hits (world units).")]
    public float hitRadius = 0.55f;

    public float HitRadius => hitRadius;
    public Vector3 Position => transform.position;
    public int Health => _health;

    Vector3 _localPos, _localDir;
    float _distInSeg;
    Vector3 _spinAxis;
    Quaternion _spin = Quaternion.identity;

    int _health;

    void OnEnable()
    {
        Enemies.Register(this);
        _health = maxHealth;
    }
    void OnDisable() { Enemies.Unregister(this); }

    void Start()
    {
        _localPos = transform.localPosition.sqrMagnitude > 1e-4f
            ? transform.localPosition.normalized * radius
            : Vector3.up * radius;
        Vector3 up = _localPos.normalized;
        _localDir = Vector3.ProjectOnPlane(transform.localRotation * Vector3.forward, up).normalized;
        if (_localDir.sqrMagnitude < 1e-6f)
            _localDir = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
        EnsureSpin();
        Apply();
    }

    public void Init(Vector3 localPos, Vector3 localDir, float radius)
    {
        this.radius = radius;
        _localPos = localPos.normalized * radius;
        Vector3 up = _localPos.normalized;
        _localDir = Vector3.ProjectOnPlane(localDir, up).normalized;
        if (_localDir.sqrMagnitude < 1e-6f)
            _localDir = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
        _distInSeg = 0f;
        EnsureSpin();
        Apply();
    }

    void EnsureSpin()
    {
        if (_spinAxis == Vector3.zero)
        {
            _spinAxis = Random.onUnitSphere;
            _spin = Random.rotationUniform;
        }
    }

    /// <summary>Apply damage; returns true if this hit destroyed the enemy.</summary>
    public bool TakeDamage(int dmg)
    {
        _health -= dmg;
        if (_health <= 0)
        {
            Die();
            return true;
        }
        GetComponent<HitFlash>()?.Flash(); // non-lethal hit: white body flash
        return false;
    }

    void Die()
    {
        ExplosionFX.Spawn(gameObject);
        Destroy(gameObject);
    }

    void Update()
    {
        float dist = speed * Time.deltaTime;

        // Crawl along the great circle.
        Vector3 up = _localPos.normalized;
        Vector3 axis = Vector3.Cross(up, _localDir);
        Quaternion rot = Quaternion.AngleAxis((dist / radius) * Mathf.Rad2Deg, axis);
        _localPos = rot * _localPos;
        _localDir = (rot * _localDir).normalized;
        up = _localPos.normalized;
        _localDir = Vector3.ProjectOnPlane(_localDir, up).normalized;

        // Random 90 turn at each segment end.
        _distInSeg += dist;
        if (_distInSeg >= segmentLength)
        {
            _distInSeg -= segmentLength;
            float sign = Random.value < 0.5f ? 1f : -1f;
            _localDir = (Quaternion.AngleAxis(sign * turnAngle, up) * _localDir).normalized;
        }

        // Tumble.
        _spin = Quaternion.AngleAxis(spinSpeed * Time.deltaTime, _spinAxis) * _spin;

        Apply();
    }

    void Apply()
    {
        transform.localPosition = _localPos;
        transform.localRotation = _spin;
    }
}
