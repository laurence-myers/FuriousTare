using System;
using System.Collections.Generic;
using System.Diagnostics;
using FortressOccident;
using HarmonyLib;
using IntroSystem;
using Sunshine;
using UnityEngine;
using UnityEngine.Scripting;

namespace FuriousTareIL2CPP.Patches;

/**
 * Diagnostic: measures where the time goes between launching the game and reaching the main menu, and logs a timeline
 * once the main menu is ready. Times are relative to the process start.
 *
 * The startup sequence (from the Mono build of the game, GOG 2023-03-16):
 * - Scene "PreInitialize": PreInitialize.Start runs LoadGameData (settings, save game list), and WaitAndStartTheGame,
 *   which waits for at least 5 seconds, then loads the scene "Initialize".
 * - Scene "Initialize": IntroManager plays the intro video. AreaManager.LoadAreasCoR loads more scenes additively. In
 *   parallel, DialogueBundleLoader loads the dialogue database via Addressables.
 * - When both are done, ApplicationManager.LoadLobby starts FastLoadManager.LoadAllScenes, which loads every game area
 *   one by one, then starts the transition to the "Lobby" scene (the main menu).
 * - The startup ends when LoadAllScenes and the Lobby transition have both finished.
 */
public class StartupTimings
{
    private static readonly Timeline Timeline = new();
    private static bool _allScenesLoaded;
    private static bool _lobbyLoaded;

    public static void Begin()
    {
        var sinceProcessStart = DateTime.Now - Process.GetCurrentProcess().StartTime;
        Timeline.Begin(
            "Startup",
            Stopwatch.GetTimestamp() - (long)(sinceProcessStart.TotalSeconds * Stopwatch.Frequency),
            0
        );
        Timeline.Mark("Plugin loaded");
    }

    private static void TryEnd()
    {
        if (!_allScenesLoaded || !_lobbyLoaded)
        {
            return;
        }

        Timeline.End(
            "main menu ready",
            new List<string>
            {
                $"After startup: Application.backgroundLoadingPriority: {Application.backgroundLoadingPriority}, QualitySettings.vSyncCount: {QualitySettings.vSyncCount}, Application.targetFrameRate: {Application.targetFrameRate}",
                $"GarbageCollector.GCMode: {GarbageCollector.GCMode}, isIncremental: {GarbageCollector.isIncremental}",
            }
        );
    }

    #region Coroutine labels

    private static string WaitAndStartTheGameResume(int state) => state switch
    {
        0 => "start",
        1 => "check splash finished",
        2 => "check Initialize scene loaded",
        3 => "(done)",
        _ => $"state {state}",
    };

    private static string WaitAndStartTheGameYield(int state) => state switch
    {
        1 => "splash: 5 seconds minimum, and LoadGameData",
        2 => "Initialize scene async load",
        3 => "next frame",
        _ => Timeline.YieldOrEnd(state),
    };

    private static string LoadGameDataResume(int state) => state switch
    {
        0 => "start",
        1 => "apply language, start reading save games",
        2 or 3 => "(save games read)",
        _ => $"state {state}",
    };

    private static string LoadGameDataYield(int state) => state switch
    {
        1 => "SettingsPersister.ReadSettingsCoR",
        2 => "SunshinePersistenceFileManager.CacheSaveGamesInit2",
        3 => "SaveFolderAccessor.ReadGameSavesCoR",
        _ => Timeline.YieldOrEnd(state),
    };

    private static string PlayIntroSequenceResume(int state) => state switch
    {
        0 => "start",
        1 or 2 => "(page UI delay finished)",
        3 => "(intro video finished)",
        4 => "(delay finished)",
        _ => $"state {state}",
    };

    private static string PlayIntroSequenceYield(int state) => state switch
    {
        1 => "page UI: 3 second delay",
        2 => "page UI: 1 second delay",
        3 => "intro video (VideoStream.StartVideo)",
        4 => "delayBetweenIntroElements",
        _ => Timeline.YieldOrEnd(state),
    };

    private static string LoadAreasResume(int state) => state switch
    {
        0 => "start",
        1 => "start loading next scene",
        2 => "(scene loaded)",
        3 => "check scene loaded",
        _ => $"state {state}",
    };

    private static string LoadAreasYield(int state) => state switch
    {
        1 => "end of frame",
        2 => "async scene load",
        3 => "scene already loading",
        _ => "(end) onLoadingComplete",
    };

    private static string DialogueBundleLoaderResume(int state) => state switch
    {
        0 => "start Addressables.InitializeAsync",
        1 => "Load: start async load of dialogue database",
        _ => $"state {state}",
    };

    private static string DialogueBundleLoaderYield(int state) => state switch
    {
        1 => "Addressables.InitializeAsync",
        _ => Timeline.YieldOrEnd(state),
    };

    private static string LoadAllScenesResume(int state) => state switch
    {
        0 => "start",
        1 => "check scene loaded, start next scene (or finish: load Lobby, GC.Collect, showAnim)",
        2 => "(done)",
        _ => $"state {state}",
    };

    private static string LoadAllScenesYield(int state) => state switch
    {
        1 => "async scene load",
        2 => "next frame",
        _ => Timeline.YieldOrEnd(state),
    };

    #endregion

    #region Coroutines

    [HarmonyPatch(
        typeof(PreInitialize._WaitAndStartTheGame_d__13),
        nameof(PreInitialize._WaitAndStartTheGame_d__13.MoveNext)
    )]
    [HarmonyPrefix]
    public static void WaitAndStartTheGamePrefix(PreInitialize._WaitAndStartTheGame_d__13 __instance) =>
        Timeline.StepPrefix(
            __instance.Pointer,
            __instance.__1__state,
            () => "PreInitialize.WaitAndStartTheGame",
            WaitAndStartTheGameResume,
            WaitAndStartTheGameYield
        );

    [HarmonyPatch(
        typeof(PreInitialize._WaitAndStartTheGame_d__13),
        nameof(PreInitialize._WaitAndStartTheGame_d__13.MoveNext)
    )]
    [HarmonyPostfix]
    public static void WaitAndStartTheGamePostfix(PreInitialize._WaitAndStartTheGame_d__13 __instance, bool __result) =>
        Timeline.StepPostfix(
            __instance.Pointer,
            __result,
            __instance.__1__state
        );

    [HarmonyPatch(
        typeof(PreInitialize._LoadGameData_d__18),
        nameof(PreInitialize._LoadGameData_d__18.MoveNext)
    )]
    [HarmonyPrefix]
    public static void LoadGameDataPrefix(PreInitialize._LoadGameData_d__18 __instance) =>
        Timeline.StepPrefix(
            __instance.Pointer,
            __instance.__1__state,
            () => "PreInitialize.LoadGameData",
            LoadGameDataResume,
            LoadGameDataYield
        );

    [HarmonyPatch(
        typeof(PreInitialize._LoadGameData_d__18),
        nameof(PreInitialize._LoadGameData_d__18.MoveNext)
    )]
    [HarmonyPostfix]
    public static void LoadGameDataPostfix(PreInitialize._LoadGameData_d__18 __instance, bool __result) =>
        Timeline.StepPostfix(
            __instance.Pointer,
            __result,
            __instance.__1__state
        );

    [HarmonyPatch(
        typeof(IntroManager._PlayIntroSequence_d__19),
        nameof(IntroManager._PlayIntroSequence_d__19.MoveNext)
    )]
    [HarmonyPrefix]
    public static void PlayIntroSequencePrefix(IntroManager._PlayIntroSequence_d__19 __instance) =>
        Timeline.StepPrefix(
            __instance.Pointer,
            __instance.__1__state,
            () => "IntroManager.PlayIntroSequence",
            PlayIntroSequenceResume,
            PlayIntroSequenceYield
        );

    [HarmonyPatch(
        typeof(IntroManager._PlayIntroSequence_d__19),
        nameof(IntroManager._PlayIntroSequence_d__19.MoveNext)
    )]
    [HarmonyPostfix]
    public static void PlayIntroSequencePostfix(IntroManager._PlayIntroSequence_d__19 __instance, bool __result) =>
        Timeline.StepPostfix(
            __instance.Pointer,
            __result,
            __instance.__1__state
        );

    [HarmonyPatch(
        typeof(AreaManager._LoadAreasCoR_d__26),
        nameof(AreaManager._LoadAreasCoR_d__26.MoveNext)
    )]
    [HarmonyPrefix]
    public static void LoadAreasPrefix(AreaManager._LoadAreasCoR_d__26 __instance) =>
        Timeline.StepPrefix(
            __instance.Pointer,
            __instance.__1__state,
            () => "AreaManager.LoadAreasCoR",
            LoadAreasResume,
            LoadAreasYield
        );

    [HarmonyPatch(
        typeof(AreaManager._LoadAreasCoR_d__26),
        nameof(AreaManager._LoadAreasCoR_d__26.MoveNext)
    )]
    [HarmonyPostfix]
    public static void LoadAreasPostfix(AreaManager._LoadAreasCoR_d__26 __instance, bool __result)
    {
        string scene = null;
        var areaManager = __instance.__4__this;
        if (__result && areaManager != null)
        {
            var scenes = areaManager.additivelyLoadedScenes;
            var index = areaManager._loadedScenesCount;
            if (scenes != null && index < scenes.Count)
            {
                scene = scenes[index];
            }
        }

        Timeline.StepPostfix(
            __instance.Pointer,
            __result,
            __instance.__1__state,
            scene
        );
    }

    [HarmonyPatch(
        typeof(DialogueBundleLoader._Start_d__10),
        nameof(DialogueBundleLoader._Start_d__10.MoveNext)
    )]
    [HarmonyPrefix]
    public static void DialogueBundleLoaderStartPrefix(DialogueBundleLoader._Start_d__10 __instance) =>
        Timeline.StepPrefix(
            __instance.Pointer,
            __instance.__1__state,
            () => "DialogueBundleLoader.Start",
            DialogueBundleLoaderResume,
            DialogueBundleLoaderYield
        );

    [HarmonyPatch(
        typeof(DialogueBundleLoader._Start_d__10),
        nameof(DialogueBundleLoader._Start_d__10.MoveNext)
    )]
    [HarmonyPostfix]
    public static void DialogueBundleLoaderStartPostfix(DialogueBundleLoader._Start_d__10 __instance, bool __result) =>
        Timeline.StepPostfix(
            __instance.Pointer,
            __result,
            __instance.__1__state
        );

    [HarmonyPatch(
        typeof(FastLoadManager._LoadAllScenes_d__8),
        nameof(FastLoadManager._LoadAllScenes_d__8.MoveNext)
    )]
    [HarmonyPrefix]
    public static void LoadAllScenesPrefix(FastLoadManager._LoadAllScenes_d__8 __instance) =>
        Timeline.StepPrefix(
            __instance.Pointer,
            __instance.__1__state,
            () => "FastLoadManager.LoadAllScenes",
            LoadAllScenesResume,
            LoadAllScenesYield
        );

    [HarmonyPatch(
        typeof(FastLoadManager._LoadAllScenes_d__8),
        nameof(FastLoadManager._LoadAllScenes_d__8.MoveNext)
    )]
    [HarmonyPostfix]
    public static void LoadAllScenesPostfix(FastLoadManager._LoadAllScenes_d__8 __instance, bool __result)
    {
        Timeline.StepPostfix(
            __instance.Pointer,
            __result,
            __instance.__1__state,
            __result ? __instance._name_5__4 : null
        );
        if (!__result)
        {
            _allScenesLoaded = true;
            TryEnd();
        }
    }

    [HarmonyPatch(
        typeof(SceneTransitionManager._LoadSceneCoR_d__36),
        nameof(SceneTransitionManager._LoadSceneCoR_d__36.MoveNext)
    )]
    [HarmonyPrefix]
    public static void LoadSceneCoRPrefix(SceneTransitionManager._LoadSceneCoR_d__36 __instance) =>
        Timeline.StepPrefix(
            __instance.Pointer,
            __instance.__1__state,
            () => $"LoadSceneCoR(\"{__instance.sceneName}\")",
            TransitionTimings.LoadSceneResume,
            TransitionTimings.LoadSceneYield
        );

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
        if (!__result && Timeline.IsActive && __instance.sceneName == "Scenes/Lobby")
        {
            _lobbyLoaded = true;
            TryEnd();
        }
    }

    [HarmonyPatch(
        typeof(SceneTransitionManager._ShowLoadingScreenCoR_d__37),
        nameof(SceneTransitionManager._ShowLoadingScreenCoR_d__37.MoveNext)
    )]
    [HarmonyPrefix]
    public static void ShowLoadingScreenCoRPrefix(SceneTransitionManager._ShowLoadingScreenCoR_d__37 __instance) =>
        Timeline.StepPrefix(
            __instance.Pointer,
            __instance.__1__state,
            () => __instance.show ? "ShowLoadingScreenCoR(show)" : "ShowLoadingScreenCoR(hide)",
            TransitionTimings.ShowLoadingScreenResume,
            TransitionTimings.ShowLoadingScreenYield
        );

    [HarmonyPatch(
        typeof(SceneTransitionManager._ShowLoadingScreenCoR_d__37),
        nameof(SceneTransitionManager._ShowLoadingScreenCoR_d__37.MoveNext)
    )]
    [HarmonyPostfix]
    public static void ShowLoadingScreenCoRPostfix(
        SceneTransitionManager._ShowLoadingScreenCoR_d__37 __instance,
        bool __result
    ) =>
        Timeline.StepPostfix(
            __instance.Pointer,
            __result,
            __instance.__1__state
        );

    #endregion

    #region Methods

    [HarmonyPatch(
        typeof(PreInitialize),
        nameof(PreInitialize.Start)
    )]
    [HarmonyPrefix]
    public static void PreInitializeStartPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(PreInitialize),
        nameof(PreInitialize.Start)
    )]
    [HarmonyPostfix]
    public static void PreInitializeStartPostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "PreInitialize.Start");

    [HarmonyPatch(
        typeof(IntroManager),
        nameof(IntroManager.Start)
    )]
    [HarmonyPrefix]
    public static void IntroManagerStartPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(IntroManager),
        nameof(IntroManager.Start)
    )]
    [HarmonyPostfix]
    public static void IntroManagerStartPostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "IntroManager.Start");

    [HarmonyPatch(
        typeof(IntroManager),
        nameof(IntroManager.OnLoadCompleted)
    )]
    [HarmonyPrefix]
    public static void OnLoadCompletedPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(IntroManager),
        nameof(IntroManager.OnLoadCompleted)
    )]
    [HarmonyPostfix]
    public static void OnLoadCompletedPostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "IntroManager.OnLoadCompleted (AreaManager finished)");

    [HarmonyPatch(
        typeof(ApplicationManager),
        nameof(ApplicationManager.OnStartCompleted)
    )]
    [HarmonyPrefix]
    public static void OnStartCompletedPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(ApplicationManager),
        nameof(ApplicationManager.OnStartCompleted)
    )]
    [HarmonyPostfix]
    public static void OnStartCompletedPostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(
            __state,
            $"ApplicationManager.OnStartCompleted (dialogue database ready: {DialogueBundleLoader.isReady})"
        );

    [HarmonyPatch(
        typeof(ApplicationManager),
        nameof(ApplicationManager.LoadLobby)
    )]
    [HarmonyPrefix]
    public static void LoadLobbyPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(ApplicationManager),
        nameof(ApplicationManager.LoadLobby)
    )]
    [HarmonyPostfix]
    public static void LoadLobbyPostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "ApplicationManager.LoadLobby");

    [HarmonyPatch(
        typeof(DialogueBundleLoader),
        nameof(DialogueBundleLoader.LoadBuildBundle)
    )]
    [HarmonyPrefix]
    public static void LoadBuildBundlePrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(DialogueBundleLoader),
        nameof(DialogueBundleLoader.LoadBuildBundle)
    )]
    [HarmonyPostfix]
    public static void LoadBuildBundlePostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "DialogueBundleLoader.LoadBuildBundle");

    [HarmonyPatch(
        typeof(DialogueBundleLoader),
        "LoadDone"
    )]
    [HarmonyPrefix]
    public static void LoadDonePrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(DialogueBundleLoader),
        "LoadDone"
    )]
    [HarmonyPostfix]
    public static void LoadDonePostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "DialogueBundleLoader.LoadDone (dialogue database loaded)");

    [HarmonyPatch(
        typeof(DialogueBundleLoader),
        nameof(DialogueBundleLoader.LoadDatabase)
    )]
    [HarmonyPrefix]
    public static void LoadDatabasePrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(DialogueBundleLoader),
        nameof(DialogueBundleLoader.LoadDatabase)
    )]
    [HarmonyPostfix]
    public static void LoadDatabasePostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "DialogueBundleLoader.LoadDatabase");

    [HarmonyPatch(
        typeof(FastLoadManager),
        nameof(FastLoadManager.showAnim)
    )]
    [HarmonyPrefix]
    public static void ShowAnimPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(FastLoadManager),
        nameof(FastLoadManager.showAnim)
    )]
    [HarmonyPostfix]
    public static void ShowAnimPostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "FastLoadManager.showAnim (ZAUM logo)");

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
    public static void SetActivePostfix(
        Timeline.HookToken __state,
        UnityEngine.SceneManagement.Scene __0,
        bool __1
    ) =>
        Timeline.HookEnd(__state, $"SceneTransitionManager.SetActive(\"{__0.name}\", {__1})");

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
        typeof(Il2CppSystem.GC),
        nameof(Il2CppSystem.GC.Collect),
        new Type[0]
    )]
    [HarmonyPrefix]
    public static void GcCollectPrefix(out Timeline.HookToken __state) => __state = Timeline.HookBegin();

    [HarmonyPatch(
        typeof(Il2CppSystem.GC),
        nameof(Il2CppSystem.GC.Collect),
        new Type[0]
    )]
    [HarmonyPostfix]
    public static void GcCollectPostfix(Timeline.HookToken __state) =>
        Timeline.HookEnd(__state, "GC.Collect" + (TransitionSkipGC.SkippedLastCall ? " (skipped)" : ""));

    #endregion
}
