using BepInEx.Configuration;
using HarmonyLib;

namespace FuriousTareIL2CPP.Patches;

/**
 * Every area transition calls AmplifyTextureManager.Warmup(0.5f), which streams virtual texture pages in a blocking loop
 * until there is nothing left to stream, or the time budget runs out. In Martinaise it always uses the full 0.5 seconds.
 *
 * Texture pages keep streaming in the background after the warmup, and the loading screen takes another ~0.4 seconds
 * to fade out, so a smaller budget mostly goes unnoticed. If it is too small, textures may look blurry for a moment
 * after the fade.
 */
[HarmonyPatch(
    typeof(AmplifyTextureManager),
    nameof(AmplifyTextureManager.Warmup)
)]
public class TransitionTextureWarmupBudget
{
    private const float OriginalSeconds = 0.5f;
    private static float _seconds = 0.1f;

    public static void LoadConfig(ConfigFile configFile)
    {
        _seconds = configFile.Bind(
            ConfigSections.TransitionTweaks,
            "TextureWarmupSeconds",
            _seconds,
            $"Maximum time to spend streaming textures when entering an area. Original value: {OriginalSeconds}"
        ).Value;
    }

    public static void Prefix(ref float __0)
    {
        __0 = _seconds;
    }
}
