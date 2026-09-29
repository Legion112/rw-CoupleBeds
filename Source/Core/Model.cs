// Plain data describing a colony, with no reference to RimWorld's assemblies.
//
// Everything in CoupleBeds.Core is deliberately free of game types so it can be
// compiled into the net472 mod assembly *and* into a net9.0 test project.
// Verse.Pawn / RimWorld.Building_Bed cannot be constructed outside a running
// game (they need DefDatabase, Find.*, and Unity), so the decision logic works
// on these snapshots instead and the adapter in CoupleBeds.cs fills them in.

using System.Collections.Generic;

namespace CoupleBeds.Core
{
    /// Which pawns a bed may be assigned to. Mirrors RimWorld.BedOwnerType.
    public enum BedOwner
    {
        Colonist = 0,
        Prisoner = 1,
        Slave = 2,
    }

    /// One love relation as seen from a pawn. Mirrors a DirectPawnRelation whose
    /// def satisfies LovePartnerRelationUtility.IsLovePartnerRelation.
    public struct LoveRelation
    {
        public int PartnerId;
        public bool IsSpouse;    // Spouse, as opposed to lover/fiancé
        public int Opinion;      // this pawn's opinion of the partner
        public bool PartnerDead;

        public LoveRelation(int partnerId, bool isSpouse, int opinion, bool partnerDead = false)
        {
            PartnerId = partnerId;
            IsSpouse = isSpouse;
            Opinion = opinion;
            PartnerDead = partnerDead;
        }
    }

    /// A colonist on the map being processed. Only spawned pawns are snapshotted,
    /// so "is on this map" is the same as "appears in ColonySnapshot.Pawns".
    public sealed class PawnView
    {
        public const int NoPawn = -1;
        public const int NoBed = -1;

        public int Id;
        public string Label = "";

        public bool Dead;
        public bool Downed;
        public bool Humanlike = true;
        public bool HasOwnership = true;
        public bool HasRestNeed = true;
        public bool HasDeathrestGene;
        public bool IsSlave;

        public int OwnedBedId = NoBed;
        public List<LoveRelation> LovePartners = new List<LoveRelation>();
    }

    /// A bed on the map. Room facts are flattened in because a Room cannot be
    /// built outside the game either.
    public sealed class BedView
    {
        public const int NoRoom = -1;

        public int Id;
        public bool Medical;
        public bool ForPrisoners;
        public bool Humanlike = true;          // def.building.bed_humanlike
        public int SleepingSlots = 2;
        public BedOwner ForOwnerType = BedOwner.Colonist;
        public float Comfort;

        public int RoomId = NoRoom;
        public bool RoomOutdoors;              // Room.PsychologicallyOutdoors
        public float RoomImpressiveness;
        public int OtherNonMedicalBedsInRoom;  // excludes this bed

        public List<int> OwnerIds = new List<int>();
    }

    /// One map, as the planner sees it. Pawns are in map iteration order, which
    /// decides which couple gets first pick when two couples want the same bed.
    public sealed class ColonySnapshot
    {
        public List<PawnView> Pawns = new List<PawnView>();
        public List<BedView> Beds = new List<BedView>();

        public PawnView Pawn(int id)
        {
            for (int i = 0; i < Pawns.Count; i++)
                if (Pawns[i].Id == id) return Pawns[i];
            return null;
        }

        public BedView Bed(int id)
        {
            for (int i = 0; i < Beds.Count; i++)
                if (Beds[i].Id == id) return Beds[i];
            return null;
        }
    }

    /// The subset of CoupleBedsSettings the planner cares about.
    public sealed class PlannerSettings
    {
        public bool IncludeLovers = true;
        public bool AllowUpgrade = true;
        /// Manage pawns with no rest need (the Neversleep gene, the body mastery
        /// trait, void touched, a circadian half-cycler). They never sleep, but
        /// the game still gives them the "sleeping alone" mood penalty, so by
        /// default they are bedded like anyone else.
        public bool ManageSleepless = true;
        public float UpgradeMargin = 15f;
    }

    /// One decision for the adapter to carry out.
    public sealed class BedAssignment
    {
        public int PawnAId;
        public int PawnBId;
        public int BedId;
        /// The bed the couple already shared, or PawnView.NoBed if they did not.
        public int PreviousSharedBedId = PawnView.NoBed;

        public bool IsUpgrade { get { return PreviousSharedBedId != PawnView.NoBed; } }
    }

    /// Facts that are too expensive (or too stateful) to snapshot eagerly.
    /// The planner calls these only for beds that survive the cheap filters, so a
    /// fake can count the calls and prove the expensive one stays rare.
    public interface IBedAccess
    {
        /// RestUtility.CanUseBedEver: body size, humanlike, deathrest caskets.
        bool CanUseBedEver(int pawnId, int bedId);

        /// ForbidUtility.IsForbidden.
        bool IsForbidden(int pawnId, int bedId);

        /// Pawn.CanReach. The expensive one.
        bool CanReach(int pawnId, int bedId);
    }
}
