using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TalentLibrary
{
    public static class WorkUnlock
    {
        public static bool Active(Pawn pawn)
        { return pawn != null && pawn.health != null && pawn.health.hediffSet.HasHediff(TL_DefOf.TL_WorkUnlocked); }

        public static WorkTags PhysicalTags(Pawn pawn)
        {
            WorkTags tags = WorkTags.None;
            foreach (var hediff in pawn.health.hediffSet.hediffs)
                if (hediff.CurStage != null) tags |= hediff.CurStage.disabledWorkTags;
            return tags;
        }

        public static List<WorkTypeDef> PhysicalTypes(Pawn pawn, bool permanentOnly)
        {
            var result = new List<WorkTypeDef>();
            if (permanentOnly) return result;
            foreach (var type in pawn.health.DisabledWorkTypes)
                if (!result.Contains(type)) result.Add(type);
            foreach (var setting in pawn.RaceProps.lifeStageWorkSettings)
                if (setting.IsDisabled(pawn) && !result.Contains(setting.workType)) result.Add(setting.workType);
            return result;
        }

        public static void Refresh(Pawn pawn)
        {
            pawn.Notify_DisabledWorkTypesChanged();
            if (pawn.skills != null) pawn.skills.Notify_SkillDisablesChanged();
            pawn.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty();
        }
    }

    public class Hediff_WorkUnlocked : Hediff
    {
        public override void PostAdd(DamageInfo? dinfo) { base.PostAdd(dinfo); WorkUnlock.Refresh(pawn); }
        public override void PostRemoved() { base.PostRemoved(); WorkUnlock.Refresh(pawn); }
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) WorkUnlock.Refresh(pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_CombinedDisabledWorkTags")]
    public static class Patch_WorkTags
    {
        public static void Postfix(Pawn __instance, ref WorkTags __result)
        { if (WorkUnlock.Active(__instance)) __result = WorkUnlock.PhysicalTags(__instance); }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetDisabledWorkTypes))]
    public static class Patch_WorkTypes
    {
        public static bool Prefix(Pawn __instance, bool permanentOnly, ref List<WorkTypeDef> __result)
        {
            if (!WorkUnlock.Active(__instance)) return true;
            // Return a fresh list: callers must never mutate another pawn's disabled-work cache.
            __result = WorkUnlock.PhysicalTypes(__instance, permanentOnly);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetReasonsForDisabledWorkType))]
    public static class Patch_WorkReasons
    {
        public static bool Prefix(Pawn __instance, WorkTypeDef workType, ref List<string> __result)
        {
            if (!WorkUnlock.Active(__instance)) return true;
            __result = new List<string>();
            if (WorkUnlock.PhysicalTypes(__instance, false).Contains(workType) || (WorkUnlock.PhysicalTags(__instance) & workType.workTags) != 0)
                __result.Add("TL_PhysicalRestriction".Translate());
            return false;
        }
    }
}
