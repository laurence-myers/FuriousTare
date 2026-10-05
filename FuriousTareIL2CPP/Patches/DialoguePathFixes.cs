using HarmonyLib;
using PixelCrushers.DialogueSystem;

namespace FuriousTareIL2CPP.Patches;

[HarmonyPatch(
    typeof(Conversation),
    nameof(Conversation.GetDialogueEntry),
    new[] { typeof(int) }
)]
public class DialoguePathFixes
{
    public static void Postfix(ref DialogueEntry __result)
    {
        if (__result.ArticyID() == "0x0100004500009218")
        {
            ReplaceDestination(
                __result,
                1480,
                144
            );
        }
    }

    private static void ReplaceDestination(DialogueEntry entry, int original, int replacement)
    {
        foreach (var link in entry.outgoingLinks)
        {
            if (link.destinationDialogueID == original)
            {
                link.destinationDialogueID = replacement;
                Logger.Log.LogInfo($"Replaced incorrect outgoing link for \"{entry.Title}\" (Articy ID: \"{entry.ArticyID()}\"). {link.originDialogueID} -> {replacement} (replacing {original})");
                return;
            }
        }
    }
}
