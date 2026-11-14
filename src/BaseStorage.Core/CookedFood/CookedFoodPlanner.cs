namespace BaseStorage.Core.CookedFood;

using System;
using System.Collections.Generic;
using BaseStorage.Core.Persistence;
using BaseStorage.Core.Snapshots;

/// <summary>
/// Picks the chest a cooked item goes to. Pure. A chest assigned the item's category comes first, then an unassigned chest that already
/// holds the item. Keep chests, chests of other categories, graves and locked chests are never used. The nearest chest with room wins.
/// </summary>
public static class CookedFoodPlanner
{
    /// <summary>Picks a chest for one item.</summary>
    /// <param name="prefabName">The item's prefab name.</param>
    /// <param name="quality">The item's quality; only a stack of the same quality can take it.</param>
    /// <param name="category">The item's category.</param>
    /// <param name="chests">The base's chests, with current contents.</param>
    /// <param name="assignments">The category per chest id. A missing id means unassigned.</param>
    /// <param name="stationPosition">Where the station is.</param>
    /// <param name="skip">Chests to leave alone for now.</param>
    /// <returns>The chest, or null when none has room.</returns>
    public static ContainerId? PickChest(
        string prefabName,
        int quality,
        string category,
        IReadOnlyList<ContainerSnapshot> chests,
        IReadOnlyDictionary<ContainerId, CategoryAssignment> assignments,
        WorldPosition stationPosition,
        ICollection<ContainerId> skip)
    {
        if (string.IsNullOrEmpty(prefabName))
        {
            throw new ArgumentException("A prefab name is required.", nameof(prefabName));
        }

        if (category is null)
        {
            throw new ArgumentNullException(nameof(category));
        }

        if (chests is null)
        {
            throw new ArgumentNullException(nameof(chests));
        }

        if (assignments is null)
        {
            throw new ArgumentNullException(nameof(assignments));
        }

        if (skip is null)
        {
            throw new ArgumentNullException(nameof(skip));
        }

        ContainerId? best = null;
        var bestTier = int.MaxValue;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < chests.Count; i++)
        {
            ContainerSnapshot chest = chests[i];
            if (chest is null || (chest.Flags & (ContainerFlags.AccessDenied | ContainerFlags.Tombstone)) != 0 || skip.Contains(chest.Id))
            {
                continue;
            }

            var tier = TierOf(chest, prefabName, category, assignments);
            if (tier < 0 || tier > bestTier || !HasRoom(chest, prefabName, quality))
            {
                continue;
            }

            var distance = DistanceSquared(chest.Position, stationPosition);
            if (tier < bestTier || distance < bestDistance)
            {
                best = chest.Id;
                bestTier = tier;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Tells whether a chest can take one more of an item.</summary>
    /// <param name="chest">The chest.</param>
    /// <param name="prefabName">The item's prefab name.</param>
    /// <param name="quality">The item's quality.</param>
    /// <returns>True when a slot is free or a stack of the item has space.</returns>
    public static bool HasRoom(ContainerSnapshot chest, string prefabName, int quality)
    {
        if (chest is null)
        {
            throw new ArgumentNullException(nameof(chest));
        }

        if (chest.Contents.Count < chest.SlotCount)
        {
            return true;
        }

        for (var i = 0; i < chest.Contents.Count; i++)
        {
            ItemStackSnapshot stack = chest.Contents[i];
            if (stack.Quality == quality && stack.Count < stack.MaxStack && string.Equals(stack.PrefabName, prefabName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int TierOf(ContainerSnapshot chest, string prefabName, string category, IReadOnlyDictionary<ContainerId, CategoryAssignment> assignments)
    {
        if (assignments.TryGetValue(chest.Id, out CategoryAssignment? assignment) && assignment is not null)
        {
            return !assignment.IsKeep && string.Equals(assignment.CategoryName, category, StringComparison.Ordinal) ? 0 : -1;
        }

        for (var i = 0; i < chest.Contents.Count; i++)
        {
            if (string.Equals(chest.Contents[i].PrefabName, prefabName, StringComparison.Ordinal))
            {
                return 1;
            }
        }

        return -1;
    }

    private static float DistanceSquared(WorldPosition a, WorldPosition b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        var z = a.Z - b.Z;
        return (x * x) + (y * y) + (z * z);
    }
}
