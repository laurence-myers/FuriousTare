using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using FuriousTareIL2CPP.Patches;
using HarmonyLib;

namespace FuriousTareIL2CPP;

public class PluginEntryPoint
{
    // Bug fixes and tweaks, each enabled individually in the "Patches" section
    private static readonly Type[] Patches = new[]
    {
        typeof(AbilityLabelOverflow),
        typeof(DisableCollageMode),
        typeof(DialoguePathFixes),
        typeof(HandHud),
        typeof(HandHudReplaceHeldItem),
        typeof(MuzzleKimsBark),
        typeof(RemapVoiceOvers), // should apply after VoiceOverFixAlternatives by using low priority
        typeof(TweakHudWhiteSpace),
        typeof(SkipIncorrectVoiceOver),
        typeof(StopWavingThatFlashlight),
        typeof(TakeASwig),
        typeof(ScrollSensitivityTweaks),
        typeof(ThrowAGunLoseAGun),
        typeof(VoiceOverFixAlternatives),
    };

    private class PatchGroup
    {
        public string Section;
        public string Description;

        // Stripped from the patch name to make its config key, e.g. "StartupSkipSplashMinimum" -> "SkipSplashMinimum"
        public string KeyPrefix = "";

        // Adds an "Enabled" config entry, which disables every patch in the group when false
        public bool HasMasterSwitch = true;
        public bool EnabledByDefault = true;
        public (Type patch, string description)[] Patches;
    }

    private static readonly PatchGroup[] Groups = new[]
    {
        new PatchGroup
        {
            Section = ConfigSections.StartupTweaks,
            Description = "Speed up the game startup",
            KeyPrefix = "Startup",
            Patches = new[]
            {
                (typeof(StartupLoadingPriority),
                    "Raise Unity's loading priority (and optionally turn off vsync) until the main menu is ready"),
                (typeof(StartupSkipShaderWarmup),
                    "Skip warming up every shader when entering the main menu. Shaders are compiled when first drawn instead"),
                (typeof(StartupSkipSplashMinimum),
                    "Skip the 5 second minimum duration of the first loading screen"),
            },
        },
        new PatchGroup
        {
            Section = ConfigSections.TransitionTweaks,
            Description = "Speed up area transitions, e.g. when walking through a door",
            KeyPrefix = "Transition",
            Patches = new[]
            {
                (typeof(TransitionFadeSpeed),
                    "Fade the loading screen in and out faster. See FadeSpeedMultiplier"),
                (typeof(TransitionSkipGC),
                    "Skip the forced garbage collection at the end of each area transition"),
                (typeof(TransitionShowVisibleEntities),
                    "Show the characters and objects in view before the loading screen fades out, instead of over the following frames"),
                (typeof(TransitionSkipShaderWarmup),
                    "Skip warming up every shader again on each area transition"),
                (typeof(TransitionTextureWarmupBudget),
                    "Spend less time streaming textures while the loading screen is shown. See TextureWarmupSeconds"),
            },
        },
        new PatchGroup
        {
            Section = ConfigSections.Diagnostics,
            Description = "Log timing information",
            HasMasterSwitch = false,
            EnabledByDefault = false,
            Patches = new[]
            {
                (typeof(StartupTimings),
                    "Log a timeline of where the time goes between launching the game and reaching the main menu"),
                (typeof(TransitionTimings),
                    "Log a timeline of where the time goes when changing area"),
            },
        },
    };

    private readonly Dictionary<Type, bool> _enabledPatches = new Dictionary<Type, bool>();

    private void LoadConfig(ConfigFile configFile)
    {
        foreach (var patch in Patches)
        {
            var configEntry = configFile.Bind(
                ConfigSections.Patches,
                patch.Name,
                true
            );
            _enabledPatches[patch] = configEntry.Value;
        }

        foreach (var group in Groups)
        {
            var isGroupEnabled = !group.HasMasterSwitch || configFile.Bind(
                group.Section,
                "Enabled",
                true,
                $"{group.Description}. Set to false to disable every patch in this section"
            ).Value;
            foreach (var (patch, description) in group.Patches)
            {
                var key = patch.Name.StartsWith(group.KeyPrefix)
                    ? patch.Name.Substring(group.KeyPrefix.Length)
                    : patch.Name;
                var configEntry = configFile.Bind(
                    group.Section,
                    key,
                    group.EnabledByDefault,
                    description
                );
                _enabledPatches[patch] = isGroupEnabled && configEntry.Value;
            }
        }

        ScrollSensitivityTweaks.LoadConfig(configFile);
        StartupLoadingPriority.LoadConfig(configFile);
        TransitionFadeSpeed.LoadConfig(configFile);
        TransitionTextureWarmupBudget.LoadConfig(configFile);
    }

    public PluginEntryPoint(ConfigFile configFile, string pluginName, string pluginGuid)
    {
        LoadConfig(configFile);

        var harmony = new Harmony(
            pluginGuid
        );

        // DebugTypeLogger.RegisterPatches(typeof(HudHeldButton));
        // DebugTypeLogger.RegisterPatches(typeof(HudHeldPanelController));
        // DebugTypeLogger.RegisterPatches(typeof(InventoryViewData));

        var allPatches = Patches.Concat(
            Groups.SelectMany(group => group.Patches.Select(entry => entry.patch))
        );
        foreach (var patch in allPatches)
        {
            if (_enabledPatches[patch])
            {
                Logger.Log.LogInfo(
                    $"Applying patch: {patch.Name}"
                );
                harmony.PatchAll(
                    patch
                );
            }
            else
            {
                Logger.Log.LogInfo(
                    $"Skipping disabled patch: {patch.Name}"
                );
            }
        }

        if (_enabledPatches[typeof(StartupTimings)])
        {
            StartupTimings.Begin();
        }

        if (_enabledPatches[typeof(StartupLoadingPriority)])
        {
            StartupLoadingPriority.Apply();
        }

        Logger.Log.LogInfo(
            $"Plugin \"{pluginName}\" (\"{pluginGuid}\") is loaded!"
        );
    }
}
