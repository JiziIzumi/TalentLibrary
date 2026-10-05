using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TalentLibrary
{
    [DefOf]
    public static class TL_DefOf
    {
        public static ThingDef TL_UniversalBook;
        public static HediffDef TL_WorkUnlocked;
        public static JobDef TL_Read, TL_ApplySpecialBook;
        static TL_DefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(TL_DefOf)); }
    }

    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup() { new Harmony("wentao.talentlibrary").PatchAll(); }
    }

    [HarmonyPatch(typeof(GenRecipe), "PostProcessProduct")]
    public static class Patch_WritingQuality
    {
        public static bool Prefix(Thing product, Pawn worker, RecipeDef recipeDef, ref Thing __result)
        {
            var comp = product.TryGetComp<CompUniversalBook>();
            if (comp == null || worker == null) return true;
            float knowledge = LearningRules.Knowledge(worker.skills.GetSkill(SkillDefOf.Intellectual).Level,
                worker.skills.skills.Where(s => !s.TotallyDisabled).Select(s => (float)s.Level));
            // A normal distribution keeps rare masterpieces while raising the mean with knowledge.
            int category = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(Rand.Gaussian(knowledge * 0.23f - 0.4f, 1f)),
                0, LearningRules.QualityCap(knowledge));
            comp.authorName = worker.LabelShortCap;
            comp.authorKnowledge = knowledge;
            comp.writtenAt = GenTicks.TicksAbs;
            product.TryGetComp<CompQuality>().SetQuality((QualityCategory)category, ArtGenerationContext.Colony);
            ((Book)product).GenerateBook(worker, comp.writtenAt);
            QualityUtility.SendCraftNotification(product, worker);
            if (worker.Ideo != null) product.StyleDef = worker.Ideo.GetStyleFor(product.def);
            __result = product;
            return false;
        }
    }
}
