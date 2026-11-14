namespace BaseStorage.Plugin.CharacterMenu;

using BaseStorage.Plugin.Patching;
using BaseStorage.Shared;
using HarmonyLib;

/// <summary>
/// Patch on Inventory.CanAddItem: only items inside the grid use its space. Vanilla counts every item, so worn gear in the hidden row made
/// the last free grid spaces look full to pickup, crafting and fishing. The prefab overload calls this one, so both are covered. A failure
/// leaves the vanilla answer.
/// </summary>
[HarmonyPatch(typeof(Inventory), PatchTargetNames.Inventory.CanAddItem, typeof(ItemDrop.ItemData), typeof(int))]
internal static class InventoryCanAddItemPatch
{
    /// <summary>The patch key used by the fail-closed wrapper to report a failure once per session.</summary>
    internal const string PatchKey = "BaseStorage.Plugin.CharacterMenu.InventoryCanAddItemPatch.Postfix";

    private static void Postfix(Inventory __instance, ItemDrop.ItemData item, int stack, ref bool __result)
    {
        // Vanilla never over-counts free space, so a yes stands; only a no is worth a second look.
        if (__result)
        {
            return;
        }

        __result = PatchSafety.Run(PatchKey, __result, _ => WornItems.CanAddItem(__instance, item, stack));
    }
}
