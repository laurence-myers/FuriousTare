using System;
using BepInEx.Configuration;
using FortressOccident;
using HarmonyLib;
using UnityEngine;
using ThreadPriority = UnityEngine.ThreadPriority;

namespace FuriousTareIL2CPP.Patches;

/**
 * Startup loads a lot asynchronously: the "Init" scene, the dialogue database, then every game area. Unity limits how
 * much main thread time async loading may use per frame, via Application.backgroundLoadingPriority. The game never
 * sets it, so it uses the default, BelowNormal: ~4 ms per frame. With vsync on, loading sits idle most of the time.
 *
 * During startup, we raise the priority to High (~50 ms per frame), and optionally turn off vsync and the frame rate
 * cap, so more frames (and so more loading time slices) fit in each second. Once the main menu is ready, we restore
 * the original values, so gameplay isn't affected.
 */
public class StartupLoadingPriority
{
    private static bool _disableVSync = true;
    private static bool _isApplied;
    private static bool _isRestored;
    private static ThreadPriority _originalPriority;
    private static int _originalVSyncCount;
    private static int _originalTargetFrameRate;

    public static void LoadConfig(ConfigFile configFile)
    {
        _disableVSync = configFile.Bind(
            ConfigSections.StartupTweaks,
            "DisableVSyncWhileLoading",
            _disableVSync,
            "Also turn off vsync and the frame rate cap while the game starts up"
        ).Value;
    }

    /**
     * Called when the plugin loads. If Unity isn't ready yet, we try again when the first scene starts.
     */
    public static void Apply()
    {
        if (_isApplied || _isRestored)
        {
            return;
        }

        try
        {
            _originalPriority = Application.backgroundLoadingPriority;
            _originalVSyncCount = QualitySettings.vSyncCount;
            _originalTargetFrameRate = Application.targetFrameRate;

            Application.backgroundLoadingPriority = ThreadPriority.High;
            if (_disableVSync)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }

            _isApplied = true;
            Logger.Log.LogInfo(
                $"Startup loading priority set from {_originalPriority} -> {Application.backgroundLoadingPriority}"
                + (_disableVSync
                    ? $", vSyncCount {_originalVSyncCount} -> 0, targetFrameRate {_originalTargetFrameRate} -> -1"
                    : "")
            );
        }
        catch (Exception e)
        {
            Logger.Log.LogDebug(
                $"Could not set the startup loading priority yet: {e.Message}"
            );
        }
    }

    private static void Restore()
    {
        if (!_isApplied || _isRestored)
        {
            return;
        }

        _isRestored = true;
        Application.backgroundLoadingPriority = _originalPriority;
        if (_disableVSync)
        {
            // Only undo our own changes: if the game changed a value in the meantime, keep the game's value
            if (QualitySettings.vSyncCount == 0)
            {
                QualitySettings.vSyncCount = _originalVSyncCount;
            }

            if (Application.targetFrameRate == -1)
            {
                Application.targetFrameRate = _originalTargetFrameRate;
            }
        }

        Logger.Log.LogInfo(
            $"Startup finished, restored loading priority {Application.backgroundLoadingPriority}, vSyncCount {QualitySettings.vSyncCount}, targetFrameRate {Application.targetFrameRate}"
        );
    }

    [HarmonyPatch(
        typeof(PreInitialize),
        nameof(PreInitialize.Start)
    )]
    [HarmonyPrefix]
    public static void PreInitializeStartPrefix()
    {
        Apply();
    }

    [HarmonyPatch(
        typeof(SceneTransitionManager._LoadSceneCoR_d__36),
        nameof(SceneTransitionManager._LoadSceneCoR_d__36.MoveNext)
    )]
    [HarmonyPostfix]
    public static void LoadSceneCoRPostfix(SceneTransitionManager._LoadSceneCoR_d__36 __instance, bool __result)
    {
        // The main menu is ready
        if (!__result && !_isRestored && __instance.sceneName == "Scenes/Lobby")
        {
            Restore();
        }
    }
}
