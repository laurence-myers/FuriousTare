using BepInEx.Configuration;
using FortressOccident;
using HarmonyLib;

namespace FuriousTareIL2CPP.Patches;

/**
 * The loading screen takes ~0.43 seconds to fade in and ~0.42 seconds to fade out, on every area transition.
 * We speed up the loading screen's tween.
 */
[HarmonyPatch(
    typeof(SceneTransitionManager),
    nameof(SceneTransitionManager.Start)
)]
public class TransitionFadeSpeed
{
    private static float _multiplier = 2f;

    public static void LoadConfig(ConfigFile configFile)
    {
        // A speed of 0 would pause the tween, and transitions would wait forever for it to finish
        _multiplier = configFile.Bind(
            ConfigSections.TransitionTweaks,
            "FadeSpeedMultiplier",
            _multiplier,
            new ConfigDescription(
                "Loading screen fade speed multiplier. Original value: 1",
                new AcceptableValueRange<float>(0.1f, 10f)
            )
        ).Value;
    }

    public static void Postfix(SceneTransitionManager __instance)
    {
        var tween = __instance.loadingScreen;
        if (tween == null)
        {
            Logger.Log.LogWarning(
                "Could not find the loading screen tween"
            );
            return;
        }

        var originalSpeed = tween.playSpeed;
        tween.playSpeed = originalSpeed * _multiplier;
        Logger.Log.LogInfo(
            $"Loading screen fade speed set from {originalSpeed} -> {tween.playSpeed}"
        );
    }
}
