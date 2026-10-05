using FortressOccident;
using HarmonyLib;
using UnityEngine;

namespace FuriousTareIL2CPP.Patches;

/**
 * The time HUD element has a Y that's too big, and overlaps the left/right hand icons.
 * We can fix the hand HUD by shrinking the clock.
 */
[HarmonyPatch(
    typeof(FeldController),
    nameof(FeldController.Start)
)]
public class HandHud
{
    public static void Postfix()
    {
        var gameObject = GameObject.Find("ClockText");
        if (gameObject == null)
        {
            Logger.Log.LogWarning("Couldn't find ClockText, can't patch it");
            return;
        }
        var component = gameObject.GetComponent<RectTransform>();

        // Change sizeDelta.y from 130 -> 41
        component.sizeDelta = new Vector2(
            component.sizeDelta.x,
            41
        );

        Logger.Log.LogInfo("Fixed hand HUD by shrinking the clock");
    }
}
