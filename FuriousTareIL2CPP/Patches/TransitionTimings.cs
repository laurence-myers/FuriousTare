using FortressOccident;
using HarmonyLib;
using UnityEngine.SceneManagement;
using Voidforge;

namespace FuriousTareIL2CPP.Patches;

/**
 * Diagnostic: measures where the time goes when changing areas, and logs a timeline once the transition finishes.
 *
 * All scenes are preloaded at startup (FastLoadManager), so a transition doesn't load anything from disk. It runs
 * these coroutines:
 * - TransitionEntity.Activate: the door. Shows the loading screen, then calls ApplicationManager.ChangeArea.
 * - SceneTransitionManager.LoadSceneCoR: swaps the active scene, places the party, warms up textures/shaders, etc.
 * - SceneTransitionManager.ShowLoadingScreenCoR: fades the loading screen in/out, and waits on a few counters.
 *
 * State numbers come from the compiler-generated state machines, as seen in the Mono build of the game
 * (GOG 2023-03-16). We also time some methods that run inside those steps, to break the steps down further.
 */
public class TransitionTimings
{
    private static readonly Timeline Timeline = new();
    private static bool _loadSceneStarted;

    #region Coroutine labels

    private static string ActivateResume(int state) => state switch
    {
        0 => "door used: fade out spatial audio, show loading screen",
        1 => "ApplicationManager.ChangeArea (LoadSceneCoR's first step runs inline)",
        _ => $"state {state}",
    };

    private static string ActivateYield(int state) => state switch
    {
        1 => "ShowLoadingScreenCoR(show)",
        _ => Timeline.YieldOrEnd(state),
    };

    internal static string LoadSceneResume(int state) => state switch
    {
        0 => "deactivate old scene, activate new scene, swap navmesh, notReadyEvent",
        1 => "async scene load (Lobby)",
        2 => "music spatial blend",
        3 => "GameController.OnAreaNotReady, show loading screen (if requested)",
        4 => "(loading screen shown)",
        5 => "OnAreaChangeBeforePlacingCharacter, TravelDestination.ArriveAt",
        6 => "TransitionProcessScene (animator culling), re-enable NavMeshAgents, AmplifyTexture.SetActiveCollection",
        7 => "AmplifyTextureManager.Warmup",
        8 or 9 => "loadingScreenDelays, music, readyEvent, start hiding loading screen",
        10 => "music fade, OnAreaChanged, GC.Collect",
        _ => $"state {state}",
    };

    internal static string LoadSceneYield(int state) => state switch
    {
        1 => "async scene load",
        4 => "ShowLoadingScreenCoR(show)",
        9 => "loadingScreenDelays",
        10 => "ShowLoadingScreenCoR(hide)",
        _ => Timeline.YieldOrEnd(state),
    };

    internal static string ShowLoadingScreenResume(int state) => state switch
    {
        0 => "start",
        1 => "(fade-in finished)",
        2 => "(done)",
        3 => "check TequilaClothing",
        4 => "check ContainerSource item icons",
        5 => "check AnimatorInitializer, OnTransitionFinish",
        6 => "start fade-out",
        7 => "(fade-out finished) deactivate loading screen",
        _ => $"state {state}",
    };

    internal static string ShowLoadingScreenYield(int state) => state switch
    {
        1 => "fade-in tween",
        3 => "TequilaClothing.ClothesOnTequillaToLoad",
        4 => "ContainerSource.LoadedContainersSourcesToLoad (item icons)",
        5 => "AnimatorInitializer.IsAnimatorsWorking",
        7 => "fade-out tween",
        _ => Timeline.YieldOrEnd(state),
    };

    #endregion

    private static void BeginSession(string title)
    {
        if (Timeline.IsActive)
        {
            if (!_loadSceneStarted)
            {
                Timeline.AppendTitle($" / {title}");
                return;
            }

            // A previous transition never finished, e.g. the coroutine was stopped. Flush what we have.
            Timeline.End("interrupted");
        }

        Timeline.Begin($"Area transition: {title}");
        _loadSceneStarted = false;
    }

    #region Coroutines

    [HarmonyPatch(
        typeof(TransitionEntity._Activate_d__6),
        nameof(TransitionEntity._Activate_d__6.MoveNext)
    )]
    [HarmonyPrefix]
    public static void ActivatePrefix(TransitionEntity._Activate_d__6 __instance)
    {
        var state = __instance.__1__state;
        if (state == 0)
        {
            BeginSession($"TransitionEntity.Activate(\"{__instance.__4__this.area}\")");
        }

        Timeline.StepPrefix(
            __instance.Pointer,
            state,
            () => "Activate",
            ActivateResume,
            ActivateYield
        );
    }

    [HarmonyPatch(
        typeof(TransitionEntity._Activate_d__6),
        nameof(TransitionEntity._Activate_d__6.MoveNext)
    )]
    [HarmonyPostfix]
    public static void ActivatePostfix(TransitionEntity._Activate_d__6 __instance, bool __result)
    {
        Timeline.StepPostfix(
            __instance.Pointer,
            __result,
            __instance.__1__state
        );

        // E.g. Kim interjected with dialogue instead of changing area
        if (!__result && Timeline.IsActive && !_loadSceneStarted)
        {
            Timeline.Discard();
        }
    }

    [HarmonyPatch(
        typeof(SceneTransitionManager._LoadSceneCoR_d__36),
        nameof(SceneTransitionManager._LoadSceneCoR_d__36.MoveNext)
    )]
    [HarmonyPrefix]
    public static void LoadSceneCoRPrefix(SceneTransitionManager._LoadSceneCoR_d__36 __instance)
    {
        var state = __instance.__1__state;
        if (state == 0)
        {
            BeginSession($"LoadSceneCoR(\"{__instance.sceneName}\")");
            _loadSceneStarted = true;
        }

        Timeline.StepPrefix(
            __instance.Pointer,
            state,
            () => "LoadSceneCoR",
            LoadSceneResume,
            LoadSceneYield
        );
    }

    [HarmonyPatch(
        typeof(SceneTransitionManager._LoadSceneCoR_d__36),
        nameof(SceneTransitionManager._LoadSceneCoR_d__36.MoveNext)
    )]
    [HarmonyPostfix]
    public static void LoadSceneCoRPostfix(SceneTransitionManager._LoadSceneCoR_d__36 __instance, bool __result)
    {
        Timeline.StepPostfix(
            __instance.Pointer,
            __result,
            __instance.__1__state
        );
        if (!__result)
        {
            Timeline.End("finished");
        }
    }

    [HarmonyPatch(
        typeof(SceneTransitionManager._ShowLoadingScreenCoR_d__37),
        nameof(SceneTransitionManager._ShowLoadingScreenCoR_d__37.MoveNext)
    )]
    [HarmonyPrefix]
    public static void ShowLoadingScreenCoRPrefix(SceneTransitionManager._ShowLoadingScreenCoR_d__37 __instance)
    {
        Timeline.StepPrefix(
            __instance.Pointer,
            __instance.__1__state,
            () => __instance.show ? "ShowLoadingScreenCoR(show)" : "ShowLoadingScreenCoR(hide)",
            ShowLoadingScreenResume,
            ShowLoadingScreenYield
        );
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
        Timeline.StepPostfix(
            __instance.Pointer,
            __result,
            __instance.__1__state
        );
    }

    #endregion

    #region Methods

    [HarmonyPatch(
        typeof(SceneTransitionManager),
        nameof(SceneTransitionManager.SetActive)
    )]
    [HarmonyPrefix]
    public static void SetActivePrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(SceneTransitionManager),
        nameof(SceneTransitionManager.SetActive)
    )]
    [HarmonyPostfix]
    public static void SetActivePostfix(Timeline.HookToken __state, Scene __0, bool __1) =>
        Timeline.HookEnd(__state, $"SceneTransitionManager.SetActive(\"{__0.name}\", {__1})");

    [HarmonyPatch(
        typeof(SceneLoadingManager),
        nameof(SceneLoadingManager.SetObjects)
    )]
    [HarmonyPrefix]
    public static void SetObjectsPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(SceneLoadingManager),
        nameof(SceneLoadingManager.SetObjects)
    )]
    [HarmonyPostfix]
    public static void SetObjectsPostfix(Timeline.HookToken __state, SceneLoadingManager __instance) =>
        Timeline.HookEnd(__state, $"SceneLoadingManager.SetObjects ({__instance.ObjectList.Count} objects)");

    [HarmonyPatch(
        typeof(SceneLoadingManager),
        nameof(SceneLoadingManager.DisableAll)
    )]
    [HarmonyPrefix]
    public static void DisableAllPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(SceneLoadingManager),
        nameof(SceneLoadingManager.DisableAll)
    )]
    [HarmonyPostfix]
    public static void DisableAllPostfix(Timeline.HookToken __state, SceneLoadingManager __instance) =>
        Timeline.HookEnd(__state, $"SceneLoadingManager.DisableAll ({__instance.ObjectList.Count} objects)");

    [HarmonyPatch(
        typeof(SceneTransitionManager),
        nameof(SceneTransitionManager.TransitionProcessScene)
    )]
    [HarmonyPrefix]
    public static void TransitionProcessScenePrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(SceneTransitionManager),
        nameof(SceneTransitionManager.TransitionProcessScene)
    )]
    [HarmonyPostfix]
    public static void TransitionProcessScenePostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "SceneTransitionManager.TransitionProcessScene");

    [HarmonyPatch(
        typeof(TravelDestination),
        nameof(TravelDestination.ArriveAt),
        typeof(string)
    )]
    [HarmonyPrefix]
    public static void ArriveAtPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(TravelDestination),
        nameof(TravelDestination.ArriveAt),
        typeof(string)
    )]
    [HarmonyPostfix]
    public static void ArriveAtPostfix(Timeline.HookToken __state, string __0) =>
        Timeline.HookEnd(__state, $"TravelDestination.ArriveAt(\"{__0}\")");

    [HarmonyPatch(
        typeof(AmplifyTextureManager),
        nameof(AmplifyTextureManager.SetActiveCollection)
    )]
    [HarmonyPrefix]
    public static void SetActiveCollectionPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(AmplifyTextureManager),
        nameof(AmplifyTextureManager.SetActiveCollection)
    )]
    [HarmonyPostfix]
    public static void SetActiveCollectionPostfix(Timeline.HookToken __state, string __0) =>
        Timeline.HookEnd(__state, $"AmplifyTextureManager.SetActiveCollection(\"{__0}\")");

    [HarmonyPatch(
        typeof(AmplifyTextureManager),
        nameof(AmplifyTextureManager.Warmup)
    )]
    [HarmonyPrefix]
    public static void AmplifyWarmupPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(AmplifyTextureManager),
        nameof(AmplifyTextureManager.Warmup)
    )]
    [HarmonyPostfix]
    public static void AmplifyWarmupPostfix(Timeline.HookToken __state, float __0) =>
        Timeline.HookEnd(__state, $"AmplifyTextureManager.Warmup({__0})");

    [HarmonyPatch(
        typeof(ShaderWarmupper),
        nameof(ShaderWarmupper.Warmup)
    )]
    [HarmonyPrefix]
    public static void ShaderWarmupPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(ShaderWarmupper),
        nameof(ShaderWarmupper.Warmup)
    )]
    [HarmonyPostfix]
    public static void ShaderWarmupPostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(
            __state,
            "ShaderWarmupper.Warmup (Shader.WarmupAllShaders)" + (StartupSkipShaderWarmup.SkippedLastCall || TransitionSkipShaderWarmup.SkippedLastCall ? " (skipped)" : "")
        );

    [HarmonyPatch(
        typeof(TransitionShowVisibleEntities),
        nameof(TransitionShowVisibleEntities.UpdateVisibleEntities)
    )]
    [HarmonyPrefix]
    public static void UpdateVisibleEntitiesPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(TransitionShowVisibleEntities),
        nameof(TransitionShowVisibleEntities.UpdateVisibleEntities)
    )]
    [HarmonyPostfix]
    public static void UpdateVisibleEntitiesPostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(
            __state,
            $"TransitionShowVisibleEntities.UpdateVisibleEntities ({NPCUnloader.characterScheduleRegistry.Count} NPC schedules, {NPCUnloader.basicEntityRegistry.Count} entities)"
        );

    [HarmonyPatch(
        typeof(Il2CppSystem.GC),
        nameof(Il2CppSystem.GC.Collect),
        new System.Type[0]
    )]
    [HarmonyPrefix]
    public static void GcCollectPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(Il2CppSystem.GC),
        nameof(Il2CppSystem.GC.Collect),
        new System.Type[0]
    )]
    [HarmonyPostfix]
    public static void GcCollectPostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "GC.Collect" + (TransitionSkipGC.SkippedLastCall ? " (skipped)" : ""));

    [HarmonyPatch(
        typeof(PrioritizedActionList),
        nameof(PrioritizedActionList.Invoke)
    )]
    [HarmonyPrefix]
    public static void PrioritizedActionListInvokePrefix(
        PrioritizedActionList __instance,
        out Timeline.HookToken __state
    )
    {
        __state = Timeline.IsActive && GetEventName(__instance) != null ? Timeline.HookBegin() : default;
    }

    [HarmonyPatch(
        typeof(PrioritizedActionList),
        nameof(PrioritizedActionList.Invoke)
    )]
    [HarmonyPostfix]
    public static void PrioritizedActionListInvokePostfix(
        PrioritizedActionList __instance,
        Timeline.HookToken __state
    )
    {
        if (__state.IsValid)
        {
            Timeline.HookEnd(
                __state,
                $"SceneTransitionManager.{GetEventName(__instance)}.Invoke ({GetListenerCount(__instance)} listeners)"
            );
        }
    }

    private static string GetEventName(PrioritizedActionList list)
    {
        if (list.Pointer == SceneTransitionManager.readyEvent?.Pointer)
        {
            return nameof(SceneTransitionManager.readyEvent);
        }

        if (list.Pointer == SceneTransitionManager.notReadyEvent?.Pointer)
        {
            return nameof(SceneTransitionManager.notReadyEvent);
        }

        return null;
    }

    private static string GetListenerCount(PrioritizedActionList list)
    {
        // IL2CPP may have stripped this getter; it's only informational
        try
        {
            return list.Count.ToString();
        }
        catch (System.Exception)
        {
            return "?";
        }
    }

    #endregion
}
