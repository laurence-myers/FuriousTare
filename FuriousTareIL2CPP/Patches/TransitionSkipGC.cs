using FortressOccident;
using HarmonyLib;

namespace FuriousTareIL2CPP.Patches;

/**
 * At the very end of an area transition, after the loading screen has faded out, SceneTransitionManager.LoadSceneCoR
 * calls GC.Collect(). That takes 0.3 - 0.4 seconds, right as you regain control.
 *
 * We skip GC.Collect() calls made while LoadSceneCoR is running. Other forced collections (e.g. when loading or saving
 * the game) are untouched, and the garbage collector still runs by itself when it needs to.
 */
public class TransitionSkipGC
{
    private static bool _isInLoadSceneCoR;

    // For the timing diagnostics
    public static bool SkippedLastCall { get; private set; }

    [HarmonyPatch(
        typeof(SceneTransitionManager._LoadSceneCoR_d__36),
        nameof(SceneTransitionManager._LoadSceneCoR_d__36.MoveNext)
    )]
    [HarmonyPrefix]
    public static void LoadSceneCoRPrefix(out bool __state)
    {
        // LoadSceneCoR's first step can run inside another coroutine, so restore the previous value afterwards
        __state = _isInLoadSceneCoR;
        _isInLoadSceneCoR = true;
    }

    [HarmonyPatch(
        typeof(SceneTransitionManager._LoadSceneCoR_d__36),
        nameof(SceneTransitionManager._LoadSceneCoR_d__36.MoveNext)
    )]
    [HarmonyPostfix]
    public static void LoadSceneCoRPostfix(bool __state)
    {
        _isInLoadSceneCoR = __state;
    }

    [HarmonyPatch(
        typeof(Il2CppSystem.GC),
        nameof(Il2CppSystem.GC.Collect),
        new System.Type[0]
    )]
    [HarmonyPrefix]
    public static void GcCollectPrefix(ref bool __runOriginal)
    {
        SkippedLastCall = _isInLoadSceneCoR;
        if (!_isInLoadSceneCoR)
        {
            return;
        }

        Logger.Log.LogDebug(
            "Skipping GC.Collect() at the end of the area transition"
        );
        __runOriginal = false;
    }
}
