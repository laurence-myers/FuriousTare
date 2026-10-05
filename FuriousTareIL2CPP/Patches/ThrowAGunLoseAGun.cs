using HarmonyLib;
using Sunshine.Dialogue;

namespace FuriousTareIL2CPP.Patches;

[HarmonyPatch(
    typeof(InventoryLuaFunctions),
    nameof(InventoryLuaFunctions.LoseGun)
)]
public class ThrowAGunLoseAGun
{

    // Original code would incorrectly throw ruby's gun if you had the Villiers gun
    public static void Prefix(ref bool __runOriginal)
    {
        if (InventoryLuaFunctions.CheckItem("gun_ruby"))
        {
            InventoryLuaFunctions.LoseItem("gun_ruby");
        }
        else if (InventoryLuaFunctions.CheckItem("gun_villiers"))
        {
            InventoryLuaFunctions.LoseItem("gun_villiers");
        }

        __runOriginal = false;
    }
}
