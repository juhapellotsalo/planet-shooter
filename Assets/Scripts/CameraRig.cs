using UnityEngine;

/// <summary>
/// Follows the surface-crawling ship: sits on the ship's radial (outside the combat shell)
/// and looks at the planet centre, so the ship stays screen-centred and you always see the
/// hemisphere around it. The "up" vector is parallel-transported frame to frame, which keeps
/// the view from rolling or flipping as the ship roams the sphere.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)] // run after ShipController has moved the ship this frame
public class CameraRig : MonoBehaviour
{
    [Tooltip("The ship to follow.")]
    public Transform target;
    [Tooltip("Planet centre. Defaults to the world origin if unset.")]
    public Transform center;
    [Tooltip("Camera distance from the planet centre (keep > combat-shell radius).")]
    public float distance = 12f;
    [Tooltip("Rotation easing; higher = snappier. 0 = instant.")]
    public float rotationLerp = 24f;

    Vector3 _up = Vector3.up;
    bool _init;

    Vector3 C => center != null ? center.position : Vector3.zero;

    void OnEnable() { _init = false; }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 dir = target.position - C;
        if (dir.sqrMagnitude < 1e-6f) return;
        dir.Normalize();

        Vector3 desiredPos = C + dir * distance;

        // Parallel-transport the up vector onto the new tangent plane (no roll / pole flips).
        Vector3 up = Vector3.ProjectOnPlane(_up, dir);
        if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.up, dir);
        if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.forward, dir);
        up.Normalize();
        _up = up;

        Quaternion desiredRot = Quaternion.LookRotation(-dir, up); // look toward the centre

        transform.position = desiredPos;
        if (!_init || rotationLerp <= 0f)
        {
            transform.rotation = desiredRot;
            _init = true;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot,
                1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
        }
    }
}
