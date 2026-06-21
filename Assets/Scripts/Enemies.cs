using System.Collections.Generic;
using UnityEngine;

/// <summary>Anything a bullet can hit. Implemented by every enemy type.</summary>
public interface IEnemy
{
    float HitRadius { get; }
    Vector3 Position { get; }   // world-space
    bool TakeDamage(int dmg);   // returns true if this hit killed it
}

/// <summary>Global registry of live enemies, for cheap bullet collision tests.</summary>
public static class Enemies
{
    public static readonly List<IEnemy> Active = new List<IEnemy>();
    public static void Register(IEnemy e) { if (!Active.Contains(e)) Active.Add(e); }
    public static void Unregister(IEnemy e) { Active.Remove(e); }

    // Clear stale state at each Play start (needed when Domain Reload is disabled).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active.Clear();
}
