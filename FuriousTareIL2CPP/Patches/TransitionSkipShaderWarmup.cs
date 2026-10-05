using HarmonyLib;

namespace FuriousTareIL2CPP.Patches;

/**
 * ShaderWarmupper calls Shader.WarmupAllShaders() at the end of every area transition, because it registers itself to
 * SceneTransitionManager.readyEvent and never unregisters. That's 0.2 - 1 seconds per transition, plus several more
 * seconds of render thread work in the next frames.
 *
 * All scenes are preloaded at startup (FastLoadManager), so every shader is already in memory for the first call, when
 * entering the main menu. Later calls warm up the same shaders again, so we skip them.
 *
 * The first call is handled by StartupSkipShaderWarmup.
 */
[HarmonyPatch(
    typeof(ShaderWarmupper),
    nameof(ShaderWarmupper.Warmup)
)]
public class TransitionSkipShaderWarmup
{
    private static bool _isFirstCall = true;

    // For the timing diagnostics
    public static bool SkippedLastCall { get; private set; }

    public static void Prefix(ref bool __runOriginal)
    {
        SkippedLastCall = !_isFirstCall;
        if (_isFirstCall)
        {
            _isFirstCall = false;
            return;
        }

        Logger.Log.LogDebug(
            "Skipping repeated shader warmup"
        );
        __runOriginal = false;
    }
}
