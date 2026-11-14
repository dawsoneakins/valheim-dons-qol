namespace BaseStorage.Core.Tests.CharacterMenu;

using System;
using BaseStorage.Core.CharacterMenu;
using BaseStorage.Core.Persistence;

/// <summary>Where worn items live, how free space is counted, and what the hand slots show.</summary>
public sealed class WornSlotsTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(9)]
    public void EverySlotHasItsOwnPositionJustPastTheGrid(int gridHeight)
    {
        var seen = new System.Collections.Generic.HashSet<InventorySlot>();
        foreach (WornSlot slot in Enum.GetValues(typeof(WornSlot)))
        {
            InventorySlot position = WornSlots.PositionOf(slot, gridHeight);
            Assert.Equal(gridHeight, position.Y);
            Assert.True(WornSlots.IsWornPosition(position.X, position.Y, gridHeight));
            Assert.True(seen.Add(position));
        }

        Assert.Equal(WornSlots.Count, seen.Count);
    }

    [Fact]
    public void TheNormalInventoryKeepsTheRowItAlwaysHad() =>
        Assert.Equal(WornSlots.DefaultRow, WornSlots.RowFor(4));

    [Fact]
    public void ABoughtRowMovesTheWornRowDownSoItNeverOverlapsTheGrid()
    {
        Assert.Equal(5, WornSlots.RowFor(5));
        Assert.False(WornSlots.IsWornPosition(0, 4, 5));
        Assert.True(WornSlots.IsWornPosition(0, 5, 5));
    }

    [Theory]
    [InlineData(0, 3, 4)]
    [InlineData(6, 4, 4)]
    [InlineData(-1, 4, 4)]
    [InlineData(0, 5, 4)]
    [InlineData(0, 4, 5)]
    [InlineData(0, 0, 0)]
    public void OtherPositionsAreNotWorn(int x, int y, int gridHeight) => Assert.False(WornSlots.IsWornPosition(x, y, gridHeight));

    [Fact]
    public void AnUndefinedSlotIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => WornSlots.PositionOf((WornSlot)99, 4));

    [Fact]
    public void AGridWithNoRowsIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => WornSlots.RowFor(0));

    [Theory]
    [InlineData(4, 5)]
    [InlineData(5, 6)]
    [InlineData(9, 10)]
    public void TheHeightCheckAllowsTheWornRow(int height, int expected) =>
        Assert.Equal(expected, WornSlots.HeightWithWornRow(height));

    [Theory]
    [InlineData(4, -1, 4)]
    [InlineData(4, 3, 4)]
    [InlineData(4, 4, 5)]
    [InlineData(5, 5, 6)]
    [InlineData(6, 4, 6)]
    public void AGraveGrowsToShowItsLastRow(int height, int lastRow, int expected) =>
        Assert.Equal(expected, WornSlots.GraveHeight(height, lastRow));

    [Theory]
    [InlineData(0, 0, 32)]
    [InlineData(32, 0, 0)]
    [InlineData(38, 6, 0)]
    [InlineData(26, 6, 12)]
    [InlineData(6, 6, 32)]
    public void WornItemsDoNotUseGridSpace(int items, int outside, int expected) =>
        Assert.Equal(expected, WornSlots.EmptySlots(8, 4, items, outside));

    [Fact]
    public void FreeSpaceIsNeverNegative() => Assert.Equal(0, WornSlots.EmptySlots(8, 4, 40, 0));

    [Fact]
    public void PuttingOnIntoAnEmptySlotMovesOnlyTheItem()
    {
        WornSwap swap = WornSlots.PutOn(new InventorySlot(3, 2), WornSlot.Chest, 5, occupied: false);

        Assert.Equal(WornSlots.PositionOf(WornSlot.Chest, 5), swap.Item);
        Assert.Null(swap.Occupant);
    }

    [Fact]
    public void PuttingOnIntoAnOccupiedSlotSwapsSoAFullInventoryLosesNothing()
    {
        var from = new InventorySlot(3, 2);

        WornSwap swap = WornSlots.PutOn(from, WornSlot.Helmet, 4, occupied: true);

        Assert.Equal(WornSlots.PositionOf(WornSlot.Helmet, 4), swap.Item);
        Assert.Equal(from, swap.Occupant);
    }

    [Theory]
    [InlineData(true, false, false, HandSource.Right)]
    [InlineData(true, true, true, HandSource.Right)]
    [InlineData(false, true, false, HandSource.Left)]
    [InlineData(false, true, true, HandSource.None)]
    [InlineData(false, false, false, HandSource.None)]
    public void TheWeaponSlotFindsBowsInTheLeftHand(bool right, bool left, bool offHand, HandSource expected) =>
        Assert.Equal(expected, HandSlots.Weapon(right, left, offHand));

    [Theory]
    [InlineData(true, true, HandSource.Left)]
    [InlineData(true, false, HandSource.None)]
    [InlineData(false, false, HandSource.None)]
    public void TheShieldSlotShowsOnlyAnOffHandItem(bool left, bool offHand, HandSource expected) =>
        Assert.Equal(expected, HandSlots.OffHand(left, offHand));
}
