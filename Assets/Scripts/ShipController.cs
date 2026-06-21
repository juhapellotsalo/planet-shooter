using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Geometry-Wars "planet level" ship — a SURFACE CRAWLER.
///
/// The planet is now static at the world origin; the ship moves over the sphere
/// surface (great-circle movement, the same math the enemies use) and a
/// <see cref="CameraRig"/> keeps it centred on screen. This puts the ship, enemies and
/// bullets all in ONE world frame, so a stock TrailRenderer — and any other world-space
/// effect — works on the ship for free.
///
/// Movement (left stick / WASD) is screen-relative. A SEPARATE aim direction
/// (right stick / mouse) and a firing flag are exposed for <see cref="ShipWeapon"/>.
/// Input priority: a connected gamepad wins; otherwise keyboard + mouse.
/// </summary>
public class ShipController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Transform core;              // planet centre (world origin)

    [Header("Movement")]
    [Tooltip("Surface move speed in units/second.")]
    public float moveSpeed = 6f;
    [Tooltip("How fast the hull eases to face its movement direction (degrees/second).")]
    public float turnSpeed = 540f;

    // Exposed for the weapon system, recomputed each frame:
    [System.NonSerialized] public Vector3 aimDirection; // world-space, tangent to the surface
    [System.NonSerialized] public bool firing;

    float _radius;       // distance from centre (captured at Start, preserves the combat shell)
    Vector3 _dir;        // unit surface normal: position = Center + _dir * _radius
    Vector3 _forward;    // unit tangent heading

    Vector3 Center => core != null ? core.position : Vector3.zero;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (core == null) { var c = GameObject.Find("Core"); if (c != null) core = c.transform; }

        Vector3 fromCenter = transform.position - Center;
        _radius = fromCenter.magnitude;
        if (_radius < 1e-3f) _radius = 6.6f;
        _dir = fromCenter / _radius;

        _forward = Vector3.ProjectOnPlane(transform.forward, _dir).normalized;
        if (_forward.sqrMagnitude < 1e-6f) _forward = ScreenUp(_dir);
        aimDirection = _forward;
        Apply();
    }

    void Update()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        Vector3 up = _dir;
        Vector3 sRight = ScreenRight(up);
        Vector3 sUp = ScreenUp(up);

        // --- Crawl along the surface toward the movement input ---
        Vector2 mv = ReadMove();
        if (mv.sqrMagnitude > 1f) mv = mv.normalized; // clamp analog over-range
        Vector3 moveTangent = sRight * mv.x + sUp * mv.y;
        if (moveTangent.sqrMagnitude > 1e-4f)
        {
            Vector3 moveDir = moveTangent.normalized;
            float ang = (moveSpeed * mv.magnitude * Time.deltaTime / _radius) * Mathf.Rad2Deg;
            Vector3 axis = Vector3.Cross(up, moveDir);
            _dir = (Quaternion.AngleAxis(ang, axis) * _dir).normalized;

            // ease the hull toward the heading
            _forward = Vector3.RotateTowards(_forward, moveDir,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
        }

        // keep the heading tangent to the (now possibly moved) surface normal
        _forward = Vector3.ProjectOnPlane(_forward, _dir).normalized;
        if (_forward.sqrMagnitude < 1e-6f) _forward = ScreenUp(_dir);

        // --- Aim + fire (for the weapon system) ---
        Vector2 aim = ReadAim();
        aimDirection = aim.sqrMagnitude > 1e-4f
            ? (ScreenRight(_dir) * aim.x + ScreenUp(_dir) * aim.y).normalized
            : _forward;
        firing = ReadFire();

        Apply();
    }

    void Apply()
    {
        transform.position = Center + _dir * _radius;
        transform.rotation = Quaternion.LookRotation(_forward, _dir);
    }

    Vector3 ScreenRight(Vector3 up)
    {
        Vector3 r = Vector3.ProjectOnPlane(cam.transform.right, up).normalized;
        return r.sqrMagnitude < 1e-6f ? Vector3.ProjectOnPlane(Vector3.right, up).normalized : r;
    }

    Vector3 ScreenUp(Vector3 up)
    {
        Vector3 u = Vector3.ProjectOnPlane(cam.transform.up, up).normalized;
        return u.sqrMagnitude < 1e-6f ? Vector3.ProjectOnPlane(Vector3.forward, up).normalized : u;
    }

    /// <summary>Movement input as a screen vector (x = right, y = up). Gamepad left stick, else WASD.</summary>
    Vector2 ReadMove()
    {
        Vector2 m = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 ls = gp.leftStick.ReadValue();
            if (ls.sqrMagnitude > 0.04f) return ls;
        }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed) m.x -= 1f;
            if (kb.dKey.isPressed) m.x += 1f;
            if (kb.wKey.isPressed) m.y += 1f;
            if (kb.sKey.isPressed) m.y -= 1f;
        }
#else
        m.x = Input.GetAxisRaw("Horizontal");
        m.y = Input.GetAxisRaw("Vertical");
#endif
        return m;
    }

    /// <summary>Aim input as a screen vector (x = right, y = up). Gamepad right stick, else mouse-from-ship.</summary>
    Vector2 ReadAim()
    {
        Vector2 a = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 rs = gp.rightStick.ReadValue();
            return rs.sqrMagnitude > 0.04f ? rs : Vector2.zero;
        }
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(transform.position);
            Vector2 mp = mouse.position.ReadValue();
            a = new Vector2(mp.x - sp.x, mp.y - sp.y);
        }
#else
        Vector3 sp2 = cam.WorldToScreenPoint(transform.position);
        a = new Vector2(Input.mousePosition.x - sp2.x, Input.mousePosition.y - sp2.y);
#endif
        return a;
    }

    /// <summary>True while firing. LMB / right trigger / right-stick deflection (GW auto-fire).</summary>
    bool ReadFire()
    {
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.rightTrigger.ReadValue() > 0.5f || gp.rightShoulder.isPressed) return true;
            if (gp.rightStick.ReadValue().sqrMagnitude > 0.25f) return true;
        }
        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }
}
