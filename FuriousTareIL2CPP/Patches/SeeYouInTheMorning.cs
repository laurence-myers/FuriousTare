using FortressOccident;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Sunshine;
using UnityEngine;
using Voidforge;

namespace FuriousTareIL2CPP.Patches;

/**
 * Kim could disappear when you enter the tent or the shack in the fishing village, and never come back out.
 *
 * When you open your room door at the Whirling late at night, Kim says goodnight (the KimGoodnight cutscene): he leaves
 * the party, PartyManager.IsKimAwayUpToMorning is set, and he walks to his own room. If you leave the upper floor before
 * he gets there, the area change hides him, which calls KimGoodnight.OnDisable. Its first line uses KimDoor.singleton,
 * which is null once the upper floor is no longer the active scene, so it throws. The rest of OnDisable doesn't run, so
 * the cutscene isn't destroyed.
 *
 * The next morning, Kim rejoins you at the stairs, which shows him again. Unity calls OnEnable on the leftover cutscene,
 * which starts it over: it sets IsKimAwayUpToMorning again (he says "See you in the morning." and "Bye."), but its call
 * to remove him from the party does nothing, because Party.Add hasn't refreshed its list of members yet. So Kim walks
 * with you while the game thinks he's away until morning.
 *
 * Entering the tent or the shack leaves Kim waiting outside (RemovingTransitionEntity.ManageRemovals). When you come
 * out, KimMarker.TryArriveHere should bring him back, but it does nothing while IsKimAwayUpToMorning is set. He comes
 * back only after you next sleep.
 *
 * To fix it, we let KimGoodnight.OnDisable finish: while it runs, KimDoor.singleton points to Kim's door, which still
 * exists on the unloaded floor. We also mark Kim as asleep in his room, which the cutscene would have done a few seconds
 * later. Saves already affected keep Kim in the party with IsKimAwayUpToMorning set, so we clear it when loading them.
 */
public class SeeYouInTheMorning
{
    private static bool _isKimDoorBorrowed;

    [HarmonyPatch(
        typeof(KimGoodnight),
        nameof(KimGoodnight.OnDisable)
    )]
    [HarmonyPrefix]
    public static void BorrowKimDoor()
    {
        if (KimDoor.singleton != null)
        {
            return;
        }

        var kimDoor = FindKimDoor();
        if (kimDoor == null)
        {
            Logger.Log.LogWarning(
                "Kim's goodnight cutscene stopped away from his door, and his door wasn't found. Can't let it finish"
            );
            return;
        }

        Logger.Log.LogInfo(
            "Kim's goodnight cutscene stopped away from his door. Letting it finish: Kim goes to bed"
        );
        KimDoor.singleton = kimDoor;
        _isKimDoorBorrowed = true;
        PartyManager.IsKimSleepingInHisRoom = true;
        PartyManager.TimeSinceKimWentSleepingInHisRoom = SingletonClass<SunshineClock>.Singleton.Time.TotalMinutes;
    }

    [HarmonyPatch(
        typeof(KimGoodnight),
        nameof(KimGoodnight.OnDisable)
    )]
    [HarmonyPostfix]
    public static void ReturnKimDoor()
    {
        if (_isKimDoorBorrowed)
        {
            KimDoor.singleton = null;
            _isKimDoorBorrowed = false;
        }
    }

    // Kim can't be in the party and away until morning at the same time
    [HarmonyPatch(
        typeof(PartyPersister),
        nameof(PartyPersister.Deserialize)
    )]
    [HarmonyPostfix]
    public static void RepairSave()
    {
        if (PartyManager.IsKimInParty() && PartyManager.IsKimAwayUpToMorning)
        {
            Logger.Log.LogInfo(
                "Kim is in the party, but the save says he's away until morning. Clearing it"
            );
            PartyManager.RestoreReturnState(false);
        }
    }

    // The upper floor's scene is loaded at startup and kept, so its door exists even when it isn't the active scene
    private static KimDoor FindKimDoor()
    {
        foreach (var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<KimDoor>()))
        {
            var kimDoor = obj.Cast<KimDoor>();
            if (kimDoor.gameObject.scene.IsValid())
            {
                return kimDoor;
            }
        }

        return null;
    }
}
