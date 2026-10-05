namespace FuriousTareIL2CPP;

public static class ConfigSections
{
    // Bug fixes and tweaks, each enabled individually
    public const string Patches = "Patches";

    // Groups of related tweaks, enabled together
    public const string StartupTweaks = "StartupTweaks";
    public const string TransitionTweaks = "TransitionTweaks";

    // Disabled by default: they only log information
    public const string Diagnostics = "Diagnostics";
}
