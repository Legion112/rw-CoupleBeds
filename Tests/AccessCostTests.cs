using Xunit;
using System.Collections.Generic;
using System.Linq;
using CoupleBeds.Core;

namespace CoupleBeds.Tests
{
    /// CanReach is a pathfinding query and by far the most expensive thing the
    /// planner does, so these lock in how often it can happen. The planner runs
    /// inside MapComponentTick, so a regression here is a frame hitch.
    public class AccessCostTests
    {
        [Fact]
        public void NoCouples_NoAccessCallsAtAll()
        {
            Colony colony = new Colony();
            colony.AddPawn("loner1");
            colony.AddPawn("loner2");
            for (int i = 0; i < 10; i++) colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess();
            colony.Plan(access: access);

            Assert.Equal(0, access.CanReachCalls);
            Assert.Equal(0, access.IsForbiddenCalls);
            Assert.Equal(0, access.CanUseBedEverCalls);
        }

        [Fact]
        public void IneligibleCouple_NoAccessCalls()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo", p => p.Downed = true);
            colony.Marry(a, b);
            for (int i = 0; i < 10; i++) colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess();
            colony.Plan(access: access);

            Assert.Equal(0, access.CanReachCalls);
        }

        [Fact]
        public void BedsRejectedByCheapChecks_NeverReachThePathfinder()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);

            colony.AddBed(bed => { bed.SleepingSlots = 1; bed.RoomId = 1; });                        // too small
            colony.AddBed(bed => { bed.SleepingSlots = 2; bed.RoomId = 2; bed.Medical = true; });    // medical
            colony.AddBed(bed => { bed.SleepingSlots = 2; bed.RoomId = 3; bed.ForPrisoners = true; });
            colony.AddBed(bed => { bed.SleepingSlots = 2; bed.RoomId = 4; bed.Humanlike = false; }); // animal
            colony.AddBed(bed => { bed.SleepingSlots = 2; bed.RoomId = 5; bed.ForOwnerType = BedOwner.Slave; });
            BedView taken = colony.AddPrivateDoubleBed();
            colony.OwnedByStranger(taken);
            BedView good = colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess();
            colony.Plan(access: access);

            // Only the one usable bed is ever pathed to, once per partner.
            Assert.Equal(2, access.CanReachCalls);
            Assert.All(access.CanReachArgs, arg => Assert.Equal(good.Id, arg.BedId));
        }

        [Fact]
        public void OwnerCheckHappensBeforeAnyAccessCall()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            for (int i = 0; i < 5; i++) colony.OwnedByStranger(colony.AddPrivateDoubleBed(), strangerId: 700 + i);

            FakeBedAccess access = new FakeBedAccess();
            colony.Plan(access: access);

            Assert.Equal(0, access.CanUseBedEverCalls);
            Assert.Equal(0, access.IsForbiddenCalls);
            Assert.Equal(0, access.CanReachCalls);
        }

        [Fact]
        public void ForbiddenIsCheckedBeforeReachability()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView bed = colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess().Forbidden(a, bed);
            colony.Plan(access: access);

            Assert.Equal(0, access.CanReachCalls);
        }

        [Fact]
        public void CanUseBedEverIsCheckedBeforeForbidden()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView bed = colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess().CannotUseEver(a, bed);
            colony.Plan(access: access);

            Assert.Equal(0, access.IsForbiddenCalls);
            Assert.Equal(0, access.CanReachCalls);
        }

        [Fact]
        public void SecondPartnerIsNotPathedWhenTheFirstCannotReach()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView bed = colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess().Unreachable(a, bed);
            colony.Plan(access: access);

            Assert.Equal(1, access.CanReachCalls);
            Assert.Equal(a.Id, access.CanReachArgs[0].PawnId);
        }

        [Fact]
        public void EachCandidateIsPathedAtMostOncePerPartner()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            for (int i = 0; i < 6; i++) colony.AddPrivateDoubleBed(impressiveness: i);

            FakeBedAccess access = new FakeBedAccess();
            colony.Plan(access: access);

            Assert.Equal(access.CanReachArgs.Count, access.CanReachArgs.Distinct().Count());
            Assert.Equal(12, access.CanReachCalls);   // 6 beds x 2 partners
        }

        [Fact]
        public void LargeColony_PathfindingStaysWithinCouplesTimesBedsTimesTwo()
        {
            // 20 couples, 40 double beds. This is the worst realistic case and the
            // reason the whole pass should eventually be spread over several ticks.
            const int couples = 20;
            const int beds = 40;

            Colony colony = new Colony();
            List<PawnView> pawns = new List<PawnView>();
            for (int i = 0; i < couples * 2; i++) pawns.Add(colony.AddPawn("p" + i));
            for (int i = 0; i < couples * 2; i += 2) colony.Marry(pawns[i], pawns[i + 1]);
            for (int i = 0; i < beds; i++) colony.AddPrivateDoubleBed(impressiveness: i);

            FakeBedAccess access = new FakeBedAccess();
            List<BedAssignment> plan = colony.Plan(access: access);

            Assert.Equal(couples, plan.Count);
            Assert.True(access.CanReachCalls <= couples * beds * 2,
                "CanReach calls: " + access.CanReachCalls);

            // Beds claimed by earlier couples drop out on the cheap owner check, so
            // the real count is well under the bound. Locked in to catch regressions.
            Assert.Equal(1220, access.CanReachCalls);
        }

        [Fact]
        public void BedsAreCollectedOnlyOncePerPass()
        {
            // Not directly observable, but a couple that finds no usable bed must
            // not make the next couple redo the expensive work either: the total
            // path count stays proportional to couples x beds, not couples^2.
            Colony colony = new Colony();
            List<PawnView> pawns = new List<PawnView>();
            for (int i = 0; i < 6; i++) pawns.Add(colony.AddPawn("p" + i));
            for (int i = 0; i < 6; i += 2) colony.Marry(pawns[i], pawns[i + 1]);
            BedView only = colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess();
            colony.Plan(access: access);

            // First couple: 2 paths and takes the bed. The other two couples find
            // it owned and stop at the cheap check.
            Assert.Equal(2, access.CanReachCalls);
            Assert.All(access.CanReachArgs, arg => Assert.Equal(only.Id, arg.BedId));
        }
    }
}
