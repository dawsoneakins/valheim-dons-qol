namespace BaseStorage.Core.Tests.CookedFood;

using System;
using System.Collections.Generic;
using BaseStorage.Core.CookedFood;
using BaseStorage.Core.Persistence;
using BaseStorage.Core.Snapshots;
using static BaseStorage.Core.Tests.Organize.OrganizeFixtures;

/// <summary>Where cooked food goes, and how a station's collect setting is stored.</summary>
public sealed class CookedFoodTests
{
    private const string Food = "Cooked Food";
    private static readonly WorldPosition At = new WorldPosition(0f, 30f, 0f);
    private static readonly ContainerId[] NoSkip = Array.Empty<ContainerId>();
    private static readonly Dictionary<ContainerId, CategoryAssignment> None = new Dictionary<ContainerId, CategoryAssignment>();

    [Theory]
    [InlineData("off", true, CollectMode.Off)]
    [InlineData("chests", false, CollectMode.ToChests)]
    [InlineData("drop", true, CollectMode.Drop)]
    [InlineData("", true, CollectMode.ToChests)]
    [InlineData("", false, CollectMode.Off)]
    [InlineData(null, true, CollectMode.ToChests)]
    [InlineData("nonsense", false, CollectMode.Off)]
    public void ASettingReadsBackAndAnUnsetFedStationCollects(string? stored, bool fed, CollectMode expected) =>
        Assert.Equal(expected, CollectSetting.Decode(stored, fed));

    [Theory]
    [InlineData(CollectMode.Off)]
    [InlineData(CollectMode.ToChests)]
    [InlineData(CollectMode.Drop)]
    public void EveryModeSurvivesStoring(CollectMode mode) =>
        Assert.Equal(mode, CollectSetting.Decode(CollectSetting.Encode(mode), fed: mode != CollectMode.ToChests));

    [Fact]
    public void ClickingCyclesThroughEveryMode()
    {
        Assert.Equal(CollectMode.ToChests, CollectSetting.Next(CollectMode.Off));
        Assert.Equal(CollectMode.Drop, CollectSetting.Next(CollectMode.ToChests));
        Assert.Equal(CollectMode.Off, CollectSetting.Next(CollectMode.Drop));
    }

    [Fact]
    public void AChestOfTheFoodCategoryWinsOverANearerChestAlreadyHoldingIt()
    {
        var chests = new[] { C(1, 1f, 0f, 4, S("CookedMeat", 3)), C(2, 20f, 0f, 4) };

        Assert.Equal(Id(2), Pick(chests, Assign((2, CategoryAssignment.ForCategory(Food)))));
    }

    [Fact]
    public void TheNearestChestOfTheCategoryWins()
    {
        var chests = new[] { C(1, 30f, 0f, 4), C(2, 5f, 0f, 4) };

        Assert.Equal(Id(2), Pick(chests, Assign((1, CategoryAssignment.ForCategory(Food)), (2, CategoryAssignment.ForCategory(Food)))));
    }

    [Fact]
    public void AnUnassignedChestAlreadyHoldingTheFoodIsUsedWhenNoChestHasTheCategory() =>
        Assert.Equal(Id(2), Pick(new[] { C(1, 1f, 0f, 4), C(2, 9f, 0f, 4, S("CookedMeat", 3)) }, None));

    [Fact]
    public void KeepChestsAndOtherCategoriesAreNeverUsed()
    {
        var chests = new[] { C(1, 1f, 0f, 4, S("CookedMeat", 3)), C(2, 2f, 0f, 4, S("CookedMeat", 3)) };
        var assignments = Assign((1, CategoryAssignment.Keep), (2, CategoryAssignment.ForCategory("Ores")));

        Assert.Null(Pick(chests, assignments));
    }

    [Fact]
    public void AFullChestIsPassedOverUnlessAStackHasSpace()
    {
        var full = C(1, 1f, 0f, 1, S("CookedMeat", 50));
        var topUp = C(2, 9f, 0f, 1, S("CookedMeat", 49));

        Assert.Equal(Id(2), Pick(new[] { full, topUp }, None));
        Assert.Null(Pick(new[] { full }, None));
    }

    [Fact]
    public void AStackOfAnotherQualityIsNoRoom() =>
        Assert.False(CookedFoodPlanner.HasRoom(C(1, 0f, 0f, 1, S("CookedMeat", 1, quality: 2)), "CookedMeat", 1));

    [Fact]
    public void LockedChestsGravesAndSkippedChestsAreNeverUsed()
    {
        var assignments = Assign((1, CategoryAssignment.ForCategory(Food)), (2, CategoryAssignment.ForCategory(Food)), (3, CategoryAssignment.ForCategory(Food)));
        var chests = new[]
        {
            CF(1, 1f, 0f, 4, ContainerFlags.PlayerBuilt | ContainerFlags.AccessDenied),
            CF(2, 2f, 0f, 4, ContainerFlags.Tombstone),
            C(3, 3f, 0f, 4),
        };

        Assert.Equal(Id(3), CookedFoodPlanner.PickChest("CookedMeat", 1, Food, chests, assignments, At, NoSkip));
        Assert.Null(CookedFoodPlanner.PickChest("CookedMeat", 1, Food, chests, assignments, At, new[] { Id(3) }));
    }

    [Fact]
    public void BadArgumentsAreRefused()
    {
        Assert.Throws<ArgumentException>(() => CookedFoodPlanner.PickChest(string.Empty, 1, Food, Array.Empty<ContainerSnapshot>(), None, At, NoSkip));
        Assert.Throws<ArgumentNullException>(() => CookedFoodPlanner.PickChest("CookedMeat", 1, null!, Array.Empty<ContainerSnapshot>(), None, At, NoSkip));
        Assert.Throws<ArgumentNullException>(() => CookedFoodPlanner.PickChest("CookedMeat", 1, Food, null!, None, At, NoSkip));
        Assert.Throws<ArgumentOutOfRangeException>(() => CollectSetting.Encode((CollectMode)9));
    }

    private static ContainerId? Pick(IReadOnlyList<ContainerSnapshot> chests, Dictionary<ContainerId, CategoryAssignment> assignments) =>
        CookedFoodPlanner.PickChest("CookedMeat", 1, Food, chests, assignments, At, NoSkip);
}
