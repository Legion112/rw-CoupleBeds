using Xunit;
using System.Collections.Generic;
using System.Linq;
using CoupleBeds.Core;

namespace CoupleBeds.Tests
{
    /// Several couples in one pass must not be handed the same bed, and the
    /// outcome must not depend on anything but the snapshot.
    public class MultiCoupleTests
    {
        [Fact]
        public void TwoCouples_GetDifferentBeds()
        {
            Colony colony = new Colony();
            PawnView a1 = colony.AddPawn("a1");
            PawnView b1 = colony.AddPawn("b1");
            PawnView a2 = colony.AddPawn("a2");
            PawnView b2 = colony.AddPawn("b2");
            colony.Marry(a1, b1);
            colony.Marry(a2, b2);
            colony.AddPrivateDoubleBed(impressiveness: 100f);
            colony.AddPrivateDoubleBed(impressiveness: 50f);

            List<BedAssignment> plan = colony.Plan();

            Assert.Equal(2, plan.Count);
            Assert.Equal(2, plan.Select(x => x.BedId).Distinct().Count());
        }

        [Fact]
        public void TheEarlierCoupleGetsTheBetterBed()
        {
            Colony colony = new Colony();
            PawnView a1 = colony.AddPawn("a1");
            PawnView b1 = colony.AddPawn("b1");
            PawnView a2 = colony.AddPawn("a2");
            PawnView b2 = colony.AddPawn("b2");
            colony.Marry(a1, b1);
            colony.Marry(a2, b2);
            BedView grand = colony.AddPrivateDoubleBed(impressiveness: 100f);
            BedView plain = colony.AddPrivateDoubleBed(impressiveness: 50f);

            List<BedAssignment> plan = colony.Plan();

            Assert.Equal(grand.Id, plan[0].BedId);
            Assert.Equal(plain.Id, plan[1].BedId);
        }

        [Fact]
        public void OnlyOneBedForTwoCouples_TheFirstCoupleTakesIt()
        {
            Colony colony = new Colony();
            PawnView a1 = colony.AddPawn("a1");
            PawnView b1 = colony.AddPawn("b1");
            PawnView a2 = colony.AddPawn("a2");
            PawnView b2 = colony.AddPawn("b2");
            colony.Marry(a1, b1);
            colony.Marry(a2, b2);
            BedView only = colony.AddPrivateDoubleBed();

            List<BedAssignment> plan = colony.Plan();

            BedAssignment assignment = PlanAssert.Single(plan);
            Assert.Equal(only.Id, assignment.BedId);
            Assert.Equal(new HashSet<int> { a1.Id, b1.Id },
                         new HashSet<int> { assignment.PawnAId, assignment.PawnBId });
        }

        [Fact]
        public void ThreeCouples_ThreeBeds_AllAssigned()
        {
            Colony colony = new Colony();
            List<PawnView> pawns = new List<PawnView>();
            for (int i = 0; i < 6; i++) pawns.Add(colony.AddPawn("p" + i));
            colony.Marry(pawns[0], pawns[1]);
            colony.Marry(pawns[2], pawns[3]);
            colony.Marry(pawns[4], pawns[5]);
            for (int i = 0; i < 3; i++) colony.AddPrivateDoubleBed(impressiveness: 10f * i);

            List<BedAssignment> plan = colony.Plan();

            Assert.Equal(3, plan.Count);
            Assert.Equal(3, plan.Select(x => x.BedId).Distinct().Count());
            Assert.Equal(6, plan.SelectMany(x => new[] { x.PawnAId, x.PawnBId }).Distinct().Count());
        }

        [Fact]
        public void NoPawnAppearsInTwoAssignments()
        {
            Colony colony = new Colony();
            List<PawnView> pawns = new List<PawnView>();
            for (int i = 0; i < 8; i++) pawns.Add(colony.AddPawn("p" + i));
            for (int i = 0; i < 8; i += 2) colony.Marry(pawns[i], pawns[i + 1]);
            for (int i = 0; i < 4; i++) colony.AddPrivateDoubleBed();

            List<BedAssignment> plan = colony.Plan();

            List<int> assigned = plan.SelectMany(x => new[] { x.PawnAId, x.PawnBId }).ToList();
            Assert.Equal(assigned.Count, assigned.Distinct().Count());
        }

        [Fact]
        public void PlanningTwiceOverTheSameSnapshot_GivesTheSamePlan()
        {
            Colony colony = new Colony();
            List<PawnView> pawns = new List<PawnView>();
            for (int i = 0; i < 6; i++) pawns.Add(colony.AddPawn("p" + i));
            colony.Marry(pawns[0], pawns[1], opinion: 70);
            colony.Marry(pawns[2], pawns[3], opinion: 70);
            colony.Marry(pawns[4], pawns[5], opinion: 70);
            colony.AddPrivateDoubleBed(impressiveness: 30f);
            colony.AddBarracksDoubleBed(otherBeds: 1, impressiveness: 80f);
            colony.AddPrivateDoubleBed(comfort: 0.9f);

            string first = Describe(colony.Plan());
            string second = Describe(colony.Plan());

            Assert.Equal(first, second);
        }

        private static string Describe(List<BedAssignment> plan)
        {
            return string.Join("|", plan.Select(x => x.PawnAId + "+" + x.PawnBId + "->" + x.BedId));
        }

        // ------------------------------------------------------------- polygamy

        [Fact]
        public void PolygamousTriad_TheMutualPairIsBedded()
        {
            // A and B like each other most; C is married in but liked less.
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView b = colony.AddPawn("b");
            PawnView c = colony.AddPawn("c");
            colony.LovesOneWay(a, b, opinion: 90);
            colony.LovesOneWay(a, c, opinion: 30);
            colony.LovesOneWay(b, a, opinion: 90);
            colony.LovesOneWay(b, c, opinion: 20);
            colony.LovesOneWay(c, a, opinion: 60);
            colony.LovesOneWay(c, b, opinion: 50);
            BedView bed = colony.AddPrivateDoubleBed();

            PlanAssert.AssignedTo(colony.Plan(), a, b, bed);
        }

        [Fact]
        public void PolygamousTriad_WithNoMutualPair_NobodyIsBedded()
        {
            // A likes B best, B likes C best, C likes A best. Nobody is anybody's
            // mutual favourite, so the mod gives up entirely - even though the
            // game's own thought would be satisfied by *any* pairing.
            // This documents a real gap, not desired behaviour.
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView b = colony.AddPawn("b");
            PawnView c = colony.AddPawn("c");
            colony.LovesOneWay(a, b, opinion: 90);
            colony.LovesOneWay(a, c, opinion: 10);
            colony.LovesOneWay(b, c, opinion: 90);
            colony.LovesOneWay(b, a, opinion: 10);
            colony.LovesOneWay(c, a, opinion: 90);
            colony.LovesOneWay(c, b, opinion: 10);
            colony.AddPrivateDoubleBed();
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void PolygamousQuad_TwoMutualPairs_BothBedded()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView b = colony.AddPawn("b");
            PawnView c = colony.AddPawn("c");
            PawnView d = colony.AddPawn("d");
            colony.Marry(a, b, opinion: 90);
            colony.Marry(c, d, opinion: 90);
            // Cross relations, liked less.
            colony.LovesOneWay(a, c, opinion: 20);
            colony.LovesOneWay(c, a, opinion: 20);
            colony.AddPrivateDoubleBed();
            colony.AddPrivateDoubleBed();

            Assert.Equal(2, colony.Plan().Count);
        }

        [Fact]
        public void AlreadyHandledPartner_IsNotPairedAgain()
        {
            // B is A's favourite and gets bedded with A. C also names B as their
            // favourite, but B is spoken for, so C is left out rather than
            // producing a second assignment for B.
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView b = colony.AddPawn("b");
            PawnView c = colony.AddPawn("c");
            colony.Marry(a, b, opinion: 90);
            colony.LovesOneWay(c, b, opinion: 95);
            colony.AddPrivateDoubleBed();
            colony.AddPrivateDoubleBed();

            List<BedAssignment> plan = colony.Plan();

            BedAssignment assignment = PlanAssert.Single(plan);
            Assert.DoesNotContain(c.Id, new[] { assignment.PawnAId, assignment.PawnBId });
        }
    }
}
