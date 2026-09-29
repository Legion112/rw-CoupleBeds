using Xunit;
using System;
using System.Collections.Generic;
using CoupleBeds.Core;

namespace CoupleBeds.Tests
{
    /// Builds ColonySnapshots readably. Ids are assigned in creation order, and
    /// pawn order in the snapshot is the order pawns were added - which is what
    /// decides who gets first pick of a bed.
    public sealed class Colony
    {
        private int nextPawnId = 100;
        private int nextBedId = 900;
        private int nextRoomId = 1;

        public ColonySnapshot Snapshot { get; } = new ColonySnapshot();

        public PawnView AddPawn(string label = null, Action<PawnView> configure = null)
        {
            PawnView pawn = new PawnView { Id = nextPawnId++ };
            pawn.Label = label ?? ("pawn" + pawn.Id);
            configure?.Invoke(pawn);
            Snapshot.Pawns.Add(pawn);
            return pawn;
        }

        /// A pawn that is not on this map: it gets an id and can be named as a
        /// love partner, but never enters the snapshot.
        public PawnView OffMapPawn(string label = null)
        {
            return new PawnView { Id = nextPawnId++, Label = label ?? "offmap" };
        }

        public BedView AddBed(Action<BedView> configure = null)
        {
            BedView bed = new BedView { Id = nextBedId++ };
            configure?.Invoke(bed);
            Snapshot.Beds.Add(bed);
            return bed;
        }

        /// A 2-slot bed alone in its own room. The common good case.
        public BedView AddPrivateDoubleBed(float impressiveness = 0f, float comfort = 0f)
        {
            int room = nextRoomId++;
            return AddBed(b =>
            {
                b.SleepingSlots = 2;
                b.RoomId = room;
                b.RoomImpressiveness = impressiveness;
                b.Comfort = comfort;
                b.OtherNonMedicalBedsInRoom = 0;
            });
        }

        /// A 2-slot bed sharing a room with <paramref name="otherBeds"/> other beds.
        public BedView AddBarracksDoubleBed(int otherBeds = 2, float impressiveness = 0f, float comfort = 0f)
        {
            int room = nextRoomId++;
            return AddBed(b =>
            {
                b.SleepingSlots = 2;
                b.RoomId = room;
                b.RoomImpressiveness = impressiveness;
                b.Comfort = comfort;
                b.OtherNonMedicalBedsInRoom = otherBeds;
            });
        }

        /// Makes a and b each other's most-liked (and only) love partner.
        public void Marry(PawnView a, PawnView b, int opinion = 80, bool spouse = true)
        {
            a.LovePartners.Add(new LoveRelation(b.Id, spouse, opinion));
            b.LovePartners.Add(new LoveRelation(a.Id, spouse, opinion));
        }

        /// A one-directional love relation, for polygamy and unrequited cases.
        public void LovesOneWay(PawnView from, PawnView to, int opinion, bool spouse = true)
        {
            from.LovePartners.Add(new LoveRelation(to.Id, spouse, opinion));
        }

        public void Owns(PawnView pawn, BedView bed)
        {
            pawn.OwnedBedId = bed.Id;
            if (!bed.OwnerIds.Contains(pawn.Id)) bed.OwnerIds.Add(pawn.Id);
        }

        /// A bed owned by somebody who is not in the snapshot at all.
        public void OwnedByStranger(BedView bed, int strangerId = 555)
        {
            if (!bed.OwnerIds.Contains(strangerId)) bed.OwnerIds.Add(strangerId);
        }

        public List<BedAssignment> Plan(PlannerSettings settings = null, IBedAccess access = null)
        {
            return CoupleBedPlanner.Plan(Snapshot, settings ?? new PlannerSettings(), access ?? new FakeBedAccess());
        }
    }

    /// Permissive by default; individual facts can be denied, and every call is
    /// counted so tests can assert that the expensive check stays rare.
    public sealed class FakeBedAccess : IBedAccess
    {
        private readonly HashSet<(int, int)> unreachable = new HashSet<(int, int)>();
        private readonly HashSet<(int, int)> forbidden = new HashSet<(int, int)>();
        private readonly HashSet<(int, int)> unusable = new HashSet<(int, int)>();

        public int CanUseBedEverCalls { get; private set; }
        public int IsForbiddenCalls { get; private set; }
        public int CanReachCalls { get; private set; }

        public readonly List<(int PawnId, int BedId)> CanReachArgs = new List<(int, int)>();

        public FakeBedAccess Unreachable(PawnView pawn, BedView bed)
        {
            unreachable.Add((pawn.Id, bed.Id));
            return this;
        }

        public FakeBedAccess Forbidden(PawnView pawn, BedView bed)
        {
            forbidden.Add((pawn.Id, bed.Id));
            return this;
        }

        public FakeBedAccess CannotUseEver(PawnView pawn, BedView bed)
        {
            unusable.Add((pawn.Id, bed.Id));
            return this;
        }

        public bool CanUseBedEver(int pawnId, int bedId)
        {
            CanUseBedEverCalls++;
            return !unusable.Contains((pawnId, bedId));
        }

        public bool IsForbidden(int pawnId, int bedId)
        {
            IsForbiddenCalls++;
            return forbidden.Contains((pawnId, bedId));
        }

        public bool CanReach(int pawnId, int bedId)
        {
            CanReachCalls++;
            CanReachArgs.Add((pawnId, bedId));
            return !unreachable.Contains((pawnId, bedId));
        }
    }

    internal static class PlanAssert
    {
        /// The single assignment in the plan, or a clear failure.
        public static BedAssignment Single(List<BedAssignment> plan)
        {
            Assert.Single(plan);
            return plan[0];
        }

        public static void AssignedTo(List<BedAssignment> plan, PawnView a, PawnView b, BedView bed)
        {
            BedAssignment assignment = Single(plan);
            Assert.Equal(bed.Id, assignment.BedId);
            Assert.Equal(
                new HashSet<int> { a.Id, b.Id },
                new HashSet<int> { assignment.PawnAId, assignment.PawnBId });
        }
    }
}
