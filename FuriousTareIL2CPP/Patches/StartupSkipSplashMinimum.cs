using HarmonyLib;

namespace FuriousTareIL2CPP.Patches;

/**
 * The first scene (PreInitialize) reads the settings and the save game list, then waits until at least 5 seconds have
 * passed (PreInitialize.SplashMaxTime) before loading the next scene. Reading the data only takes ~0.5 seconds.
 *
 * We start the timer at 5 seconds, so the game moves on as soon as the data is read.
 */
[HarmonyPatch(
    typeof(PreInitialize),
    nameof(PreInitialize.Start)
)]
public class StartupSkipSplashMinimum
{
    private const float SplashMaxTime = 5f;

    public static void Postfix(PreInitialize __instance)
    {
        __instance.timeCounter = SplashMaxTime;
        Logger.Log.LogInfo(
            "Skipping the minimum splash screen time"
        );
    }
}
