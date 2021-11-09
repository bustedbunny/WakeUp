using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Reflection;

namespace WakeUp
{
    [RimWorld.DefOf]
    public static class WorkGiverDefOf
    {
        public static WorkGiverDef DoctorTendToHumanlikes;

        static WorkGiverDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WorkGiverDefOf));
        }
    }

    [StaticConstructorOnStartup]
    public static class StartUp
    {
        static StartUp()
        {
            var harmony = new Harmony("NoSleepDuringEmergency.HarmonyPatch");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Pawn), "TickRare")]
    class PawnTickRarePatch
    {
        private static bool HasJobOnThingFire(Pawn pawn, Thing t)
        {
            Fire fire = t as Fire;
            if (fire == null)
            {
                return false;
            }
            Pawn pawn2 = fire.parent as Pawn;
            if (pawn2 != null)
            {
                if (pawn2 == pawn)
                {
                    return false;
                }
                if ((pawn2.Faction == pawn.Faction || pawn2.HostFaction == pawn.Faction || pawn2.HostFaction == pawn.HostFaction) && !pawn.Map.areaManager.Home[fire.Position] && IntVec3Utility.ManhattanDistanceFlat(pawn.Position, pawn2.Position) > 15)
                {
                    return false;
                }
                if (!pawn.CanReach(pawn2, PathEndMode.Touch, Danger.Deadly))
                {
                    return false;
                }
            }
            else
            {
                if (pawn.WorkTagIsDisabled(WorkTags.Firefighting))
                {
                    return false;
                }
                if (!pawn.Map.areaManager.Home[fire.Position])
                {
                    return false;
                }
            }
            if ((pawn.Position - fire.Position).LengthHorizontalSquared > 225 && !pawn.CanReserve(fire, 1, -1, null, false))
            {
                return false;
            }
            if (FireIsBeingHandled(fire, pawn))
            {
                return false;
            }
            return true;
        }
        public static bool FireIsBeingHandled(Fire f, Pawn potentialHandler)
        {
            if (!f.Spawned)
            {
                return false;
            }
            return f.Map.reservationManager.FirstRespectedReserver(f, potentialHandler)?.Position.InHorDistOf(f.Position, 5f) ?? false;
        }
        private static bool HasJobOnThingTend(Pawn pawn, Thing t)
        {
            Pawn pawn2 = t as Pawn;
            if (pawn2 == null || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
            {
                return false;
            }
            if (!HealthAIUtility.ShouldBeTendedNowByPlayer(pawn2))
            {
                return false;
            }
            if (!pawn.CanReserve(pawn2, 1, -1, null, false))
            {
                return false;
            }
            if ((pawn2.InAggroMentalState && !pawn2.health.hediffSet.HasHediff(HediffDefOf.Scaria)))
            {
                return false;
            }
            return true;
        }

        private static bool GoodLayingStatusForTend(Pawn patient, Pawn doctor)
        {
            if (patient == doctor)
            {
                return true;
            }
            if (patient.RaceProps.Humanlike)
            {
                return patient.InBed();
            }
            return patient.GetPosture() != PawnPosture.Standing;
        }

        public static void Postfix(Pawn __instance)
        {
            if (Find.TickManager.TicksGame % 1750 == 0)
            {
                Pawn pawn = __instance;
                if (pawn.IsColonist && pawn.Spawned && !(pawn.Downed || pawn.InMentalState) && pawn.workSettings != null && pawn.jobs != null && pawn.jobs.curJob != null)
                {
                    if (pawn.jobs.curJob.def == JobDefOf.LayDown && !pawn.jobs.curJob.playerForced)
                    {
                        if (pawn.workSettings.WorkIsActive(WorkTypeDefOf.Firefighter))
                        {
                            List<Thing> list2 = pawn.Map.listerThings.ThingsMatching(ThingRequest.ForDef(ThingDefOf.Fire));
                            for (int i = list2.Count - 1; i >= 0; i--)
                            {
                                if (HasJobOnThingFire(pawn, list2[i]))
                                {
                                    RestUtility.WakeUp(pawn);
                                    return;
                                }
                            }
                        }
                        if (pawn.workSettings.WorkIsActive(WorkTypeDefOf.Doctor))
                        {
                            List<Pawn> list = pawn.Map.mapPawns.SpawnedPawnsWithAnyHediff;
                            for (int i = list.Count - 1; i >= 0; i--)
                            {
                                if (HasJobOnThingTend(pawn, list[i]))
                                {
                                    RestUtility.WakeUp(pawn);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

}
