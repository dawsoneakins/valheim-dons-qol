namespace BaseStorage.Plugin.Feeding;

using System;
using System.Collections.Generic;
using BaseStorage.Core.Snapshots;
using UnityEngine;

/// <summary>Counts for the compendium: what the base around a position holds, and which of its chests hold an item. Main thread only.</summary>
internal static partial class StationFeeder
{
    private static readonly List<ContainerSnapshot> StockChests = new List<ContainerSnapshot>();

    /// <summary>Adds up what the chests of the base around a position hold, by prefab name.</summary>
    /// <param name="position">The position.</param>
    /// <param name="counts">Receives the counts.</param>
    /// <returns>False when the position is not in a base.</returns>
    internal static bool CountBase(Vector3 position, Dictionary<string, int> counts)
    {
        if (!LoadChests(position, StockChests))
        {
            return false;
        }

        for (int i = 0; i < StockChests.Count; i++)
        {
            IReadOnlyList<ItemStackSnapshot> contents = StockChests[i].Contents;
            for (int j = 0; j < contents.Count; j++)
            {
                counts.TryGetValue(contents[j].PrefabName, out int count);
                counts[contents[j].PrefabName] = count + contents[j].Count;
            }
        }

        return true;
    }

    /// <summary>Finds the chests of the base around a position that hold an item.</summary>
    /// <param name="position">The position.</param>
    /// <param name="prefabName">The item's prefab name.</param>
    /// <param name="chests">Receives the chests.</param>
    internal static void FindHolding(Vector3 position, string prefabName, List<Container> chests)
    {
        chests.Clear();
        if (!LoadChests(position, StockChests))
        {
            return;
        }

        for (int i = 0; i < StockChests.Count; i++)
        {
            ContainerSnapshot snapshot = StockChests[i];
            for (int j = 0; j < snapshot.Contents.Count; j++)
            {
                if (string.Equals(snapshot.Contents[j].PrefabName, prefabName, StringComparison.Ordinal) && ById.TryGetValue(snapshot.Id, out Container? chest))
                {
                    chests.Add(chest);
                    break;
                }
            }
        }
    }
}
