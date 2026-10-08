using System.Collections.Generic;
using UnityEngine;

/// <summary>Artificial gravity above the station's actual decks, including its exterior roof decks.</summary>
[DisallowMultipleComponent]
public sealed class StationGravity : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float heightAboveDeck = 3.75f;
    [SerializeField, Min(0f)] private float depthBelowDeck = 0.15f;
    private readonly List<Bounds> deckVolumes = new List<Bounds>();
    private static readonly HashSet<StationGravity> stations = new HashSet<StationGravity>();

    private void Awake()
    {
        foreach (var floor in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!floor.name.StartsWith("Floor_")) continue;
            Bounds deck = floor.bounds;
            float bottom = deck.min.y - depthBelowDeck;
            float top = deck.max.y + heightAboveDeck;
            deck.SetMinMax(new Vector3(deck.min.x, bottom, deck.min.z),
                new Vector3(deck.max.x, top, deck.max.z));
            deckVolumes.Add(deck);
        }
    }

    private void OnEnable() => stations.Add(this);
    private void OnDisable() => stations.Remove(this);

    public static bool Contains(Vector3 position)
    {
        foreach (var station in stations)
            foreach (var deck in station.deckVolumes)
                if (deck.Contains(position)) return true;
        return false;
    }
}
