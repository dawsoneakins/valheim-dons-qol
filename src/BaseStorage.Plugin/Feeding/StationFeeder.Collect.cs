namespace BaseStorage.Plugin.Feeding;

using System;
using BaseStorage.Core.CookedFood;
using BaseStorage.Core.Persistence;
using BaseStorage.Core.Reservations;
using BaseStorage.Core.Snapshots;
using BaseStorage.Plugin.Categories;
using BaseStorage.Plugin.CategoryButton;
using UnityEngine;

/// <summary>
/// Takes cooked food off cooking stations and ovens. Only the station's owner collects, one item per transfer. The chest is reserved
/// through the same handshake as feeding, and the slot is emptied only once the chest has room, in the same frame as the item goes in. Food
/// that can't go into a chest is dropped beside the station the way the game drops it, so nothing is lost. Burnt food is left alone.
/// Main thread only.
/// </summary>
internal static partial class StationFeeder
{
    private const string CollectOperation = "collect";
    private const string SetCollectRpc = "BaseStorage_SetCollect";

    private static CollectJob? collectJob;

    /// <summary>Reads what a station does with cooked food.</summary>
    /// <param name="station">The station.</param>
    /// <returns>The mode; off for a station that doesn't cook or has no valid view.</returns>
    internal static CollectMode ReadCollect(FeedTarget station)
    {
        ZNetView view = station.View;
        return station.CanCollect && view != null && view.IsValid()
            ? CollectSetting.Decode(view.GetZDO().GetString(PersistenceKeys.StationCollect, string.Empty), Read(station).Entries.Count > 0)
            : CollectMode.Off;
    }

    /// <summary>Stores what a station does with cooked food, through its owner.</summary>
    /// <param name="station">The station.</param>
    /// <param name="mode">The mode.</param>
    internal static void WriteCollect(FeedTarget station, CollectMode mode)
    {
        ZNetView view = station.View;
        if (station.CanCollect && view != null && view.IsValid())
        {
            view.InvokeRPC(SetCollectRpc, CollectSetting.Encode(mode));
        }
    }

    private static void RegisterCollect(ZNetView view) =>
        view.Register<string>(SetCollectRpc, (sender, value) =>
        {
            if (view.IsValid() && view.IsOwner())
            {
                view.GetZDO().Set(PersistenceKeys.StationCollect, value);
            }
        });

    /// <summary>Collects one cooked item from a station, or starts the chest transfer that will.</summary>
    /// <returns>True when something was done or started.</returns>
    private static bool TryCollect(FeedTarget station)
    {
        ZNetView? view = station.IsAlive && station.CanCollect ? station.View : null;
        if (view == null || !view.IsValid() || !view.IsOwner())
        {
            return false;
        }

        CollectMode mode = ReadCollect(station);
        if (mode == CollectMode.Off || !station.TryFindCooked(out int slot, out string prefab, out bool cheated))
        {
            return false;
        }

        // A cheated item keeps its mark only through the game's own drop.
        if (mode == CollectMode.Drop || cheated)
        {
            DropCooked(station, slot, prefab, cheated);
            return true;
        }

        GameObject? prefabObject = ObjectDB.instance == null ? null : ObjectDB.instance.GetItemPrefab(prefab);
        ItemDrop? drop = prefabObject == null ? null : prefabObject.GetComponent<ItemDrop>();
        if (drop == null)
        {
            return false;
        }

        Vector3 at = station.Station.transform.position;
        if (!LoadChests(at, Chests))
        {
            DropCooked(station, slot, prefab, cheated);
            return true;
        }

        string category = PluginCategoryCatalog.Current.Resolve(prefab, ItemKindAdapter.From(drop.m_itemData.m_shared));
        var position = new WorldPosition(at.x, at.y, at.z);
        ContainerId? any = CookedFoodPlanner.PickChest(prefab, drop.m_itemData.m_quality, category, Chests, Assignments, position, Array.Empty<ContainerId>());
        if (any == null)
        {
            // No chest of the base takes it: drop it, so the station keeps cooking.
            DropCooked(station, slot, prefab, cheated);
            return true;
        }

        // Chests that were busy a moment ago are left for later rather than dropping food a free chest could take.
        ContainerId? target = CookedFoodPlanner.PickChest(prefab, drop.m_itemData.m_quality, category, Chests, Assignments, position, Cooldowns.Keys);
        if (target == null || !ById.TryGetValue(target.Value, out Container? chest))
        {
            return false;
        }

        if (driver!.Begin(chest, CollectOperation) == null)
        {
            Cool(target.Value);
            return false;
        }

        collectJob = new CollectJob(station, chest, target.Value, slot, prefab);
        return true;
    }

    private static void AdvanceCollect(CollectJob current)
    {
        Reservation<string>? reservation = driver!.Find(current.ChestId);
        if (reservation == null || !string.Equals(reservation.Operation, CollectOperation, StringComparison.Ordinal))
        {
            collectJob = null;
            return;
        }

        if (reservation.State == ReservationState.Requesting)
        {
            return;
        }

        if (reservation.State == ReservationState.Granted)
        {
            driver.BeginWrite(current.ChestId);
        }

        if (driver.CanWrite(current.ChestId))
        {
            MoveCooked(current);
        }
        else
        {
            Cool(current.ChestId);
        }

        driver.Release(current.ChestId);
        collectJob = null;
    }

    /// <summary>Moves the item in one frame, after checking the station is still ours and the item is still there.</summary>
    private static void MoveCooked(CollectJob current)
    {
        FeedTarget station = current.Station;
        Inventory? inventory = current.Chest == null ? null : current.Chest.GetInventory();
        GameObject? prefab = ObjectDB.instance == null ? null : ObjectDB.instance.GetItemPrefab(current.Prefab);
        if (!station.IsAlive || inventory == null || prefab == null || !station.View.IsValid() || !station.View.IsOwner()
            || !station.HoldsCooked(current.Slot, current.Prefab))
        {
            return;
        }

        if (!inventory.CanAddItem(prefab, 1))
        {
            Cool(current.ChestId);
            return;
        }

        station.ClearSlot(current.Slot);
        if (!inventory.AddItem(prefab, 1))
        {
            station.Drop(current.Slot, current.Prefab, false);
        }
    }

    private static void DropCooked(FeedTarget station, int slot, string prefab, bool cheated)
    {
        station.ClearSlot(slot);
        station.Drop(slot, prefab, cheated);
    }

    private sealed class CollectJob
    {
        internal CollectJob(FeedTarget station, Container chest, ContainerId chestId, int slot, string prefab)
        {
            Station = station;
            Chest = chest;
            ChestId = chestId;
            Slot = slot;
            Prefab = prefab;
        }

        internal FeedTarget Station { get; }

        internal Container Chest { get; }

        internal ContainerId ChestId { get; }

        internal int Slot { get; }

        internal string Prefab { get; }
    }
}
