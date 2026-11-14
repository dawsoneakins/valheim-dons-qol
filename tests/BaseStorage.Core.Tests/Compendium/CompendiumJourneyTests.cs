namespace BaseStorage.Core.Tests.Compendium;

using System.Collections.Generic;
using System.Linq;
using BaseStorage.Core.Compendium;

/// <summary>The journal, the silhouettes of what's not yet found, and the home, settings and stock additions to the book.</summary>
public sealed class CompendiumJourneyTests
{
    private static CompendiumData Data()
    {
        var data = new CompendiumData();
        data.Items.Add(new ItemRecord { Id = "Wood", Name = "Wood", Category = ItemCategory.Material });
        data.Items.Add(new ItemRecord { Id = "Resin", Name = "Resin", Category = ItemCategory.Material });
        data.Items.Add(new ItemRecord { Id = "LeatherScraps", Name = "Leather scraps", Category = ItemCategory.Material });
        data.Items.Add(new ItemRecord { Id = "TrophyGreydwarf", Name = "Greydwarf trophy", Category = ItemCategory.Trophy });
        data.Items.Add(new ItemRecord { Id = "Ooze", Name = "Ooze", Category = ItemCategory.Material });
        var club = new ItemRecord { Id = "Club", Name = "Club", Category = ItemCategory.Weapon };
        club.Damage.Add(new DamageAmount { Type = CompendiumDamage.Blunt, Amount = 12f });
        data.Items.Add(club);
        var bow = new ItemRecord { Id = "Bow", Name = "Crude bow", Category = ItemCategory.Weapon };
        bow.Damage.Add(new DamageAmount { Type = CompendiumDamage.Pierce, Amount = 10f });
        data.Items.Add(bow);
        var axe = new ItemRecord { Id = "Axe", Name = "Stone axe", Category = ItemCategory.Weapon };
        axe.Damage.Add(new DamageAmount { Type = CompendiumDamage.Chop, Amount = 50f });
        data.Items.Add(axe);

        var recipe = new RecipeRecord { ResultId = "Club", Amount = 1, Station = "Workbench" };
        recipe.Ingredients.Add(new IngredientRecord { ItemId = "Wood", Amount = 6 });
        data.Recipes.Add(recipe);
        var torch = new RecipeRecord { ResultId = "Resin", Amount = 2 };
        torch.Ingredients.Add(new IngredientRecord { ItemId = "Wood", Amount = 1 });
        data.Recipes.Add(torch);

        var boar = new CreatureRecord { Id = "Boar", Name = "Boar", Health = 10f };
        boar.Drops.Add(new CreatureDropRecord { ItemId = "LeatherScraps", Min = 1, Max = 1, Chance = 1f });
        data.Creatures.Add(boar);
        var greydwarf = new CreatureRecord { Id = "Greydwarf", Name = "Greydwarf", Health = 41f };
        greydwarf.Drops.Add(new CreatureDropRecord { ItemId = "Resin", Chance = 1f });
        greydwarf.Drops.Add(new CreatureDropRecord { ItemId = "TrophyGreydwarf", Chance = 0.05f });
        greydwarf.Responses.Add(new DamageResponseEntry { Type = CompendiumDamage.Pierce, Response = DamageResponse.Weak });
        data.Creatures.Add(greydwarf);
        var blob = new CreatureRecord { Id = "Blob", Name = "Blob", Health = 50f };
        blob.Drops.Add(new CreatureDropRecord { ItemId = "Ooze", Chance = 1f });
        data.Creatures.Add(blob);
        data.Creatures.Add(new CreatureRecord { Id = "Eikthyr", Name = "Eikthyr", Boss = true, Health = 500f });

        data.Spawns.Add(new SpawnRecord { CreatureId = "Boar", Biome = CompendiumBiome.Meadows, Day = true, Night = true });
        data.Spawns.Add(new SpawnRecord { CreatureId = "Greydwarf", Biome = CompendiumBiome.BlackForest, Night = true });
        data.Spawns.Add(new SpawnRecord { CreatureId = "Blob", Biome = CompendiumBiome.Swamp, Night = true });
        data.Spawns.Add(new SpawnRecord { CreatureId = "Eikthyr", Biome = CompendiumBiome.Meadows, Day = true });

        var tree = new ResourceRecord { Name = "Beech" };
        tree.Biomes.Add(CompendiumBiome.Meadows);
        tree.ItemIds.Add("Wood");
        data.Resources.Add(tree);
        return data;
    }

    private static CompendiumKnowledge Knows(params string[] items)
    {
        var knowledge = new CompendiumKnowledge();
        knowledge.Biomes.Add(CompendiumBiome.Meadows);
        knowledge.Biomes.Add(CompendiumBiome.BlackForest);
        knowledge.Creatures.Add("Boar");
        knowledge.Items.UnionWith(items);
        knowledge.Stations.Add("Workbench");
        return knowledge;
    }

    [Fact]
    public void TheJournalSurvivesSavingAndSkipsDamagedEntries()
    {
        var journal = new CompendiumJournal(null) { Spoilers = SpoilerLevel.LookAhead, SecondHint = SecondHintRule.Never, ShowCounts = false, TellWhenNear = false };
        journal.ShowNextHint("frontier:creature:Greydwarf", 2);
        journal.Reveal("frontier:item:Ooze");
        journal.ToggleTracked("frontier:creature:Blob");
        journal.Start(new[] { "item:Wood" });

        var copy = new CompendiumJournal(journal.ToSaved() + ";h:x=99;o:spoil=7;o:junk;;r:a;b");

        Assert.Equal(journal.ToSaved().Replace(";r:a", string.Empty), copy.ToSaved().Replace(";r:a", string.Empty));
        Assert.Equal(SpoilerLevel.LookAhead, copy.Spoilers);
        Assert.Equal(SecondHintRule.Never, copy.SecondHint);
        Assert.False(copy.ShowCounts);
        Assert.True(copy.ShowTracked);
        Assert.Equal(2, copy.ShownHints("frontier:creature:Greydwarf"));
        Assert.Equal(1, copy.ShownHints("x"));
        Assert.True(copy.IsRevealed("frontier:item:Ooze"));
        Assert.True(copy.IsTracked("frontier:creature:Blob"));
        Assert.True(copy.IsRead("item:Wood"));
        Assert.True(copy.Started);
    }

    [Fact]
    public void ANewJournalDefaultsToHintsAndRefusesBadIds()
    {
        var journal = new CompendiumJournal("garbage");

        Assert.Equal(SpoilerLevel.Hints, journal.Spoilers);
        Assert.Equal(SecondHintRule.HalfFound, journal.SecondHint);
        Assert.False(journal.Reveal("a;b"));
        Assert.False(journal.ShowNextHint("a", 1));
        Assert.True(journal.ShowNextHint("a", 2));
        Assert.False(journal.ShowNextHint("a", 2));
        Assert.True(journal.Start(new[] { "item:Wood" }));
        Assert.False(journal.Start(new[] { "item:Resin" }));
        Assert.False(journal.IsRead("item:Resin"));
    }

    [Fact]
    public void OnlyVisitedLandsGetSilhouettesAndBossesNever()
    {
        FrontierResult frontier = CompendiumFrontier.Build(Data(), Knows("Wood"), SpoilerLevel.Hints);

        Assert.Equal(new[] { "frontier:item:LeatherScraps", "frontier:creature:Greydwarf", "frontier:item:Resin", "frontier:item:TrophyGreydwarf" }, frontier.Entries.Select(e => e.Id));
        Assert.DoesNotContain(frontier.Entries, e => e.Id.Contains("Blob") || e.Id.Contains("Ooze") || e.Id.Contains("Eikthyr"));
        Assert.Equal(CompendiumBiome.Meadows, frontier.Progress[0].Biome);
        Assert.Equal(2, frontier.Progress[0].Found);
        Assert.Equal(3, frontier.Progress[0].Total);
        Assert.True(frontier.HasUnvisited);
    }

    [Fact]
    public void HintsComeFromTheDataAndNeverNameTheSubject()
    {
        FrontierResult frontier = CompendiumFrontier.Build(Data(), Knows("Wood"), SpoilerLevel.Hints);

        FrontierEntry greydwarf = frontier.Find("frontier:creature:Greydwarf")!;
        Assert.Equal("Black Forest · by night", greydwarf.Hints[0]);
        Assert.Equal("Health about 40 · drops 2 kinds of item, 0 you have", greydwarf.Hints[1]);
        Assert.Equal("item:TrophyGreydwarf", greydwarf.IconId);
        Assert.False(greydwarf.Day);
        Assert.True(greydwarf.Night);
        Assert.Equal("Dropped by Boar, which you've met", frontier.Find("frontier:item:LeatherScraps")!.Hints[0]);
        Assert.Equal("Black Forest · dropped by a creature you haven't met", frontier.Find("frontier:item:Resin")!.Hints[0]);
        Assert.Equal("That creature has health about 40", frontier.Find("frontier:item:Resin")!.Hints[1]);
        Assert.All(frontier.Entries, e => Assert.All(e.Hints, h => Assert.DoesNotContain(e.Name, h)));
    }

    [Fact]
    public void HiddenShowsNoSilhouettesAndLookAheadAddsTheNextLandWithoutHints()
    {
        Assert.Empty(CompendiumFrontier.Build(Data(), Knows("Wood"), SpoilerLevel.Hidden).Entries);

        FrontierResult ahead = CompendiumFrontier.Build(Data(), Knows("Wood"), SpoilerLevel.LookAhead);
        FrontierEntry blob = ahead.Find("frontier:creature:Blob")!;
        Assert.True(blob.Ahead);
        Assert.Equal(new[] { "In a land you haven't visited yet" }, blob.Hints);
        Assert.False(ahead.SecondHintOpen(blob, SecondHintRule.RightAway));
    }

    [Fact]
    public void TheSecondHintFollowsTheRule()
    {
        FrontierResult frontier = CompendiumFrontier.Build(Data(), Knows("Wood"), SpoilerLevel.Hints);
        FrontierEntry scraps = frontier.Find("frontier:item:LeatherScraps")!;
        FrontierEntry greydwarf = frontier.Find("frontier:creature:Greydwarf")!;

        Assert.True(frontier.SecondHintOpen(scraps, SecondHintRule.HalfFound));
        Assert.False(frontier.SecondHintOpen(greydwarf, SecondHintRule.HalfFound));
        Assert.True(frontier.SecondHintOpen(greydwarf, SecondHintRule.RightAway));
        Assert.False(frontier.SecondHintOpen(scraps, SecondHintRule.Never));
    }

    [Theory]
    [InlineData(41f, 40)]
    [InlineData(43f, 45)]
    [InlineData(2f, 5)]
    [InlineData(600f, 600)]
    [InlineData(1234f, 1230)]
    public void HealthIsRoundedForHints(float health, int expected) => Assert.Equal(expected, CompendiumFrontier.About(health));

    [Fact]
    public void ActionsChangeTheJournalAndRevealNeedsTwoClicks()
    {
        var journal = new CompendiumJournal(null);

        Assert.False(CompendiumJourney.Apply(journal, "act:reveal:frontier:item:Resin", out string? confirming));
        Assert.Equal("frontier:item:Resin", confirming);
        Assert.False(journal.IsRevealed("frontier:item:Resin"));
        Assert.True(CompendiumJourney.Apply(journal, "act:confirm:frontier:item:Resin", out _));
        Assert.True(journal.IsRevealed("frontier:item:Resin"));
        Assert.True(CompendiumJourney.Apply(journal, "act:next:frontier:item:Resin", out _));
        Assert.Equal(2, journal.ShownHints("frontier:item:Resin"));
        Assert.True(CompendiumJourney.Apply(journal, "act:track:frontier:item:Resin", out _));
        Assert.True(journal.IsTracked("frontier:item:Resin"));
        Assert.True(CompendiumJourney.Apply(journal, "act:spoil:0", out _));
        Assert.Equal(SpoilerLevel.Hidden, journal.Spoilers);
        Assert.False(CompendiumJourney.Apply(journal, "act:spoil:9", out _));
        Assert.True(CompendiumJourney.Apply(journal, "act:hint2:1", out _));
        Assert.Equal(SecondHintRule.RightAway, journal.SecondHint);
        Assert.True(CompendiumJourney.Apply(journal, "act:counts", out _));
        Assert.False(journal.ShowCounts);
        Assert.False(CompendiumJourney.Apply(journal, "act:highlight:Wood", out _));
        Assert.False(CompendiumJourney.Apply(journal, "item:Wood", out _));
    }

    [Fact]
    public void StockCountsWhatCanBeMade()
    {
        var stock = new CompendiumStock(inBase: true);
        stock.Carried["Wood"] = 5;
        stock.Base["Wood"] = 8;
        CompendiumData data = Data();

        Assert.Equal(2, stock.CanMake(data.Recipes[0]));
        Assert.Equal(26, stock.CanMake(data.Recipes[1]));
        Assert.Equal(0, CompendiumStock.None.CanMake(data.Recipes[0]));
    }

    [Fact]
    public void TheBestWeaponUsesTheCreaturesWeaknessAndIgnoresChop()
    {
        CompendiumData data = Data();
        CreatureRecord greydwarf = data.Creatures.Single(c => c.Id == "Greydwarf");

        IReadOnlyList<KeyValuePair<ItemRecord, float>> best = CompendiumJourney.BestWeapons(greydwarf, data, Knows("Club", "Bow", "Axe"));

        Assert.Equal(new[] { "Crude bow", "Club" }, best.Select(p => p.Key.Name));
        Assert.Equal(15f, best[0].Value);
        Assert.Equal(0.25f, CompendiumJourney.Multiplier(DamageResponse.VeryResistant));
    }

    [Fact]
    public void TheBookAddsHomeSettingsSilhouettesAndStock()
    {
        var journal = new CompendiumJournal(null);
        journal.Start(new[] { "item:Wood" });
        var stock = new CompendiumStock(inBase: true);
        stock.Base["Wood"] = 12;
        CompendiumData data = Data();

        CompendiumBook book = CompendiumJourney.Build(data, Knows("Wood", "Club", "Resin"), journal, stock, "frontier:creature:Greydwarf");

        CompendiumPage home = book.Find(CompendiumJourney.HomePage)!;
        Assert.Equal(new[] { "Your journey", "Recently discovered", "Ready to craft from this base", "Within reach" }, home.Sections.Select(s => s.Heading));
        Assert.Contains(home.Sections[0].Lines, l => l.Text == "Meadows: 2 of 3 found");
        Assert.Contains(home.Sections[1].Lines, l => l.Text == "Club (new)" && l.Link == "item:Club");
        Assert.DoesNotContain(home.Sections[1].Lines, l => l.Text.StartsWith("Wood"));
        Assert.Contains(home.Sections[2].Lines, l => l.Text == "Club: can make 2");

        CompendiumPage wood = book.Find("item:Wood")!;
        CompendiumSection stockSection = wood.Sections.Single(s => s.Heading == "In this base");
        Assert.Equal("12 in this base's chests · 0 carried", stockSection.Lines[0].Text);
        Assert.Equal("act:highlight:Wood", stockSection.Lines[1].Link);

        CompendiumPage greydwarf = book.Find("frontier:creature:Greydwarf")!;
        Assert.Equal(CompendiumKind.Frontier, greydwarf.Kind);
        Assert.Equal("Black Forest · by night", greydwarf.Title);
        Assert.Contains(greydwarf.Sections[1].Lines, l => l.Link == "act:confirm:frontier:creature:Greydwarf");
        Assert.Contains(greydwarf.Sections[1].Lines, l => l.Text.StartsWith("Hint 2 unlocks when you've found half of the Black Forest"));

        Assert.NotNull(book.Find(CompendiumJourney.SettingsPage));
        Assert.Equal(2, book.Search(CompendiumKind.Home, null).Count);
    }
}
