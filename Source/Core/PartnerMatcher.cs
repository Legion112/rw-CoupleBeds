using System.Collections.Generic;

namespace CoupleBeds.Core
{
    /// Picks the one partner a pawn should be bedded with.
    ///
    /// This reproduces RimWorld's LovePartnerRelationUtility.ExistingMostLikedLovePartner:
    /// the highest-opinion love relation wins, ties go to whichever comes first in
    /// DirectRelations, and dead partners are skipped.
    public static class PartnerMatcher
    {
        /// Returns the partner's id, or PawnView.NoPawn.
        public static int PartnerOf(PawnView pawn, PlannerSettings settings)
        {
            if (pawn == null || pawn.LovePartners == null) return PawnView.NoPawn;

            bool found = false;
            LoveRelation best = default(LoveRelation);
            int bestOpinion = 0;

            List<LoveRelation> relations = pawn.LovePartners;
            for (int i = 0; i < relations.Count; i++)
            {
                LoveRelation r = relations[i];
                if (r.PartnerDead) continue;
                // Strictly greater, so the first relation wins a tie - same as the game.
                if (!found || r.Opinion > bestOpinion)
                {
                    found = true;
                    best = r;
                    bestOpinion = r.Opinion;
                }
            }

            if (!found) return PawnView.NoPawn;

            // NOTE: the spouse filter is applied *after* the most-liked partner has
            // been chosen, exactly like the mod's original GetPartner. A pawn whose
            // most-liked partner is a lover is therefore skipped entirely when
            // "spouses only" is set, even if they also have a spouse.
            if (!settings.IncludeLovers && !best.IsSpouse) return PawnView.NoPawn;

            return best.PartnerId;
        }

        /// True when a and b each name the other as their most-liked partner.
        public static bool AreMutualPartners(PawnView a, PawnView b, PlannerSettings settings)
        {
            if (a == null || b == null) return false;
            return PartnerOf(a, settings) == b.Id && PartnerOf(b, settings) == a.Id;
        }
    }

    /// Whether a pawn is a candidate for automatic bed assignment at all.
    public static class Eligibility
    {
        public static bool IsEligible(PawnView pawn, PlannerSettings settings)
        {
            if (pawn == null || settings == null) return false;
            if (pawn.Dead || pawn.Downed) return false;
            if (!pawn.HasOwnership) return false;
            if (!pawn.Humanlike) return false;
            // A pawn with no rest need never sleeps, but RimWorld still applies
            // the "sleeping alone" thought to them - the ThoughtDef has no gene
            // or need gate - so they are worth bedding unless the player says
            // otherwise and would rather keep the bed free.
            if (!pawn.HasRestNeed && !settings.ManageSleepless) return false;
            if (pawn.HasDeathrestGene) return false;      // do not break deathrest caskets
            return true;
        }
    }
}
