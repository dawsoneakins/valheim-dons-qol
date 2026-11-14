namespace BaseStorage.Plugin.Feeding;

using System.Collections.Generic;
using BaseStorage.Core.Feeding;
using UnityEngine;

/// <summary>One station the feeder can fill, whatever its game type. Main thread only.</summary>
internal abstract class FeedTarget
{
    /// <summary>Initializes a new instance of the <see cref="FeedTarget"/> class.</summary>
    /// <param name="station">The station's game object component.</param>
    /// <param name="view">The station's network view.</param>
    protected FeedTarget(Component station, ZNetView view)
    {
        Station = station;
        View = view;
    }

    /// <summary>Gets the station's network view.</summary>
    internal ZNetView View { get; }

    /// <summary>Gets the station's game object component.</summary>
    internal Component Station { get; }

    /// <summary>Gets a value indicating whether the station still exists.</summary>
    internal bool IsAlive => Station != null;

    /// <summary>Gets the station's unlocalized name.</summary>
    internal abstract string Name { get; }

    /// <summary>Gets a value indicating whether the station can use what it is given right now.</summary>
    internal virtual bool IsReady => true;

    /// <summary>Reads what the station holds and accepts.</summary>
    /// <returns>The state.</returns>
    internal abstract FeedStationState State();

    /// <summary>Fills a list with the fuel and input items the station takes.</summary>
    /// <param name="items">The list to fill.</param>
    internal abstract void CopyItems(List<ItemDrop> items);

    /// <summary>Adds fuel the caller has already taken out of a chest. The caller has checked the room.</summary>
    /// <param name="amount">The whole items to add.</param>
    internal abstract void AddFuel(int amount);

    /// <summary>Adds one input the caller has already taken out of a chest. The caller has checked the room.</summary>
    /// <param name="prefabName">The item's prefab name.</param>
    /// <param name="cheated">The item's cheated mark.</param>
    internal abstract void AddInput(string prefabName, bool cheated);

    /// <summary>Plays the effect of inputs being added, for a station that does not play its own.</summary>
    internal virtual void InputsAdded()
    {
    }

    /// <summary>Gets a value indicating whether the station cooks food that can be collected.</summary>
    internal virtual bool CanCollect => false;

    /// <summary>Finds a slot holding cooked food. Burnt food is never found, so it stays for the player to see.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="prefabName">The cooked item's prefab name.</param>
    /// <param name="cheated">The item's cheated mark.</param>
    /// <returns>True when a slot holds cooked food.</returns>
    internal virtual bool TryFindCooked(out int slot, out string prefabName, out bool cheated)
    {
        slot = -1;
        prefabName = string.Empty;
        cheated = false;
        return false;
    }

    /// <summary>Tells whether a slot still holds a cooked item.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="prefabName">The item's prefab name.</param>
    /// <returns>True when it does.</returns>
    internal virtual bool HoldsCooked(int slot, string prefabName) => false;

    /// <summary>Empties a slot. The caller has checked it holds cooked food and owns the station.</summary>
    /// <param name="slot">The slot.</param>
    internal virtual void ClearSlot(int slot)
    {
    }

    /// <summary>Drops an item beside the station, the way the game does when a player takes it.</summary>
    /// <param name="slot">The slot it came from.</param>
    /// <param name="prefabName">The item's prefab name.</param>
    /// <param name="cheated">The item's cheated mark.</param>
    internal virtual void Drop(int slot, string prefabName, bool cheated)
    {
    }
}
