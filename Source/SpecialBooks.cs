using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TalentLibrary
{
    public enum SpecialEffect { UnlockWork, RemoveBrawler, AddBrawler }
    public class CompProperties_SpecialBook : CompProperties
    {
        public SpecialEffect effect;
        public CompProperties_SpecialBook() { compClass = typeof(CompSpecialBook); }
    }

    public class CompSpecialBook : ThingComp
    {
        public CompProperties_SpecialBook Props { get { return (CompProperties_SpecialBook)props; } }
        private TraitDef Brawler { get { return TraitDefOf.Brawler; } }

        public AcceptanceReport CanApply(Pawn target)
        {
            if (target == null || target.Dead || !target.RaceProps.Humanlike || target.story == null)
                return "TL_InvalidTarget".Translate();
            if (Props.effect == SpecialEffect.UnlockWork && WorkUnlock.Active(target))
                return "TL_AlreadyUnlocked".Translate();
            bool hasBrawler = target.story.traits.allTraits.Any(t => t.def == Brawler);
            if (Props.effect == SpecialEffect.AddBrawler && hasBrawler) return "TL_AlreadyBrawler".Translate();
            if (Props.effect == SpecialEffect.RemoveBrawler && !hasBrawler) return "TL_NoBrawler".Translate();
            return true;
        }

        public bool Apply(Pawn target)
        {
            if (!CanApply(target).Accepted) return false;
            if (Props.effect == SpecialEffect.UnlockWork)
                target.health.AddHediff(TL_DefOf.TL_WorkUnlocked);
            else if (Props.effect == SpecialEffect.AddBrawler)
                target.story.traits.GainTrait(new Trait(Brawler), suppressConflicts: false);
            else
            {
                foreach (var trait in target.story.traits.allTraits.Where(t => t.def == Brawler).ToList())
                {
                    // Vanilla RemoveTrait deletes the source gene too. Preserve the gene and other traits.
                    trait.sourceGene = null;
                    // RemoveTrait's HasTrait check excludes suppressed traits. A latent Brawler
                    // must also be removed rather than consuming the book without an effect.
                    trait.suppressedByGene = null;
                    trait.suppressedByTrait = false;
                    target.story.traits.RemoveTrait(trait, unsuppressConflicts: false);
                }
            }
            WorkUnlock.Refresh(target);
            parent.SplitOff(1).Destroy(DestroyMode.Vanish);
            Messages.Message("TL_UsedBook".Translate(target.LabelShortCap), target, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        private bool CanOperate(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && pawn.IsColonistPlayerControlled && !pawn.Downed && !pawn.Drafted &&
                parent.Spawned && !parent.IsForbidden(pawn) && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
                pawn.CanReserveAndReach(parent, PathEndMode.Touch, Danger.None);
        }

        private void Order(Pawn actor, Pawn target)
        {
            if (!CanOperate(actor) || !CanApply(target).Accepted || target.Map != actor.Map ||
                !actor.CanReserveAndReach(target, PathEndMode.Touch, Danger.None)) return;
            var job = JobMaker.MakeJob(TL_DefOf.TL_ApplySpecialBook, parent, target);
            job.count = 1;
            actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned) yield break;
            yield return new Command_Action
            {
                defaultLabel = "TL_ApplyBook".Translate(),
                defaultDesc = "TL_ApplyBookDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get(parent.def.graphicData.texPath),
                action = () =>
                {
                    var options = new List<FloatMenuOption>();
                    foreach (var target in parent.Map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && (p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony)))
                    {
                        var report = CanApply(target);
                        var actor = parent.Map.mapPawns.FreeColonistsSpawned.Where(CanOperate)
                            .Where(p => p.CanReserveAndReach(target, PathEndMode.Touch, Danger.None))
                            .OrderBy(p => p == target ? 0f : p.Position.DistanceTo(parent.Position)).FirstOrDefault();
                        var picked = target; var user = actor;
                        string reason = report.Accepted ? (actor == null ? "TL_NoUser".Translate().ToString() : null) : report.Reason;
                        options.Add(new FloatMenuOption(target.LabelShortCap + (reason == null ? "" : ": " + reason),
                            reason == null ? (System.Action)(() => Order(user, picked)) : null));
                    }
                    if (options.Count == 0) options.Add(new FloatMenuOption("TL_NoTarget".Translate(), null));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            var report = CanApply(selPawn);
            string label = "TL_UseSelf".Translate(parent.LabelCap);
            bool allowed = report.Accepted && CanOperate(selPawn);
            if (!report.Accepted) label += ": " + report.Reason;
            yield return new FloatMenuOption(label, allowed ? (System.Action)(() => Order(selPawn, selPawn)) : null);
        }
    }

    public class JobDriver_ApplySpecialBook : JobDriver
    {
        private Thing Book { get { return job.targetA.Thing; } }
        private Pawn Recipient { get { return job.targetB.Pawn; } }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Book, job, 1, 1, null, errorOnFailed) &&
                (Recipient == pawn || pawn.Reserve(Recipient, job, 1, -1, null, errorOnFailed));
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOn(() => Recipient.Dead || !Book.TryGetComp<CompSpecialBook>().CanApply(Recipient).Accepted);
            AddFinishAction(condition =>
            {
                if (pawn.carryTracker.CarriedThing != null && pawn.carryTracker.CarriedThing.TryGetComp<CompSpecialBook>() != null)
                {
                    Thing dropped; pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out dropped);
                }
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            if (Recipient != pawn) yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.Wait(180).WithProgressBarToilDelay(TargetIndex.A);
            var apply = ToilMaker.MakeToil("TL_ApplyBook");
            apply.initAction = () =>
            {
                if (Recipient.Map != pawn.Map || Recipient.Position.DistanceTo(pawn.Position) > 2f)
                { EndJobWith(JobCondition.Incompletable); return; }
                if (!Book.TryGetComp<CompSpecialBook>().Apply(Recipient)) EndJobWith(JobCondition.Incompletable);
            };
            apply.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return apply;
        }
    }
}
