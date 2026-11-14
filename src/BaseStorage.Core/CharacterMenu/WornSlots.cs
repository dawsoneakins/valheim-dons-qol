namespace BaseStorage.Core.CharacterMenu;

using System;
using BaseStorage.Core.Persistence;

/// <summary>A slot that really holds its item, outside the inventory grid.</summary>
public enum WornSlot
{
    /// <summary>The helmet slot.</summary>
    Helmet = 0,

    /// <summary>The chest slot.</summary>
    Chest = 1,

    /// <summary>The legs slot.</summary>
    Legs = 2,

    /// <summary>The cape slot.</summary>
    Cape = 3,

    /// <summary>The utility slot.</summary>
    Utility = 4,

    /// <summary>The trinket slot.</summary>
    Trinket = 5,
}

/// <summary>
/// Where worn items are kept: the row just past the inventory grid, which the game saves but does not draw. The grid is 8x4 until a trader
/// sells extra rows, so the worn row follows the grid's height rather than sitting at a fixed row.
/// </summary>
public static class WornSlots
{
    /// <summary>The worn row of the game's normal 8x4 inventory.</summary>
    public const int DefaultRow = 4;

    /// <summary>The number of worn slots.</summary>
    public const int Count = 6;

    /// <summary>Gets the worn row of a grid.</summary>
    /// <param name="gridHeight">The inventory grid's height, without the worn row.</param>
    /// <returns>The row just past the grid.</returns>
    public static int RowFor(int gridHeight)
    {
        if (gridHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(gridHeight));
        }

        return gridHeight;
    }

    /// <summary>Gets a slot's position.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="gridHeight">The inventory grid's height, without the worn row.</param>
    /// <returns>The position.</returns>
    public static InventorySlot PositionOf(WornSlot slot, int gridHeight)
    {
        if (!Enum.IsDefined(typeof(WornSlot), slot))
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        return new InventorySlot((int)slot, RowFor(gridHeight));
    }

    /// <summary>Tells whether a position is a worn slot.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="gridHeight">The inventory grid's height, without the worn row.</param>
    /// <returns>True for a worn slot.</returns>
    public static bool IsWornPosition(int x, int y, int gridHeight) => gridHeight >= 1 && y == RowFor(gridHeight) && x >= 0 && x < Count;

    /// <summary>Gets an inventory's height counted with the worn row, for checks that a stack's position is real.</summary>
    /// <param name="gridHeight">The inventory grid's height.</param>
    /// <returns>The height with the worn row.</returns>
    public static int HeightWithWornRow(int gridHeight) => Math.Max(gridHeight, 1) + 1;

    /// <summary>Gets the height a grave needs so every item it holds can be seen.</summary>
    /// <param name="height">The grave's height.</param>
    /// <param name="lastRow">The last row any of its items sits in, or -1 when it is empty.</param>
    /// <returns>The height to give it.</returns>
    public static int GraveHeight(int height, int lastRow) => Math.Max(height, lastRow + 1);

    /// <summary>Works out the free grid spaces when some items sit outside the grid.</summary>
    /// <param name="width">The grid's width.</param>
    /// <param name="height">The grid's height.</param>
    /// <param name="items">Every item in the inventory.</param>
    /// <param name="outsideGrid">How many of them are outside the grid.</param>
    /// <returns>The free spaces, never below zero.</returns>
    public static int EmptySlots(int width, int height, int items, int outsideGrid) =>
        Math.Max(0, (width * height) - (items - outsideGrid));

    /// <summary>Works out where things go when an item is put on.</summary>
    /// <param name="item">The item's position.</param>
    /// <param name="slot">The slot it goes to.</param>
    /// <param name="gridHeight">The inventory grid's height, without the worn row.</param>
    /// <param name="occupied">Whether another item is in that slot.</param>
    /// <returns>The moves.</returns>
    public static WornSwap PutOn(InventorySlot item, WornSlot slot, int gridHeight, bool occupied) =>
        new WornSwap(PositionOf(slot, gridHeight), occupied ? item : (InventorySlot?)null);
}

/// <summary>Where an item goes when put on, and where the item it replaces goes.</summary>
public readonly struct WornSwap
{
    /// <summary>Initializes a new instance of the <see cref="WornSwap"/> struct.</summary>
    /// <param name="item">The new position of the item put on.</param>
    /// <param name="occupant">The new position of the replaced item, or null when there is none.</param>
    public WornSwap(InventorySlot item, InventorySlot? occupant)
    {
        Item = item;
        Occupant = occupant;
    }

    /// <summary>Gets the new position of the item put on.</summary>
    public InventorySlot Item { get; }

    /// <summary>Gets the new position of the replaced item, or null when there is none.</summary>
    public InventorySlot? Occupant { get; }
}

/// <summary>Which hand an item shown in a hand slot comes from.</summary>
public enum HandSource
{
    /// <summary>Neither hand.</summary>
    None = 0,

    /// <summary>The right hand.</summary>
    Right = 1,

    /// <summary>The left hand.</summary>
    Left = 2,
}

/// <summary>Picks what the weapon and shield slots show. The game keeps bows in the left hand, so the left hand is not always the shield.</summary>
public static class HandSlots
{
    /// <summary>Picks the weapon slot's hand.</summary>
    /// <param name="hasRight">Whether the right hand holds an item.</param>
    /// <param name="hasLeft">Whether the left hand holds an item.</param>
    /// <param name="leftIsOffHand">Whether that left item is a shield or a torch.</param>
    /// <returns>The hand.</returns>
    public static HandSource Weapon(bool hasRight, bool hasLeft, bool leftIsOffHand)
    {
        if (hasRight)
        {
            return HandSource.Right;
        }

        return hasLeft && !leftIsOffHand ? HandSource.Left : HandSource.None;
    }

    /// <summary>Picks the shield slot's hand.</summary>
    /// <param name="hasLeft">Whether the left hand holds an item.</param>
    /// <param name="leftIsOffHand">Whether that left item is a shield or a torch.</param>
    /// <returns>The hand.</returns>
    public static HandSource OffHand(bool hasLeft, bool leftIsOffHand) =>
        hasLeft && leftIsOffHand ? HandSource.Left : HandSource.None;
}
