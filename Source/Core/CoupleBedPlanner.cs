using System.Collections.Generic;

namespace CoupleBeds.Core
{
    /// Decides which couples move into which beds. Pure: it reads a snapshot and
    /// returns a list of assignments for the caller to carry out.
    public static class CoupleBedPlanner
    {
        public static List<BedAssignment> Plan(ColonySnapshot snapshot, PlannerSettings settings, IBedAccess access)
        {
            List<BedAssignment> plan = new List<BedAssignment>();
            if (snapshot == null || settings == null || access == null) return plan;

            Dictionary<int, PawnView> pawnsById = new Dictionary<int, PawnView>();
            for (int i = 0; i < snapshot.Pawns.Count; i++)
                pawnsById[snapshot.Pawns[i].Id] = snapshot.Pawns[i];

            // Live ownership, so a bed claimed by the first couple is off limits to
            // the next one in the same pass.
            Dictionary<int, List<int>> owners = new Dictionary<int, List<int>>();
            Dictionary<int, int> ownedBed = new Dictionary<int, int>();
            for (int i = 0; i < snapshot.Beds.Count; i++)
            {
                BedView bed = snapshot.Beds[i];
                List<int> list = new List<int>(bed.OwnerIds ?? new List<int>());
                owners[bed.Id] = list;
                for (int j = 0; j < list.Count; j++) ownedBed[list[j]] = bed.Id;
            }
            // A pawn's own record wins if the two ever disagree.
            for (int i = 0; i < snapshot.Pawns.Count; i++)
            {
                PawnView p = snapshot.Pawns[i];
                if (p.OwnedBedId != PawnView.NoBed) ownedBed[p.Id] = p.OwnedBedId;
            }

            HashSet<int> handled = new HashSet<int>();
            List<BedView> candidates = null;   // collected lazily, only if a couple needs it

            for (int i = 0; i < snapshot.Pawns.Count; i++)
            {
                PawnView a = snapshot.Pawns[i];
                if (handled.Contains(a.Id)) continue;

                int partnerId = PartnerMatcher.PartnerOf(a, settings);
                if (partnerId == PawnView.NoPawn || handled.Contains(partnerId)) continue;

                PawnView b;
                if (!pawnsById.TryGetValue(partnerId, out b)) continue;      // partner is off this map
                if (PartnerMatcher.PartnerOf(b, settings) != a.Id) continue; // must be mutual

                handled.Add(a.Id);
                handled.Add(b.Id);

                if (!Eligibility.IsEligible(a) || !Eligibility.IsEligible(b)) continue;
                if (a.IsSlave != b.IsSlave) continue;   // slave and colonist beds are different

                if (candidates == null) candidates = CollectDoubleBeds(snapshot);

                BedAssignment assignment = PlanCouple(a, b, candidates, snapshot, settings, access, owners, ownedBed);
                if (assignment != null) plan.Add(assignment);
            }

            return plan;
        }

        /// Beds that could ever host a couple, before any couple-specific checks.
        public static List<BedView> CollectDoubleBeds(ColonySnapshot snapshot)
        {
            List<BedView> result = new List<BedView>();
            for (int i = 0; i < snapshot.Beds.Count; i++)
            {
                BedView bed = snapshot.Beds[i];
                if (bed == null) continue;
                if (bed.Medical || bed.ForPrisoners) continue;
                if (bed.SleepingSlots < 2) continue;
                if (!bed.Humanlike) continue;
                result.Add(bed);
            }
            return result;
        }

        private static BedAssignment PlanCouple(
            PawnView a, PawnView b, List<BedView> candidates, ColonySnapshot snapshot,
            PlannerSettings settings, IBedAccess access,
            Dictionary<int, List<int>> owners, Dictionary<int, int> ownedBed)
        {
            int bedAId = OwnedBedOf(ownedBed, a.Id);
            int bedBId = OwnedBedOf(ownedBed, b.Id);
            bool alreadySharing = bedAId != PawnView.NoBed && bedAId == bedBId;
            if (alreadySharing && !settings.AllowUpgrade) return null;

            BedView best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                BedView bed = candidates[i];
                if (!Usable(bed, a, b, owners, access)) continue;
                float score = BedScorer.Score(bed, a.Id, b.Id, owners);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = bed;
                }
            }
            if (best == null) return null;

            if (alreadySharing)
            {
                if (best.Id == bedAId) return null;
                // The current bed is scored even if it is no longer a candidate
                // (it may have been turned into a medical bed since).
                BedView current = snapshot.Bed(bedAId);
                if (current != null)
                {
                    float currentScore = BedScorer.Score(current, a.Id, b.Id, owners);
                    if (bestScore < currentScore + settings.UpgradeMargin) return null;
                }
            }

            Claim(owners, ownedBed, best.Id, a.Id);
            Claim(owners, ownedBed, best.Id, b.Id);

            return new BedAssignment
            {
                PawnAId = a.Id,
                PawnBId = b.Id,
                BedId = best.Id,
                PreviousSharedBedId = alreadySharing ? bedAId : PawnView.NoBed,
            };
        }

        /// Checks are ordered cheapest first; the two CanReach calls are last
        /// because they are by far the most expensive thing here.
        public static bool Usable(BedView bed, PawnView a, PawnView b,
                                  IDictionary<int, List<int>> owners, IBedAccess access)
        {
            if (bed.Medical || bed.ForPrisoners) return false;
            if (bed.SleepingSlots < 2) return false;

            BedOwner wanted = a.IsSlave ? BedOwner.Slave : BedOwner.Colonist;
            if (bed.ForOwnerType != wanted) return false;

            // Never evict anyone who is not part of this couple.
            List<int> current;
            if (owners.TryGetValue(bed.Id, out current) && current != null)
            {
                for (int i = 0; i < current.Count; i++)
                    if (current[i] != a.Id && current[i] != b.Id) return false;
            }

            if (!access.CanUseBedEver(a.Id, bed.Id) || !access.CanUseBedEver(b.Id, bed.Id)) return false;
            if (access.IsForbidden(a.Id, bed.Id) || access.IsForbidden(b.Id, bed.Id)) return false;
            if (!access.CanReach(a.Id, bed.Id)) return false;
            if (!access.CanReach(b.Id, bed.Id)) return false;
            return true;
        }

        private static int OwnedBedOf(Dictionary<int, int> ownedBed, int pawnId)
        {
            int bedId;
            return ownedBed.TryGetValue(pawnId, out bedId) ? bedId : PawnView.NoBed;
        }

        /// Mirrors Pawn_Ownership.ClaimBedIfNonMedical: the pawn releases its old
        /// bed, then takes a slot in the new one.
        private static void Claim(Dictionary<int, List<int>> owners, Dictionary<int, int> ownedBed, int bedId, int pawnId)
        {
            if (BedScorer.IsOwner(owners, bedId, pawnId)) return;

            int oldBedId;
            List<int> oldOwners;
            if (ownedBed.TryGetValue(pawnId, out oldBedId) && oldBedId != PawnView.NoBed
                && owners.TryGetValue(oldBedId, out oldOwners) && oldOwners != null)
            {
                oldOwners.Remove(pawnId);
            }

            List<int> list;
            if (!owners.TryGetValue(bedId, out list) || list == null)
            {
                list = new List<int>();
                owners[bedId] = list;
            }
            list.Add(pawnId);
            ownedBed[pawnId] = bedId;
        }
    }
}
