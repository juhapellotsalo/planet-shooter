using UnityEngine;

/// <summary>
/// Inspector knob for the planet grid's bloom. URP/Unlit's Base Map colour is LDR (no HDR
/// intensity slider), so this exposes an HDR colour field — which DOES show an Intensity slider —
/// and pushes it to the shared grid-line material. Drag the Intensity to control how much the
/// grid blooms. Updates live in the editor (ExecuteAlways).
/// </summary>
[ExecuteAlways]
public class GridGlow : MonoBehaviour
{
    [ColorUsage(true, true)]
    [Tooltip("Grid line colour. Use the Intensity slider (HDR) to control the bloom amount.")]
    public Color color = new Color(0.42f, 1.18f, 1.40f, 1f);

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    public void Apply()
    {
        var lr = GetComponentInChildren<LineRenderer>(true);
        var mat = lr != null ? lr.sharedMaterial : null;
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }
}
