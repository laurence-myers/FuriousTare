using HarmonyLib;

namespace FuriousTareIL2CPP.Patches;

/**
 * The main menu shows an ad banner for "Zero Parades" which opens a store page when clicked.
 *
 * This hides it by deactivating the ad button's GameObject as soon as it wakes up and prevents anything else from re-enabling it.
 */

[HarmonyPatch(typeof(MainMenuAdButton))]
public class RemoveMainMenuAd
{
    [HarmonyPostfix]
    [HarmonyPatch(
        nameof(MainMenuAdButton.Awake)
    )]
    public static void AwakeHook(MainMenuAdButton __instance)
    {
        Logger.Log.LogInfo(
            $"Hiding main menu ad: \"{FuriousTareUtils.GetFullPath(__instance.gameObject)}\""
        );
        __instance.gameObject.SetActive(false);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        nameof(MainMenuAdButton.Enable)
    )]
    public static void EnableHook(ref bool __runOriginal)
    {
        __runOriginal = false;
    }
}