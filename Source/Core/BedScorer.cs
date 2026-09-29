using System.Collections.Generic;

namespace CoupleBeds.Core
{
    /// Ranks beds for a couple. Higher is better.
    ///
    /// The weights mirror what actually drives sleep-related mood in game: a
    /// private bedroom instead of a barracks, an impressive room, a comfortable
    /// bed. The numbers are the mod's own, not the game's.
    public static class BedScorer
    {
        public const float OutdoorsOrNoRoomPenalty = -100f;
        public const float PrivateRoomBonus = 40f;
        public const float PenaltyPerOtherBed = -10f;
        public const float ComfortWeight = 20f;
        public const float AlreadyOwnedBonus = 5f;

        /// <paramref name="owners"/> is the live ownership map, so a bed one of the
        /// couple already holds scores slightly higher and the couple stays put.
        public static float Score(BedView bed, int pawnAId, int pawnBId, IDictionary<int, List<int>> owners)
        {
            float score = 0f;

            if (bed.RoomId == BedView.NoRoom || bed.RoomOutdoors)
            {
                score += OutdoorsOrNoRoomPenalty;
            }
            else
            {
                score += bed.RoomImpressiveness;
                if (bed.OtherNonMedicalBedsInRoom == 0)
                    score += PrivateRoomBonus;                                   // no "slept in barracks"
                else
                    score += PenaltyPerOtherBed * bed.OtherNonMedicalBedsInRoom;
            }

            score += bed.Comfort * ComfortWeight;

            if (IsOwner(owners, bed.Id, pawnAId) || IsOwner(owners, bed.Id, pawnBId))
                score += AlreadyOwnedBonus;                                      // less shuffling

            return score;
        }

        internal static bool IsOwner(IDictionary<int, List<int>> owners, int bedId, int pawnId)
        {
            List<int> list;
            if (owners == null || !owners.TryGetValue(bedId, out list) || list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == pawnId) return true;
            return false;
        }
    }
}
