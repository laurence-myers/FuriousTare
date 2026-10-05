using HarmonyLib;

namespace FuriousTareIL2CPP.Patches;

/**
 * When entering the main menu, ShaderWarmupper calls Shader.WarmupAllShaders(). That compiles every variant of every
 * loaded shader (~18000), taking 5 - 25 seconds, partly in the frames after the call.
 *
 * We skip it. Shaders are then compiled the first time they are drawn, and only the variants that are actually used.
 * The graphics driver caches compiled shaders between launches.
 *
 * Later calls, on each area transition, are handled by TransitionSkipShaderWarmup.
 */
[HarmonyPatch(
    typeof(ShaderWarmupper),
    nameof(ShaderWarmupper.Warmup)
)]
public class StartupSkipShaderWarmup
{
    private static bool _isFirstCall = true;

    // For the timing diagnostics
    public static bool SkippedLastCall { get; private set; }

    public static void Prefix(ref bool __runOriginal)
    {
        SkippedLastCall = _isFirstCall;
        if (!_isFirstCall)
        {
            return;
        }

        _isFirstCall = false;
        Logger.Log.LogInfo(
            "Skipping the startup shader warmup"
        );
        __runOriginal = false;
    }
}
