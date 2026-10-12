using HarmonyLib;
using Sunshine.Dialogue;

namespace FuriousTareIL2CPP.Patches;

/**
 * The Franconigerian Cavalry Boots are supposed to count towards the Royalist reenactor dress code, but the item ID ("shoes_cavalry") doesn't match the ID the dress code condition checks for ("boots_cavalry").
 *
 * No item with ID "boots_cavalry" exists in game, so any check for it always fails. This remaps checks for the wrong ID to the real item ID.
 */

[HarmonyPatch(typeof(InventoryLuaFunctions))]
public class FranconigerianCavalryBoots
{
    private const string WrongItemId = "boots_cavalry";
    private const string CorrectItemId = "shoes_cavalry";

    [HarmonyPrefix]
    [HarmonyPatch(
        nameof(InventoryLuaFunctions.CheckItem)
    )]
    public static void CheckItemHook(ref bool __runOriginal, ref bool __result, string itemName)
    {
        if (itemName != WrongItemId)
        {
            return;
        }

        __result = InventoryLuaFunctions.CheckItem(CorrectItemId);
        __runOriginal = false;
        Logger.Log.LogInfo(
            $"Remapped CheckItem(\"{WrongItemId}\") to \"{CorrectItemId}\". Result: {__result}"
        );
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        nameof(InventoryLuaFunctions.CheckEquipped)
    )]
    public static void CheckEquippedHook(ref bool __runOriginal, ref bool __result, string itemName)
    {
        if (itemName != WrongItemId)
        {
            return;
        }

        __result = InventoryLuaFunctions.CheckEquipped(CorrectItemId);
        __runOriginal = false;
        Logger.Log.LogInfo(
            $"Remapped CheckEquipped(\"{WrongItemId}\") to \"{CorrectItemId}\". Result: {__result}"
        );
    }
}