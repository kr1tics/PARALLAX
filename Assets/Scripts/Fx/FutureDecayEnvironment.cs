using UnityEngine;

// Observes Amy's completed time swap; does not initiate or modify it.
public sealed class FutureDecayEnvironment : MonoBehaviour
{
    [System.Serializable]
    public struct Surface
    {
        public Renderer renderer;
        public Material[] past;
        public Material[] future;
    }
    [SerializeField] private Surface[] surfaces;
    private TimeState? applied;

    private void LateUpdate()
    {
        if (!applied.HasValue)
        {
            // Stable per-surface variation; do not change it when time or position changes.
            var properties = new MaterialPropertyBlock();
            for (int index = 0; index < surfaces.Length; index++)
            {
                var renderer = surfaces[index].renderer;
                if (renderer == null) continue;
                properties.Clear();
                renderer.GetPropertyBlock(properties);
                properties.SetFloat("_WeatherSeed", Mathf.Repeat((index + 1) * 0.618034f, 1f));
                renderer.SetPropertyBlock(properties);
            }
        }
        TimeState now = GlobalState.get().now;
        if (applied == now) return;
        foreach (var surface in surfaces)
            if (surface.renderer != null)
                surface.renderer.sharedMaterials = now == TimeState.Future ? surface.future : surface.past;
        applied = now;
    }

    public void Configure(Surface[] value) { surfaces = value; }
}
