using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpawnEffectType { ContractingRing, MaterializeGlow }

/// <summary>
/// Central enemy spawner — one place to tune ALL spawn params.
///
///  • Red cubes: keeps <see cref="maxCubes"/> alive; replaces a dead one after
///    <see cref="cubeRespawnDelay"/> seconds, spawned on the FAR side of the planet.
///  • Chasers: spawn in waves at random spots all around the planet. When a wave is wiped out,
///    the next (larger) wave spawns after <see cref="waveDelay"/> seconds.
///
/// Both clone an inactive template object, so spawned enemies keep all their materials/effects.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject cubeTemplate;
    public GameObject chaserTemplate;
    public Transform planet;
    public Transform ship;
    [Tooltip("Distance from the planet centre enemies live on (the combat shell).")]
    public float radius = 6.6f;

    [Header("Red cubes")]
    public int maxCubes = 2;
    public float cubeRespawnDelay = 1f;
    [Tooltip("Spawn cubes where dot(dir, shipDir) < this. Negative = far side; 1 = anywhere.")]
    public float cubeFarSideThreshold = -0.25f;

    [Header("Chaser waves")]
    public int firstWaveCount = 8;
    [Tooltip("Each new wave adds this many chasers.")]
    public int waveCountIncrement = 2;
    public int maxWaveCount = 24;
    [Tooltip("Seconds after a wave is cleared before the next spawns.")]
    public float waveDelay = 3f;
    [Tooltip("Spawn chasers where dot(dir, shipDir) < this (keeps them off the ship). 1 = truly anywhere.")]
    public float chaserSpawnThreshold = 0.7f;
    [Tooltip("Which spawn telegraph to use for chasers.")]
    public SpawnEffectType spawnEffect = SpawnEffectType.MaterializeGlow;
    [Tooltip("Seconds the spawn telegraph shows before each chaser materialises.")]
    public float chaserWarnTime = 0.8f;
    [ColorUsage(true, true)]
    [Tooltip("Colour of the spawn-warning reticle (HDR -> blooms).")]
    public Color chaserWarnColor = new Color(2.4f, 1.8f, 0.5f, 1f);

    readonly List<GameObject> _cubes = new List<GameObject>();
    readonly List<GameObject> _chasers = new List<GameObject>();
    float _cubeTimer;
    float _waveTimer;
    int _waveCount;
    bool _waveActive;
    int _pending; // chasers warned but not yet materialised

    Vector3 C => planet != null ? planet.position : Vector3.zero;

    void Start()
    {
        if (planet == null) { var p = GameObject.Find("Planet"); if (p != null) planet = p.transform; }
        if (ship == null) { var s = GameObject.Find("Ship"); if (s != null) ship = s.transform; }

        for (int i = 0; i < maxCubes; i++) SpawnCube();
        _cubeTimer = cubeRespawnDelay;

        _waveCount = Mathf.Max(1, firstWaveCount);
        SpawnWave();
        _waveActive = true;
    }

    void Update()
    {
        // --- Cubes: keep maxCubes alive, respawn on the far side ---
        Prune(_cubes);
        if (_cubes.Count < maxCubes)
        {
            _cubeTimer -= Time.deltaTime;
            if (_cubeTimer <= 0f) { SpawnCube(); _cubeTimer = cubeRespawnDelay; }
        }
        else _cubeTimer = cubeRespawnDelay;

        // --- Chasers: wave cleared -> larger wave after a delay ---
        Prune(_chasers);
        if (_waveActive && _chasers.Count == 0 && _pending == 0) { _waveActive = false; _waveTimer = waveDelay; }
        if (!_waveActive)
        {
            _waveTimer -= Time.deltaTime;
            if (_waveTimer <= 0f)
            {
                _waveCount = Mathf.Min(_waveCount + waveCountIncrement, maxWaveCount);
                SpawnWave();
                _waveActive = true;
            }
        }
    }

    void SpawnWave()
    {
        for (int i = 0; i < _waveCount; i++)
            StartCoroutine(WarnThenSpawnChaser(RandomDir(chaserSpawnThreshold)));
    }

    void SpawnCube()
    {
        if (cubeTemplate == null || planet == null) return;
        _cubes.Add(SpawnFrom(cubeTemplate, "RedCube", RandomDir(cubeFarSideThreshold)));
    }

    // Telegraph the spot, then materialise the chaser there.
    IEnumerator WarnThenSpawnChaser(Vector3 dir)
    {
        _pending++;
        Vector3 localPos = dir * radius;
        if (planet != null)
        {
            if (spawnEffect == SpawnEffectType.MaterializeGlow)
                SpawnGlow.Spawn(planet, localPos, chaserWarnColor, chaserWarnTime);
            else
                SpawnWarning.Spawn(planet, localPos, chaserWarnColor, chaserWarnTime);
        }
        yield return new WaitForSeconds(chaserWarnTime);
        _pending--;
        if (chaserTemplate != null && planet != null)
        {
            var go = SpawnFrom(chaserTemplate, "Chaser", dir);
            _chasers.Add(go);
            if (spawnEffect == SpawnEffectType.MaterializeGlow)
                StartCoroutine(ScalePop(go.transform, 0.16f)); // pop out of the glow
        }
    }

    // Quick grow-in so the enemy appears to form out of the glow.
    IEnumerator ScalePop(Transform t, float dur)
    {
        if (t == null) yield break;
        Vector3 full = t.localScale;
        float e = 0f;
        while (t != null && e < dur)
        {
            e += Time.deltaTime;
            t.localScale = full * Mathf.SmoothStep(0.15f, 1f, e / dur);
            yield return null;
        }
        if (t != null) t.localScale = full;
    }

    GameObject SpawnFrom(GameObject template, string newName, Vector3 dir)
    {
        var go = Instantiate(template, planet);
        go.name = newName;
        go.transform.localPosition = dir * radius;
        go.transform.localRotation = Quaternion.LookRotation(RandomTangent(dir), dir);
        go.SetActive(true); // the enemy's Start() initialises from the transform we just set
        return go;
    }

    void Prune(List<GameObject> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i] == null) list.RemoveAt(i);
    }

    Vector3 RandomDir(float maxDotWithShip)
    {
        Vector3 shipDir = ship != null ? (ship.position - C).normalized : Vector3.up;
        for (int i = 0; i < 40; i++)
        {
            Vector3 d = Random.onUnitSphere;
            if (Vector3.Dot(d, shipDir) < maxDotWithShip) return d;
        }
        return -shipDir;
    }

    Vector3 RandomTangent(Vector3 up)
    {
        Vector3 t = Vector3.ProjectOnPlane(Random.onUnitSphere, up);
        if (t.sqrMagnitude < 1e-4f) t = Vector3.ProjectOnPlane(Vector3.forward, up);
        return t.normalized;
    }
}
