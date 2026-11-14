# Don's QOL spec: compendium

Status: built on `feat/character-menu`; the Core rules are tested, and the game-side code has not yet run in the game. Run section 6 first.

## 1. What the player gets

A spoiler-free, searchable, cross-linked wiki inside the inventory screen, opened with the **Compendium** button in the character menu. Tabs: Items, Creatures, Biomes and Building. Search matches title, group and description, and needs every word.

- **Item:** stats, recipes, what it's used in, what drops it and where it's gathered.
- **Creature:** boss flag, base health, strongest attack, damage responses, drops and the biomes it spawns in, by day or night.
- **Biome:** a short description, creatures and resources.
- **Building piece:** comfort, station needed and cost.

## 2. Spoilers

A page exists only once the player has discovered its subject, so it can't be searched for or linked to before then.

| Subject | Discovered when |
|---|---|
| Item | It's in the game's known materials or known recipes, so it's been picked up or can be crafted |
| Building piece | It's in the game's known recipes, so it shows in the build menu |
| Station | It's in the game's known stations |
| Biome | It's in the game's known biomes, or the mod saw the player there |
| Creature | The mod saw it within 30 metres of the player, or the player knows its trophy |

The game keeps no list of creatures met, so the mod saves its own on the character under `basestorage.seen`. Its first sightings start when the mod is first running; the trophy rule covers older characters.

Pages name nothing undiscovered. Lists such as **Used in**, **Dropped by**, **Drops**, **Appears in**, **Gathered from** and a biome's creatures and resources just leave undiscovered things out. A recipe or a piece's cost must stay complete to be useful, so an undiscovered ingredient reads `Undiscovered item x10` with no link, and an undiscovered station reads `an undiscovered station`. Sources appear only with biomes the player has visited.

Places are left out, because the game doesn't record which ones a player has visited. Biome descriptions describe only the land.

## 3. Where the facts come from

Facts are read at runtime from `ObjectDB`, `ZNetScene`, `SpawnSystem` and `ZoneSystem`, never written by hand or copied from a website, so they follow game updates and include other mods' additions. The only hand-written facts are the one-line biome descriptions in the strings table. Item, creature and piece descriptions are the game's own text, in the player's language; the mod's labels are in English.

Core turns plain records into pages (`CompendiumBuilder`) and searches them (`CompendiumBook`). A link is made only when its target page exists. Plugin only reads the game (`GameCompendiumSource`) and draws (`CompendiumWindow`).

The game's data is read once per world. The book is rebuilt from it on every open, with what the player knows then.

## 4. The window

The window is a child of the inventory screen, so the game already blocks movement and shows the cursor while it's open. Closing the inventory closes it. While the search box has focus, the inventory can't close, the same as the base menu's search. The character panel and its 3D view stop while the window is open.

The list reuses a screen's worth of rows while scrolling, so thousands of entries cost no more than a few dozen.

## 5. Known limits

- A creature within 30 metres counts as seen even behind a wall or underground, which can reveal it a little early.
- A page shows a creature's full stats and weaknesses from the first sighting, and an item's whole recipe list once the item is known. Only things undiscovered are hidden, not details of things found.
- Knowledge belongs to the character, as the game's does. A new character starts with an empty compendium.
- Loot in dungeon chests isn't listed: place contents load only when the world builds them, so they can't be read here.
- A gathering source's name comes from its prefab name, so some read roughly. A source the world places only inside locations isn't listed, because it has no biome to check.
- Creature figures are base values, before star levels and world modifiers.
- It needs a pointer, and it can't open while a chest is open, because the button is in the character menu.
- The mod's own labels and biome descriptions are English.

## 6. Unverified until playtested

- D-1. Every spawn list is reachable through the `SpawnSystem` prefab or a loaded instance, so creatures show their biomes.
- D-2. Vegetation prefabs lead to their drops, so biomes list their resources.
- D-3. The first open is quick enough to feel instant.
- D-4. Rows, scrolling, search typing and links work and look right.
- D-5. An existing character's known items, pieces, stations and biomes all show on the first open.
- D-6. Sightings are saved with the character and survive a reload.

Playtest. Agents do not launch the game or read saves.

1. On a brand-new character, open the inventory and press **Compendium**. Expected: it opens within a moment, showing only what you've met so far, with the Meadows the only biome. Search `iron` and `surtling`. Expected: nothing. (D-3)
2. Pick up a new item, close the compendium and reopen it. Expected: the item now has a page.
3. Stand near a creature for a couple of seconds, then reopen. Expected: it's under Creatures. Save, quit, reload and reopen. Expected: it's still there. (D-6)
4. Open a recipe with an ingredient you haven't found. Expected: `Undiscovered item x...` with no link, and no name anywhere.
5. Open a late-game character. Expected: its known items, pieces and biomes are all there, including creatures whose trophies it holds. (D-5)
6. Scroll the whole item list. Expected: smooth, with no gaps or repeated rows. (D-4)
7. Open Wood. Expected: stats, and **Used in** and **Gathered from** sections. Click a linked item, then **Back**. (D-2, D-4)
8. Search `bronze` on a character who knows it, typing letters such as `e` and `i`. Expected: the list narrows, and the inventory stays open. (D-4)
9. Open a creature you've met. Expected: health, drops you've found, damage taken and biomes you've visited. Open a biome. Expected: only creatures and resources you know. (D-1, D-2)
10. Open Building, then the workbench. Expected: its cost, with links.
11. Press **Close**. Expected: the inventory and character panel return.
12. Leave to the main menu, join another world and open it. Expected: it works, with the same character's knowledge.
13. With another item mod installed, find one of its items in play. Expected: it gets a page.
14. Check the BepInEx log and frame rate after a few minutes. Expected: no errors from the mod and no stutter once a second. (D-3)

## 7. Journey, silhouettes and hints (0.5.3)

Built from the design canvas "Don's QOL Compendium Designs". Core: `CompendiumJournal`, `CompendiumFrontier`, `CompendiumJourney`. Plugin: `CompendiumJournalStore`, `CompendiumTracker`, and the window's new tabs.

- **Tabs:** Home, Items, Creatures, Biomes, Building, Not yet found, Options, Back. The window opens on Home.
- **Home:** found counts for each visited land; recently discovered pages (new until opened); what the base the player stands in can make; three hints from Not yet found.
- **Item pages** gain "In this base": the count in the base's chests and carried, a link that highlights the chests holding it (the inventory closes so the glow shows), and how many can be made from carried plus base stock.
- **Creature pages** gain "Your best answer": the found weapons ranked by damage against it, by the game's damage multipliers. Chop and pickaxe damage are ignored. Creatures now show their trophy's icon.
- **Not yet found:** for each visited land, the creatures that spawn there (not bosses), the items gathered there and their creatures' drops that the player hasn't found. Each shows its icon in black with an outline, and up to three hints:
  1. Free: land and time of day, or who drops it.
  2. Size, health and drops, or where it's gathered. It opens per the player's rule: when half the land is found (default), right away, or never.
  3. The name, after two clicks.
  Hints come only from game data, so other mods' content gets them too. Look ahead adds the next land's silhouettes without hints.
- **Tracking:** a tracked hint shows below the minimap. It is marked when the player is in its land at the right time of day, with one notice on arrival. Finding it shows "Found: name" and stops tracking.
- **Options:** spoiler level (Hidden, Hints, Look ahead), second-hint rule, found counts, on-screen list, arrival notice. Saved per character in `basestorage.journal`, never host-synced.
- **Choices against section 2:** silhouettes deliberately show that undiscovered things exist, only in visited lands, and Hidden turns them off. The first open marks everything already known as read, so only later finds count as new.
- **Not built from the design:** pinned recipes and shopping list, search filter chips, sighting counts and first-seen day, the reveal animation, and HUD position choice.

Playtest (J-1 to J-6):

15. Open the compendium. Expected: Home, with counts per visited land and no names of unvisited lands. (J-1)
16. Pick up something new, reopen. Expected: it's under Recently discovered until you open its page.
17. Open Not yet found and pick an entry. Expected: a black icon with a blue outline, hint 1, and Reveal. Click Reveal twice. Expected: the name, and the icon in colour. (J-2)
18. Track a hint, close the inventory. Expected: it's listed below the minimap; in the right land at the right time it's marked and a notice shows once. Find it. Expected: "Found: ..." and it leaves the list. (J-3)
19. In a base, open an item you store. Expected: base and carried counts; Show the chests closes the inventory and the chests glow. (J-4)
20. Open a creature with a weakness. Expected: Your best answer lists your weapons best first. (J-5)
21. Options: set Hidden. Expected: Not yet found is empty and Home has no Within reach. Reload the character. Expected: the choice is kept. (J-6)
