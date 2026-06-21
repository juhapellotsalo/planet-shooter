using UnityEngine;

/// <summary>
/// Central scene-resident sound board (lives on the "Audio" object): persistent 2D AudioSources
/// that play fire-and-forget one-shots on behalf of objects that can't reliably play their own —
/// code-spawned bullets, and enemies that <c>Destroy</c> themselves the same frame they're hit.
///
/// Every game SFX is configured here in one place (assign the clips in the inspector); callers
/// just trigger by name. A separate AudioSource per category keeps the per-shot random pitch of
/// one sound from warbling another that's still playing.
/// </summary>
public class Sfx : MonoBehaviour
{
    [Header("Enemy hit")]
    [Tooltip("Played when a bullet hits any enemy (cube or chaser).")]
    public AudioClip enemyHit;
    [Range(0f, 1f)]
    public float enemyHitVolume = 0.6f;
    [Tooltip("Random pitch variation (+/-) so repeated hits don't sound identical.")]
    [Range(0f, 0.5f)]
    public float enemyHitPitchJitter = 0.08f;

    [Header("Ship laser")]
    [Tooltip("Played on each shot from the ship's weapon.")]
    public AudioClip laser;
    [Range(0f, 1f)]
    public float laserVolume = 0.5f;
    [Tooltip("Random pitch variation (+/-) so rapid fire doesn't sound like a machine gun.")]
    [Range(0f, 0.5f)]
    public float laserPitchJitter = 0.06f;

    static Sfx _instance;
    AudioSource _hitSrc, _laserSrc;

    // Clear the cached singleton each Play start (Domain Reload is disabled in this project).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instance = null;

    void Awake()
    {
        _instance = this;
        _hitSrc = NewSource();
        _laserSrc = NewSource();
    }

    AudioSource NewSource()
    {
        var a = gameObject.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 0f; // 2D — consistent volume wherever the sound happens on the sphere
        return a;
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    /// <summary>Played when a bullet hits any enemy. No-op if unwired/absent.</summary>
    public static void EnemyHit()
    {
        if (_instance == null) return;
        _instance.PlayOn(_instance._hitSrc, _instance.enemyHit, _instance.enemyHitVolume, _instance.enemyHitPitchJitter);
    }

    /// <summary>Played on each ship shot. No-op if unwired/absent.</summary>
    public static void Laser()
    {
        if (_instance == null) return;
        _instance.PlayOn(_instance._laserSrc, _instance.laser, _instance.laserVolume, _instance.laserPitchJitter);
    }

    void PlayOn(AudioSource src, AudioClip clip, float vol, float jitter)
    {
        if (src == null || clip == null) return;
        src.pitch = 1f + Random.Range(-jitter, jitter);
        src.PlayOneShot(clip, vol);
    }
}
