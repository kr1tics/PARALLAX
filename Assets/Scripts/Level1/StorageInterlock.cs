using UnityEngine;

/// <summary>Reversible storage hatch with a player escape volume and doorway clearance check.</summary>
public sealed class StorageInterlock : MonoBehaviour
{
    [SerializeField] private Door door;
    [SerializeField] private BoxCollider doorShape;
    [SerializeField] private PlayerController player;
    [SerializeField] private Bounds storageInterior = new Bounds(new Vector3(-6, 0.9f, 15), new Vector3(4.8f, 4.2f, 6.6f));
    [SerializeField, Min(0.05f)] private float clearance = 0.2f;
    private readonly Collider[] hits = new Collider[32];
    private Collider[] playerColliders;
    public bool IsOpen { get; private set; }
    public bool ClosingDeferred { get; private set; }

    void Awake() => playerColliders = player.GetComponentsInChildren<Collider>();

    // Called after the time-state update, so newly activated timeline objects count immediately.
    public void Refresh(bool releaseRequested)
    {
        Physics.SyncTransforms();
        ClosingDeferred = !releaseRequested && Occupied();
        bool open = releaseRequested || ClosingDeferred;
        if (open == IsOpen) return;
        IsOpen = open;
        if (open) door.onStartTrigger(); else door.onEndTrigger();
    }

    bool Occupied()
    {
        // Checking the entire chamber lets a player leave even if gravity changes while inside.
        foreach (var collider in playerColliders)
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
                storageInterior.Intersects(collider.bounds)) return true;

        // Compute the box from its shape: disabled colliders have empty .bounds.
        var t = doorShape.transform;
        Vector3 scale = t.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 halfSize = Vector3.Scale(doorShape.size, scale) * 0.5f + Vector3.one * clearance;
        int count = Physics.OverlapBoxNonAlloc(t.TransformPoint(doorShape.center), halfSize, hits,
            t.rotation, ~0, QueryTriggerInteraction.Ignore);
        // Fail open if a crowded doorway exceeds the query buffer.
        if (count == hits.Length) return true;
        for (int i = 0; i < count; i++)
            if (hits[i] != doorShape && hits[i].attachedRigidbody != null) return true;
        return false;
    }

    // Checkpoint reset relocates the player and cubes before closing the hatch.
    public void ResetClosed()
    {
        IsOpen = ClosingDeferred = false;
        door.onEndTrigger();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(storageInterior.center, storageInterior.size);
    }
}
