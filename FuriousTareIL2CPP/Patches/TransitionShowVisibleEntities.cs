using System;
using System.Runtime.CompilerServices;
using FortressOccident;
using HarmonyLib;
using UnityEngine;

namespace FuriousTareIL2CPP.Patches;

/**
 * NPCUnloader shows and hides NPCs and entities depending on whether they're in view of the camera. It does this in
 * endless coroutines that are time-sliced: 30 NPC schedules and 10 entities per frame. After an area change, every
 * entity of the new area is registered, so a full pass over a big area takes dozens of frames. The original loading
 * screen stayed up long enough to hide this, but with faster transitions, characters visibly pop in.
 *
 * When the loading screen starts hiding, we run a full pass, using the game's own coroutines in "instant" mode (no
 * time-slicing). The game's time-sliced passes keep running as before.
 *
 * When loading a save game, the party and camera are placed in the same frame (in SceneTransitionManager.readyEvent),
 * and some camera state only updates in the next frame. So we hold the loading screen for one more frame, with an
 * AnimatorInitializer lock, and run a second pass. Newly shown NPCs also add AnimatorInitializer locks, so the loading
 * screen then waits for their animators to be ready.
 */
public class TransitionShowVisibleEntities
{
    // Each pass only does a visibility test per entity, but a pass can stop early (see RunFullPass), so cap the retries
    private const int MaxPasses = 100;

    private static NPCUnloader _npcUnloader;
    private static Il2CppSystem.Object _loadingScreenLock;
    private static int _firstPassFrame;

    [HarmonyPatch(
        typeof(NPCUnloader),
        nameof(NPCUnloader.Start)
    )]
    [HarmonyPostfix]
    public static void NPCUnloaderStartPostfix(NPCUnloader __instance)
    {
        _npcUnloader = __instance;
    }

    [HarmonyPatch(
        typeof(SceneTransitionManager._ShowLoadingScreenCoR_d__37),
        nameof(SceneTransitionManager._ShowLoadingScreenCoR_d__37.MoveNext)
    )]
    [HarmonyPrefix]
    public static void ShowLoadingScreenCoRPrefix(SceneTransitionManager._ShowLoadingScreenCoR_d__37 __instance)
    {
        if (__instance.show)
        {
            return;
        }

        if (__instance.__1__state == 0)
        {
            UpdateVisibleEntities();

            ReleaseLoadingScreen();
            _loadingScreenLock = new Il2CppSystem.Object();
            AnimatorInitializer.AddAnimatorLock(_loadingScreenLock);
            _firstPassFrame = Time.frameCount;
        }
        else if (_loadingScreenLock != null && Time.frameCount > _firstPassFrame)
        {
            UpdateVisibleEntities();
            ReleaseLoadingScreen();
        }
    }

    [HarmonyPatch(
        typeof(SceneTransitionManager._ShowLoadingScreenCoR_d__37),
        nameof(SceneTransitionManager._ShowLoadingScreenCoR_d__37.MoveNext)
    )]
    [HarmonyPostfix]
    public static void ShowLoadingScreenCoRPostfix(
        SceneTransitionManager._ShowLoadingScreenCoR_d__37 __instance,
        bool __result
    )
    {
        // E.g. the loading screen wasn't shown, so the coroutine ended before waiting for animators
        if (!__result && !__instance.show)
        {
            ReleaseLoadingScreen();
        }
    }

    private static void ReleaseLoadingScreen()
    {
        if (_loadingScreenLock == null)
        {
            return;
        }

        AnimatorInitializer.RemoveAnimatorLock(_loadingScreenLock);
        _loadingScreenLock = null;
    }

    // Not inlined, so TransitionTimings can measure it
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void UpdateVisibleEntities()
    {
        if (_npcUnloader == null)
        {
            return;
        }

        try
        {
            // CameraUtils only recalculates the visible area when the main camera moves, not when it zooms
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.transform.hasChanged = true;
            }

            RunFullPass(
                _npcUnloader.ProcessCharacterScheduleRegistry(true),
                () => NPCUnloader.characterScheduleRegistry.Count
            );
            RunFullPass(
                _npcUnloader.ProcessBasicEntityRegistry(true),
                () => NPCUnloader.basicEntityRegistry.Count
            );
        }
        catch (Exception e)
        {
            Logger.Log.LogWarning(
                $"Could not update visible entities: {e}"
            );
        }
    }

    /**
     * The coroutine is an endless loop that starts with "yield return null". The first MoveNext() stops there, and
     * every following MoveNext() does a pass over the registry, then stops at the start of the next loop.
     *
     * A pass stops early when the registry changes size: ProcessBasicEntityRegistry removes one destroyed entity per
     * pass, then stops. So we repeat until a pass leaves the registry size unchanged, meaning it went through every
     * entry.
     */
    private static void RunFullPass(Il2CppSystem.Collections.IEnumerator coroutine, Func<int> getRegistryCount)
    {
        coroutine.MoveNext();
        for (var i = 0; i < MaxPasses; i++)
        {
            var countBefore = getRegistryCount();
            coroutine.MoveNext();
            if (getRegistryCount() == countBefore)
            {
                return;
            }
        }

        Logger.Log.LogWarning(
            $"Visible entities pass did not complete after {MaxPasses} attempts"
        );
    }
}
