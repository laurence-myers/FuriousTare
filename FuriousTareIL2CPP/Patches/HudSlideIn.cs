using System.Collections.Generic;
using System.Linq;
using FortressOccident;
using HarmonyLib;
using UnityEngine;

namespace FuriousTareIL2CPP.Patches;

/**
 * The background "tape" behind the bottom-right HUD buttons is supposed to slide up into place when the HUD is shown,
 * e.g. after a loading screen. Instead, it pops in fully formed, a moment after the buttons appear.
 *
 * The Feld UI has two animator controllers: one for ultrawide screens, and one for everything else. They are
 * identical, except that the ultrawide one uses "-uw" variants of the right panel's clips. But in the standard
 * controller, the "hud" layer's "in" state has no clip: the "Armature|hud-in" clip is only used by the ultrawide one.
 * The empty state holds the "off" pose until its exit time, then snaps to "on".
 *
 * We can't edit a controller's states at runtime, so we use the ultrawide controller instead, with its "-uw" clips
 * overridden by the standard ones. The result is the standard controller, with the "hud-in" clip restored.
 */
[HarmonyPatch(
    typeof(FeldController),
    nameof(FeldController.Awake)
)]
public class HudSlideIn
{
    private const string HudInClip = "Armature|hud-in";

    private static readonly (string ultrawide, string standard)[] ClipOverrides =
    {
        ("Armature|active-uw", "Armature|active"),
        ("Armature|right-in-uw", "Armature|right-in"),
        ("Armature|right-out-uw", "Armature|right-out"),
        ("Armature|dialog-to-right-uw", "Armature|dialog-to-right"),
        ("Armature|right-to-dialog-uw", "Armature|right-to-dialog"),
    };

    private static AnimatorOverrideController _controller;

    // Runs before Awake, which assigns feldAnimatorController to the Feld animator
    public static void Prefix(FeldController __instance)
    {
        var standard = __instance.feldAnimatorController;
        var ultrawide = __instance.feldAnimatorControllerUltrawide;
        if (standard == null || ultrawide == null)
        {
            Logger.Log.LogWarning(
                "Couldn't find the Feld animator controllers, can't restore the HUD slide-in animation"
            );
            return;
        }

        var standardClips = ClipsByName(standard);
        var ultrawideClips = ClipsByName(ultrawide);
        if (standardClips.ContainsKey(HudInClip))
        {
            Logger.Log.LogInfo(
                "The Feld animator controller already uses the HUD slide-in animation, no need to patch it"
            );
            return;
        }

        // Check that the controllers still differ only by the clips we know about. If a game update changes either
        // of them, the ultrawide controller may no longer be a good stand-in for the standard one.
        var overrides = ClipOverrides.ToDictionary(pair => pair.ultrawide, pair => pair.standard);
        var expectedClips = new HashSet<string>(standardClips.Keys) { HudInClip };
        var overriddenClips = new HashSet<string>(
            ultrawideClips.Keys.Select(name => overrides.TryGetValue(name, out var replacement) ? replacement : name)
        );
        if (!overriddenClips.SetEquals(expectedClips) || !overrides.Keys.All(ultrawideClips.ContainsKey))
        {
            Logger.Log.LogWarning(
                "The Feld animator controllers have changed, can't restore the HUD slide-in animation"
            );
            return;
        }

        if (_controller == null)
        {
            _controller = new AnimatorOverrideController(ultrawide)
            {
                name = $"{standard.name} (HUD slide-in restored)",
                // Only referenced from script fields when an ultrawide aspect is in use; keep it around regardless
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            foreach (var (ultrawideName, standardName) in ClipOverrides)
            {
                _controller[ultrawideClips[ultrawideName]] = standardClips[standardName];
            }
        }

        // Also used by UpdateAspectRatio, when switching back from an ultrawide aspect
        __instance.feldAnimatorController = _controller;

        // Patched!
        Logger.Log.LogInfo(
            "Restored the HUD background slide-in animation"
        );
    }

    private static Dictionary<string, AnimationClip> ClipsByName(RuntimeAnimatorController controller)
    {
        // A clip is listed once for each state that uses it
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var clip in controller.animationClips)
        {
            clips.TryAdd(clip.name, clip);
        }

        return clips;
    }
}
