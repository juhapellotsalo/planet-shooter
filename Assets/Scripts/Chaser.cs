using UnityEngine;

/// <summary>
/// A small, fragile pyramid enemy that homes toward the player ship along the planet surface
/// (great-circle pursuit). Lives as a child of the planet (local space) so it rides the
/// planet's rotation; each frame it steers toward the ship's current position. Dies in one hit.
/// </summary>
public class Chaser : MonoBehaviour, IEnemy
{
    [Tooltip("Pursuit speed along the surface, units/second (keep below the player's move speed so it's dodgeable).")]
    public float speed = 3f;
    [Tooltip("Radius from the planet centre (the shared combat shell).")]
    public float radius = 6.6f;
    public int maxHealth = 1;
    public float hitRadius = 0.4f;
    [Tooltip("The ship to chase. Auto-found by name if empty.")]
    public Transform target;

    public float HitRadius => hitRadius;
    public Vector3 Position => transform.position;

    int _health;
    Vector3 _localPos;

    void OnEnable() { Enemies.Register(this); _health = maxHealth; }
    void OnDisable() { Enemies.Unregister(this); }

    void Start()
    {
        if (target == null) { var s = GameObject.Find("Ship"); if (s != null) target = s.transform; }
        _localPos = transform.localPosition.sqrMagnitude > 1e-4f
            ? transform.localPosition.normalized * radius
            : Vector3.up * radius;
        Apply(Vector3.ProjectOnPlane(Vector3.forward, _localPos.normalized).normalized);
    }

    public void Init(Vector3 localPos, float radius)
    {
        this.radius = radius;
        _localPos = localPos.normalized * radius;
        Apply(Vector3.ProjectOnPlane(Vector3.forward, _localPos.normalized).normalized);
    }

    public bool TakeDamage(int dmg)
    {
        _health -= dmg;
        if (_health <= 0) { ExplosionFX.Spawn(gameObject); Destroy(gameObject); return true; }
        return false;
    }

    void Update()
    {
        var planet = transform.parent;
        if (planet == null || target == null) return;

        Vector3 up = _localPos.normalized;
        // Ship position expressed in the planet's local frame, on the shell.
        Vector3 targetDir = planet.InverseTransformPoint(target.position).normalized;
        Vector3 heading = Vector3.ProjectOnPlane(targetDir, up).normalized;
        if (heading.sqrMagnitude < 1e-6f) { Apply(heading); return; }

        // Step toward the target along the great circle, without overshooting.
        float ang = (speed * Time.deltaTime / radius) * Mathf.Rad2Deg;
        ang = Mathf.Min(ang, Vector3.Angle(up, targetDir));
        Vector3 axis = Vector3.Cross(up, heading);
        _localPos = (Quaternion.AngleAxis(ang, axis) * _localPos).normalized * radius;

        Apply(heading);
    }

    void Apply(Vector3 heading)
    {
        transform.localPosition = _localPos;
        Vector3 up = _localPos.normalized;
        Vector3 fwd = Vector3.ProjectOnPlane(heading, up).normalized;
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
        transform.localRotation = Quaternion.LookRotation(fwd, up); // apex (+Y) points radially outward
    }
}
