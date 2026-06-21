using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Geometry-Wars style death burst: a one-shot spray of bright, stretched, additive sparks
/// that fly out radially, decelerate, shrink and fade. Built entirely in code and auto-destroys
/// when finished. Call <see cref="Spawn(GameObject)"/> from an enemy's death.
/// </summary>
public static class ExplosionFX
{
    // Shared additive materials cached per glow (HDR) level, so explosions and hit-sparks
    // can bloom at different intensities without affecting each other.
    static readonly Dictionary<float, Material> s_mats = new Dictionary<float, Material>();

    // Rebuild materials each Play start (Domain Reload is disabled in this project).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_mats.Clear();

    static Material SparkMaterial(float glow)
    {
        if (s_mats.TryGetValue(glow, out var cached) && cached != null) return cached;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh) { name = "ExplosionSpark x" + glow };
        // Additive + HDR white so the per-particle colour blooms; higher glow = more explosive.
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 1f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.renderQueue = (int)RenderQueue.Transparent;
        var c = new Color(glow, glow, glow, 1f);
        m.SetColor("_BaseColor", c);
        m.SetColor("_Color", c);
        s_mats[glow] = m;
        return m;
    }

    /// <summary>
    /// Spawn an explosion at a source object's position, tinted to its material colour and
    /// scaled to its size — a big enemy (red cube ~1.0) bursts roughly 2x bigger and longer
    /// than a small chaser (~0.5), automatically.
    /// </summary>
    public static void Spawn(GameObject source)
    {
        if (source == null) return;
        Color tint = new Color(1f, 0.72f, 0.15f); // warm-yellow fallback
        float extent = 0.5f;
        var r = source.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
                tint = r.sharedMaterial.GetColor("_BaseColor");
            Vector3 s = r.bounds.size;
            extent = Mathf.Max(s.x, s.y, s.z);
        }

        // Baseline = small chaser (~0.5). Big cube (~1.0) => scale ~2: more sparks, faster, longer-lived.
        float scale = Mathf.Clamp(extent / 0.5f, 0.8f, 3f);
        int count = Mathf.RoundToInt(44f * scale);     // lots more sparks
        float speed = 6f * Mathf.Pow(scale, 0.5f);
        float size = 0.06f * Mathf.Pow(scale, 0.25f);  // smaller, less size-scaling
        float life = 0.85f * Mathf.Pow(scale, 0.6f);   // slightly longer-lived
        Spawn(source.transform.position, tint, count, speed, size, life, 4f); // explosive bloom
    }

    /// <summary>Spawn an explosion at a world position with a given tint.</summary>
    /// <summary>Small, bright, directional spark pop for a bullet impact (sprays along <paramref name="direction"/>).</summary>
    public static void Hit(Vector3 position, Vector3 direction, Color tint)
    {
        // dense, fast, streaky spray -> feels like peppering the target
        Spawn(position, tint, 20, 8f, 0.05f, 0.28f, 3f, direction);
    }

    public static void Spawn(Vector3 position, Color tint, int count = 26, float speed = 6f, float size = 0.13f, float life = 0.7f, float glow = 1.6f, Vector3 direction = default)
    {
        var go = new GameObject("Explosion");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        // mix the enemy hue with a whiter spark for variety
        main.startColor = new ParticleSystem.MinMaxGradient(tint, Color.Lerp(tint, Color.white, 0.6f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 16;
        main.stopAction = ParticleSystemStopAction.Destroy; // clean up the GameObject when finished

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.enabled = true;
        if (direction.sqrMagnitude > 1e-6f)
        {
            go.transform.rotation = Quaternion.LookRotation(direction.normalized);
            shape.shapeType = ParticleSystemShapeType.Cone; // directional spray (ricochet off a hit)
            shape.angle = 62f;       // wide spray
            shape.radius = 0.02f;

            // turbulence so the spray scatters lively instead of a clean fan
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 2.5f;
            noise.scrollSpeed = 1.2f;
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Sphere; // radial spray in all directions
            shape.radius = 0.08f;
        }

        // Decelerate (drag) so sparks shoot out then slow.
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.12f;
        limit.limit = new ParticleSystem.MinMaxCurve(0.6f);

        // Shrink to nothing.
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        // Bright, then fade out late in life.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        // Velocity-stretched sparks (faster = longer streak).
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.material = SparkMaterial(glow);
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.08f;
        pr.lengthScale = 2.2f;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;
        pr.sortingFudge = 0f;

        ps.Play();
    }
}
