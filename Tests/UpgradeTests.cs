using Xunit;
using System.Collections.Generic;
using CoupleBeds.Core;

namespace CoupleBeds.Tests
{
    /// A couple that already shares a bed is only moved when the new bed beats the
    /// current one by more than UpgradeMargin. The current bed keeps the
    /// "already owned" bonus while it is theirs, which is what stops ping-pong.
    public class UpgradeTests
    {
        private sealed class Sharing
        {
            public Colony Colony;
            public PawnView A;
            public PawnView B;
            public BedView Current;
        }

        private static Sharing CoupleSharing(float currentImpressiveness)
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView current = colony.AddPrivateDoubleBed(impressiveness: currentImpressiveness);
            colony.Owns(a, current);
            colony.Owns(b, current);
            return new Sharing { Colony = colony, A = a, B = b, Current = current };
        }

        [Fact]
        public void AlreadyInTheBestBed_NothingHappens()
        {
            Sharing s = CoupleSharing(currentImpressiveness: 100f);
            s.Colony.AddPrivateDoubleBed(impressiveness: 10f);

            Assert.Empty(s.Colony.Plan());
        }

        [Fact]
        public void OnlyOneBedExists_NothingHappens()
        {
            Sharing s = CoupleSharing(currentImpressiveness: 50f);

            Assert.Empty(s.Colony.Plan());
        }

        [Fact]
        public void UpgradesDisabled_NothingHappens()
        {
            Sharing s = CoupleSharing(currentImpressiveness: 0f);
            s.Colony.AddPrivateDoubleBed(impressiveness: 1000f);

            Assert.Empty(s.Colony.Plan(new PlannerSettings { AllowUpgrade = false }));
        }

        [Fact]
        public void BetterBedWithinTheMargin_NothingHappens()
        {
            // current = 0 impressiveness + 40 private + 5 owned = 45
            // candidate = 50 impressiveness + 40 private = 90 -> +45, margin 50
            Sharing s = CoupleSharing(currentImpressiveness: 0f);
            s.Colony.AddPrivateDoubleBed(impressiveness: 50f);

            Assert.Empty(s.Colony.Plan(new PlannerSettings { UpgradeMargin = 50f }));
        }

        [Fact]
        public void BetterBedBeyondTheMargin_TheyMove()
        {
            Sharing s = CoupleSharing(currentImpressiveness: 0f);
            BedView better = s.Colony.AddPrivateDoubleBed(impressiveness: 200f);

            PlanAssert.AssignedTo(s.Colony.Plan(new PlannerSettings { UpgradeMargin = 15f }), s.A, s.B, better);
        }

        [Fact]
        public void MarginIsMetExactly_TheyMove()
        {
            // The check is `bestScore < current + margin`, so an exact tie moves them.
            // current = 40 + 5 = 45; candidate = 60 + 40 = 100; difference 55.
            Sharing s = CoupleSharing(currentImpressiveness: 0f);
            BedView better = s.Colony.AddPrivateDoubleBed(impressiveness: 60f);

            PlanAssert.AssignedTo(s.Colony.Plan(new PlannerSettings { UpgradeMargin = 55f }), s.A, s.B, better);
        }

        [Fact]
        public void MarginIsMissedByATouch_NothingHappens()
        {
            Sharing s = CoupleSharing(currentImpressiveness: 0f);
            s.Colony.AddPrivateDoubleBed(impressiveness: 60f);

            Assert.Empty(s.Colony.Plan(new PlannerSettings { UpgradeMargin = 55.01f }));
        }

        [Fact]
        public void UpgradeIsMarkedAsSuch()
        {
            Sharing s = CoupleSharing(currentImpressiveness: 0f);
            s.Colony.AddPrivateDoubleBed(impressiveness: 200f);

            BedAssignment assignment = PlanAssert.Single(s.Colony.Plan());
            Assert.True(assignment.IsUpgrade);
            Assert.Equal(s.Current.Id, assignment.PreviousSharedBedId);
        }

        [Fact]
        public void OneMemberMissingFromTheBed_CountsAsNotSharing_AndTheyAreRejoined()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView hers = colony.AddPrivateDoubleBed(impressiveness: 10f);
            colony.Owns(a, hers);

            // Not an upgrade: they were not sharing, so the margin never applies.
            BedAssignment assignment = PlanAssert.Single(colony.Plan(new PlannerSettings { UpgradeMargin = 1000f }));
            Assert.Equal(hers.Id, assignment.BedId);
            Assert.False(assignment.IsUpgrade);
        }

        [Fact]
        public void CurrentBedTurnedMedical_IsStillScoredForTheComparison()
        {
            // A medical bed drops out of the candidate list but must still be
            // scored as the incumbent, otherwise the margin is applied against
            // nothing and the couple moves on the slightest improvement.
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView current = colony.AddBed(bed =>
            {
                bed.SleepingSlots = 2;
                bed.RoomId = 1;
                bed.RoomImpressiveness = 300f;
                bed.Medical = true;
            });
            colony.Owns(a, current);
            colony.Owns(b, current);
            colony.AddPrivateDoubleBed(impressiveness: 20f);

            // incumbent = 300 + 40 + 5 = 345, candidate = 20 + 40 = 60 -> no move
            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void RunningTwice_IsStable()
        {
            // Guards against ping-pong: after moving, the "already owned" bonus
            // follows the couple, so the bed they left must not win it back.
            Sharing s = CoupleSharing(currentImpressiveness: 0f);
            BedView better = s.Colony.AddPrivateDoubleBed(impressiveness: 200f);

            List<BedAssignment> first = s.Colony.Plan();
            PlanAssert.AssignedTo(first, s.A, s.B, better);

            // Apply the move to the snapshot and plan again.
            s.Current.OwnerIds.Clear();
            better.OwnerIds.Add(s.A.Id);
            better.OwnerIds.Add(s.B.Id);
            s.A.OwnedBedId = better.Id;
            s.B.OwnedBedId = better.Id;

            Assert.Empty(s.Colony.Plan());
        }

        [Fact]
        public void BetterBedOwnedBySomebodyElse_NoUpgrade()
        {
            Sharing s = CoupleSharing(currentImpressiveness: 0f);
            BedView grand = s.Colony.AddPrivateDoubleBed(impressiveness: 500f);
            s.Colony.OwnedByStranger(grand);

            Assert.Empty(s.Colony.Plan());
        }

        [Fact]
        public void UnreachableBetterBed_NoUpgrade()
        {
            Sharing s = CoupleSharing(currentImpressiveness: 0f);
            BedView grand = s.Colony.AddPrivateDoubleBed(impressiveness: 500f);

            Assert.Empty(s.Colony.Plan(access: new FakeBedAccess().Unreachable(s.B, grand)));
        }
    }
}
