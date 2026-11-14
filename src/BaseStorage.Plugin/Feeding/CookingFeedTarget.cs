namespace BaseStorage.Plugin.Feeding;

using System.Collections.Generic;
using BaseStorage.Core.Feeding;
using BaseStorage.Shared;

/// <summary>A cooking station, iron cooking station or oven: each slot holds one item, and an oven also burns fuel.</summary>
internal sealed class CookingFeedTarget : FeedTarget
{
    private readonly CookingStation cooker;

    /// <summary>Initializes a new instance of the <see cref="CookingFeedTarget"/> class.</summary>
    /// <param name="station">The station.</param>
    internal CookingFeedTarget(CookingStation station)
        : base(station, station.m_nview)
    {
        cooker = station;
    }

    /// <inheritdoc/>
    internal override string Name => cooker.m_name;

    /// <summary>Gets a value indicating whether food put on now would cook: a station over a fire waits for the fire.</summary>
    internal override bool IsReady => !cooker.m_requireFire || cooker.IsFireLit();

    /// <inheritdoc/>
    internal override FeedStationState State()
    {
        var inputs = new List<string>(cooker.m_conversion.Count);
        foreach (CookingStation.ItemConversion conversion in cooker.m_conversion)
        {
            if (conversion.m_from != null)
            {
                inputs.Add(conversion.m_from.gameObject.name);
            }
        }

        int occupied = 0;
        for (int i = 0; i < cooker.m_slots.Length; i++)
        {
            cooker.GetSlot(i, out string item, out _, out _, out _);
            if (!string.IsNullOrEmpty(item))
            {
                occupied++;
            }
        }

        string? fuel = cooker.m_useFuel && cooker.m_fuelItem != null ? cooker.m_fuelItem.gameObject.name : null;
        return FeedStationState.SeveralInputFuelled(cooker.m_slots.Length, occupied, fuel == null ? 0f : cooker.GetFuel(), cooker.m_maxFuel, fuel, inputs);
    }

    /// <inheritdoc/>
    internal override void CopyItems(List<ItemDrop> items)
    {
        if (cooker.m_useFuel && cooker.m_fuelItem != null)
        {
            items.Add(cooker.m_fuelItem);
        }

        foreach (CookingStation.ItemConversion conversion in cooker.m_conversion)
        {
            if (conversion.m_from != null)
            {
                items.Add(conversion.m_from);
            }
        }
    }

    /// <inheritdoc/>
    internal override void AddFuel(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            View.InvokeRPC(PatchTargetNames.CookingStation.AddFuelRpc);
        }
    }

    /// <inheritdoc/>
    internal override void AddInput(string prefabName, bool cheated) =>
        View.InvokeRPC(PatchTargetNames.CookingStation.AddItemRpc, prefabName, cheated);

    /// <inheritdoc/>
    internal override bool CanCollect => true;

    /// <inheritdoc/>
    internal override bool TryFindCooked(out int slot, out string prefabName, out bool cheated)
    {
        for (int i = 0; i < cooker.m_slots.Length; i++)
        {
            cooker.GetSlot(i, out string item, out _, out _, out bool mark);
            if (IsCooked(item))
            {
                slot = i;
                prefabName = item;
                cheated = mark;
                return true;
            }
        }

        slot = -1;
        prefabName = string.Empty;
        cheated = false;
        return false;
    }

    /// <inheritdoc/>
    internal override bool HoldsCooked(int slot, string prefabName)
    {
        if (slot < 0 || slot >= cooker.m_slots.Length)
        {
            return false;
        }

        cooker.GetSlot(slot, out string item, out _, out _, out _);
        return IsCooked(item) && string.Equals(item, prefabName, System.StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    internal override void ClearSlot(int slot)
    {
        // The same writes as the game's own take-out: an empty slot, then every player's view of it.
        cooker.SetSlot(slot, string.Empty, 0f, CookingStation.Status.NotDone, false, 0L);
        View.InvokeRPC(ZNetView.Everybody, PatchTargetNames.CookingStation.SetSlotVisualRpc, slot, string.Empty);
    }

    /// <inheritdoc/>
    internal override void Drop(int slot, string prefabName, bool cheated) =>
        cooker.SpawnItem(prefabName, slot, cooker.transform.position, cheated);

    /// <summary>Tells whether an item name is a station's finished food: the result of a conversion, and not the burnt item.</summary>
    private bool IsCooked(string item)
    {
        if (string.IsNullOrEmpty(item) || (cooker.m_overCookedItem != null && item == cooker.m_overCookedItem.name))
        {
            return false;
        }

        foreach (CookingStation.ItemConversion conversion in cooker.m_conversion)
        {
            if (conversion.m_to != null && conversion.m_to.gameObject.name == item)
            {
                return true;
            }
        }

        return false;
    }
}
