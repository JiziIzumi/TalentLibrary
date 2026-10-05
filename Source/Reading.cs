using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace TalentLibrary
{
    public class BookOutcomeProperties_Talent : BookOutcomeProperties
    {
        public override Type DoerClass { get { return typeof(BookOutcomeDoer_Talent); } }
    }

    public class BookOutcomeDoer_Talent : BookOutcomeDoer
    {
        public override bool DoesProvidesOutcome(Pawn reader) { return StudyUtility.CanLearn(reader); }
        public override string GetBenefitsString(Pawn reader = null) { return "TL_BookBenefits".Translate(); }
    }

    public class CompProperties_UniversalBook : CompProperties
    {
        public CompProperties_UniversalBook() { compClass = typeof(CompUniversalBook); }
    }

    public class CompUniversalBook : ThingComp
    {
        private Pawn currentReader;
        private int accumulatedTicks;
        public string authorName;
        public float authorKnowledge;
        public int writtenAt;

        public void ReadTicks(Pawn reader, int delta)
        {
            if (reader != currentReader) { currentReader = reader; accumulatedTicks = 0; }
            accumulatedTicks += delta;
            while (accumulatedTicks >= 60)
            {
                accumulatedTicks -= 60;
                StudyUtility.AwardExperience(reader, parent);
            }
        }

        public override void PostExposeData()
        {
            Scribe_References.Look(ref currentReader, "TL_currentReader");
            Scribe_Values.Look(ref accumulatedTicks, "TL_readTicks");
            Scribe_Values.Look(ref authorName, "TL_author");
            Scribe_Values.Look(ref authorKnowledge, "TL_authorKnowledge");
            Scribe_Values.Look(ref writtenAt, "TL_writtenAt");
        }

        public override string CompInspectStringExtra()
        {
            return authorName.NullOrEmpty() ? null : "TL_Author".Translate(authorName, authorKnowledge.ToString("0.0"));
        }
    }

    public static class StudyUtility
    {
        public static SkillState[] Snapshot(Pawn pawn)
        {
            if (pawn == null || pawn.skills == null) return new SkillState[0];
            var skills = pawn.skills.skills;
            var result = new SkillState[skills.Count];
            for (int i = 0; i < skills.Count; i++)
            {
                var s = skills[i];
                int cap = s.passion == Passion.None ? 10 : 18;
                // Displayed skill includes aptitude; XP belongs to its underlying level.
                int effective = s.Level;
                int raw = s.GetLevel(false);
                float remaining = -s.xpSinceLastLevel;
                int levelsLeft = cap - effective;
                for (int j = 0; j < levelsLeft && raw + j < 20; j++)
                    remaining += SkillRecord.XpRequiredToLevelUpFrom(raw + j);
                result[i] = new SkillState(i, (int)s.passion, effective,
                    s.TotallyDisabled || raw >= 20, Math.Max(0f, remaining), s.xpSinceMidnight);
            }
            return result;
        }

        public static bool CanLearn(Pawn pawn) { return LearningRules.SelectStage(Snapshot(pawn)) >= 0; }

        public static void AwardExperience(Pawn pawn, Thing book)
        {
            if (pawn.skills == null) return;
            QualityCategory quality;
            book.TryGetQuality(out quality);
            var awards = LearningRules.Allocate(Snapshot(pawn), LearningRules.QualityMultiplier((int)quality),
                Rand.Range(0, int.MaxValue), Rand.Range(15f, 20f));
            foreach (var a in awards)
            {
                var skill = pawn.skills.skills[a.Id];
                float final = a.XP;
                // Keep the daily counter; bypass passion and general learning multipliers.
                if (final > 0f) skill.Learn(final, direct: false, ignoreLearnRate: true);
            }
        }

        public static bool IsUsableBook(Pawn pawn, Thing thing, bool forced = false)
        {
            var book = thing as Book;
            if (book == null || book.TryGetComp<CompUniversalBook>() == null || book.Destroyed ||
                book.MapHeld != pawn.Map || book.IsForbiddenHeld(pawn) || !BookUtility.CanReadNow(pawn) ||
                !BookUtility.CanReadBook(book, pawn, out var reason) || !CanLearn(pawn) ||
                book.PositionHeld.Fogged(pawn.Map) || !book.IsPoliticallyProper(pawn)) return false;
            if (!forced && pawn.reading != null && pawn.reading.CurrentPolicy != null &&
                (!pawn.reading.CurrentPolicy.defFilter.Allows(book) || !pawn.reading.CurrentPolicy.effectFilter.Allows(book))) return false;
            return pawn.CanReserveAndReach(book, PathEndMode.Touch, Danger.None);
        }
    }

    public class WorkGiver_Study : WorkGiver
    {
        private IEnumerable<Thing> Candidates(Pawn pawn)
        {
            return pawn.Map.listerThings.ThingsOfDef(TL_DefOf.TL_UniversalBook)
                .Concat(pawn.Map.listerThings.GetThingsOfType<Building_Bookcase>()
                .SelectMany(b => b.HeldBooks).Where(b => b.def == TL_DefOf.TL_UniversalBook))
                .Distinct();
        }
        private float Priority(Pawn pawn, Thing t)
        {
            QualityCategory quality; t.TryGetQuality(out quality);
            return (int)quality * 10000f + 1000f / (1f + pawn.Position.DistanceTo(t.PositionHeld));
        }
        public override Job NonScanJob(Pawn pawn)
        {
            if (pawn.Drafted || !StudyUtility.CanLearn(pawn) || !BookUtility.CanReadNow(pawn)) return null;
            // Native GenClosest's scanner path ignores unspawned books held by bookcases.
            // Validate held books directly, exactly as vanilla BookUtility does.
            var best = Candidates(pawn).Where(t => StudyUtility.IsUsableBook(pawn, t))
                .OrderByDescending(t => Priority(pawn, t)).FirstOrDefault();
            return best == null ? null : JobMaker.MakeJob(TL_DefOf.TL_Read, best);
        }
    }

    public class JobDriver_Study : JobDriver_Reading
    {
        // Reuse vanilla's bookshelf/inventory pickup, chair selection and reservations.
        private static readonly System.Reflection.MethodInfo Prepare = AccessTools.Method(typeof(JobDriver_Reading), "PrepareToReadBook");

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SetFinalizerJob(condition =>
            {
                if (!pawn.IsCarryingThing(Book)) return null;
                if (condition == JobCondition.Succeeded) return HaulAIUtility.HaulToStorageJob(pawn, Book, false);
                Thing dropped;
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out dropped);
                return null;
            });
            foreach (var toil in (IEnumerable<Toil>)Prepare.Invoke(this, null)) yield return toil;
            var read = Toils_General.Wait(2500);
            read.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            read.initAction = () => { Book.IsOpen = true; pawn.pather.StopDead(); job.showCarryingInspectLine = false; };
            read.tickIntervalAction = delta =>
            {
                if (job.GetTarget(TargetIndex.B).IsValid) pawn.rotationTracker.FaceCell(job.GetTarget(TargetIndex.B).Cell);
                Book.TryGetComp<CompUniversalBook>().ReadTicks(pawn, delta);
                pawn.GainComfortFromCellIfPossible(delta);
                if (pawn.needs.joy != null)
                    pawn.needs.joy.GainJoy(0.00008f * delta * BookUtility.GetReadingBonus(pawn), JoyKindDefOf.Reading);
                if (!StudyUtility.CanLearn(pawn)) EndJobWith(JobCondition.Succeeded);
                if (pawn.IsHashIntervalTick(600, delta)) pawn.jobs.CheckForJobOverride(9.1f);
            };
            read.AddEndCondition(() => BookUtility.CanReadNow(pawn) && BookUtility.CanReadBook(Book, pawn, out var reason) && !pawn.Drafted
                ? JobCondition.Ongoing : JobCondition.InterruptForced);
            read.AddFinishAction(() => { if (Book != null && !Book.Destroyed) Book.IsOpen = false; });
            yield return read;
        }
    }

    [HarmonyPatch(typeof(Book), nameof(Book.OnBookReadTick))]
    public static class Patch_UniversalReading
    {
        public static bool Prefix(Book __instance, Pawn pawn, int delta)
        {
            var comp = __instance.TryGetComp<CompUniversalBook>();
            if (comp == null) return true;
            comp.ReadTicks(pawn, delta);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_SkillTracker), nameof(Pawn_SkillTracker.Learn))]
    public static class Patch_NoExtraIntellectual
    {
        public static bool Prefix(Pawn_SkillTracker __instance, SkillDef s, float xp)
        {
            // Vanilla's recreational Reading adds Intellectual XP on every tick.
            // Our direct SkillRecord awards never pass through this tracker method.
            var pawn = __instance.GetSkill(SkillDefOf.Intellectual).Pawn;
            return s != SkillDefOf.Intellectual || xp <= 0f || pawn.CurJobDef != JobDefOf.Reading ||
                pawn.CurJob.targetA.Thing == null || pawn.CurJob.targetA.Thing.TryGetComp<CompUniversalBook>() == null;
        }
    }
}
