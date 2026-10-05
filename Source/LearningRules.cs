using System;
using System.Collections.Generic;
using System.Linq;

namespace TalentLibrary
{
    public struct SkillState
    {
        public readonly int Id, Passion, Level;
        public readonly bool Disabled;
        public readonly float Remaining, XPToday;
        public SkillState(int id, int passion, int level, bool disabled, float remaining, float xpToday = 0f)
        { Id = id; Passion = passion; Level = level; Disabled = disabled; Remaining = remaining; XPToday = xpToday; }
    }

    public struct Award
    {
        public readonly int Id;
        public readonly float XP;
        public Award(int id, float xp) { Id = id; XP = xp; }
    }

    // Pure policy, shared by gameplay and the executable test suite.
    public static class LearningRules
    {
        public static int SelectStage(IEnumerable<SkillState> skills)
        {
            var eligible = skills.Where(s => !s.Disabled).ToArray();
            if (eligible.Any(s => s.Passion == 2 && s.Level < 18)) return 2;
            if (eligible.Any(s => s.Passion == 1 && s.Level < 18)) return 1;
            return eligible.Any(s => s.Level < 10) ? 0 : -1;
        }

        public static List<Award> Allocate(IEnumerable<SkillState> skills, float qualityMultiplier,
            int randomIndex, float generalPool)
        {
            var states = skills.ToArray();
            int stage = SelectStage(states);
            var result = new List<Award>();
            if (stage < 0) return result;
            var candidates = states.Where(s => !s.Disabled &&
                (stage == 0 ? s.Level < 10 : s.Passion == stage && s.Level < 18)).ToArray();
            if (stage == 0)
            {
                float share = Math.Max(15f, Math.Min(20f, generalPool)) * qualityMultiplier / candidates.Length;
                foreach (var s in candidates) result.Add(new Award(s.Id, Math.Min(share * DailyFactor(s.XPToday), Math.Max(0f, s.Remaining))));
            }
            else
            {
                var s = candidates[(randomIndex & int.MaxValue) % candidates.Length];
                float xp = (stage == 2 ? 20f : 15f) * qualityMultiplier * DailyFactor(s.XPToday);
                result.Add(new Award(s.Id, Math.Min(xp, Math.Max(0f, s.Remaining))));
            }
            return result;
        }

        public static float Knowledge(float intellectual, IEnumerable<float> skills)
        {
            var top = skills.OrderByDescending(x => x).Take(4).ToArray();
            return Math.Max(0f, Math.Min(20f, 0.5f * intellectual + 0.5f * (top.Length == 0 ? 0f : top.Average())));
        }

        public static int QualityCap(float knowledge)
        {
            if (knowledge >= 18f) return 6;
            if (knowledge >= 15f) return 5;
            if (knowledge >= 12f) return 4;
            if (knowledge >= 8f) return 3;
            if (knowledge >= 4f) return 2;
            return 1;
        }

        public static float QualityMultiplier(int quality) { return 0.5f + Math.Max(0, Math.Min(6, quality)) * 0.25f; }
        public static float DailyFactor(float xpToday) { return xpToday >= 4000f ? 0.2f : 1f; }
    }
}
