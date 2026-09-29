using Xunit;
using System.Collections.Generic;
using CoupleBeds.Core;

namespace CoupleBeds.Tests
{
    public class PlannerTests
    {
        [Fact]
        public void MarriedCouple_GetsTheDoubleBed()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView bed = colony.AddPrivateDoubleBed();

            PlanAssert.AssignedTo(colony.Plan(), a, b, bed);
        }

        [Fact]
        public void NewAssignment_IsNotMarkedAsAnUpgrade()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            colony.AddPrivateDoubleBed();

            BedAssignment assignment = PlanAssert.Single(colony.Plan());
            Assert.False(assignment.IsUpgrade);
            Assert.Equal(PawnView.NoBed, assignment.PreviousSharedBedId);
        }

        [Fact]
        public void NoBedsAtAll_NothingHappens()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void OnlySingleBeds_NothingHappens()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            colony.AddBed(bed => { bed.SleepingSlots = 1; bed.RoomId = 1; });
            colony.AddBed(bed => { bed.SleepingSlots = 1; bed.RoomId = 2; });

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void NobodyIsPartnered_NothingHappens()
        {
            Colony colony = new Colony();
            colony.AddPawn("ada");
            colony.AddPawn("bo");
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void EmptyColony_NothingHappens()
        {
            Assert.Empty(new Colony().Plan());
        }

        [Fact]
        public void NullArguments_AreTolerated()
        {
            Assert.Empty(CoupleBedPlanner.Plan(null, new PlannerSettings(), new FakeBedAccess()));
            Assert.Empty(CoupleBedPlanner.Plan(new ColonySnapshot(), null, new FakeBedAccess()));
            Assert.Empty(CoupleBedPlanner.Plan(new ColonySnapshot(), new PlannerSettings(), null));
        }

        // ------------------------------------------------------------ partners

        [Fact]
        public void PartnerOnAnotherMap_NothingHappens()
        {
            Colony colony = new Colony();
            PawnView here = colony.AddPawn("here");
            PawnView away = colony.OffMapPawn("away");   // never added to the snapshot
            colony.Marry(here, away);
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void MostLikedPartnerIsOffMap_TheOnMapPartnerIsAlsoSkipped()
        {
            // Characterises current behaviour: the most-liked partner is chosen
            // before anyone checks whether they are even on this map, so a pawn
            // whose favourite is away in a caravan gets no bed with the partner
            // who is standing right there.
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView here = colony.AddPawn("here");
            PawnView away = colony.OffMapPawn("away");
            colony.LovesOneWay(a, away, opinion: 95);
            colony.LovesOneWay(a, here, opinion: 40);
            colony.LovesOneWay(here, a, opinion: 80);
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void UnrequitedLove_NothingHappens()
        {
            Colony colony = new Colony();
            PawnView admirer = colony.AddPawn("admirer");
            PawnView other = colony.AddPawn("other");
            colony.LovesOneWay(admirer, other, opinion: 70);
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void SpousesOnly_LoversAreLeftAlone()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b, spouse: false);
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan(new PlannerSettings { IncludeLovers = false }));
        }

        [Fact]
        public void SpousesOnly_SpousesAreStillAssigned()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b, spouse: true);
            BedView bed = colony.AddPrivateDoubleBed();

            PlanAssert.AssignedTo(colony.Plan(new PlannerSettings { IncludeLovers = false }), a, b, bed);
        }

        [Fact]
        public void IncludeLovers_LoversAreAssigned()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b, spouse: false);
            BedView bed = colony.AddPrivateDoubleBed();

            PlanAssert.AssignedTo(colony.Plan(new PlannerSettings { IncludeLovers = true }), a, b, bed);
        }

        // ---------------------------------------------------------- eligibility

        [Fact]
        public void DownedPartner_NothingHappens()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo", p => p.Downed = true);
            colony.Marry(a, b);
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void DeathrestingPartner_NothingHappens()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo", p => p.HasDeathrestGene = true);
            colony.Marry(a, b);
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void PartnerWithoutRestNeed_NothingHappens()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo", p => p.HasRestNeed = false);
            colony.Marry(a, b);
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void IneligibleCouple_StillBlocksBothFromLaterPasses()
        {
            // Both partners are marked handled before eligibility is checked, so a
            // downed pawn's partner is not paired with anybody else this run.
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo", p => p.Downed = true);
            colony.Marry(a, b);
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan());
        }

        // --------------------------------------------------------- slaves

        [Fact]
        public void SlaveAndColonist_AreNotBedded()
        {
            Colony colony = new Colony();
            PawnView colonist = colony.AddPawn("colonist");
            PawnView slave = colony.AddPawn("slave", p => p.IsSlave = true);
            colony.Marry(colonist, slave);
            colony.AddPrivateDoubleBed();

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void SlaveCouple_TakesASlaveBed()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a", p => p.IsSlave = true);
            PawnView b = colony.AddPawn("b", p => p.IsSlave = true);
            colony.Marry(a, b);
            BedView slaveBed = colony.AddBed(bed =>
            {
                bed.SleepingSlots = 2;
                bed.RoomId = 1;
                bed.ForOwnerType = BedOwner.Slave;
            });

            PlanAssert.AssignedTo(colony.Plan(), a, b, slaveBed);
        }

        [Fact]
        public void SlaveCouple_WillNotTakeAColonistBed()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a", p => p.IsSlave = true);
            PawnView b = colony.AddPawn("b", p => p.IsSlave = true);
            colony.Marry(a, b);
            colony.AddPrivateDoubleBed();   // BedOwner.Colonist by default

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void ColonistCouple_WillNotTakeASlaveBed()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView b = colony.AddPawn("b");
            colony.Marry(a, b);
            colony.AddBed(bed => { bed.SleepingSlots = 2; bed.RoomId = 1; bed.ForOwnerType = BedOwner.Slave; });

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void PrisonerOwnerTypeBed_IsNeverUsed()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView b = colony.AddPawn("b");
            colony.Marry(a, b);
            colony.AddBed(bed => { bed.SleepingSlots = 2; bed.RoomId = 1; bed.ForOwnerType = BedOwner.Prisoner; });

            Assert.Empty(colony.Plan());
        }

        // ------------------------------------------------------ existing owners

        [Fact]
        public void BedOwnedByAThirdPawn_IsNeverStolen()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView taken = colony.AddPrivateDoubleBed(impressiveness: 200f);
            colony.OwnedByStranger(taken);

            Assert.Empty(colony.Plan());
        }

        [Fact]
        public void BedAlreadyHeldByOnePartner_IsReused()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView hers = colony.AddPrivateDoubleBed();
            colony.Owns(a, hers);

            PlanAssert.AssignedTo(colony.Plan(), a, b, hers);
        }

        [Fact]
        public void PartnerSharingWithSomebodyElse_TheCoupleMovesElsewhere()
        {
            // The third pawn keeps their bed; the couple takes a different one.
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView shared = colony.AddPrivateDoubleBed(impressiveness: 300f);
            colony.Owns(a, shared);
            colony.OwnedByStranger(shared, strangerId: 777);
            BedView free = colony.AddPrivateDoubleBed(impressiveness: 10f);

            PlanAssert.AssignedTo(colony.Plan(), a, b, free);
        }

        [Fact]
        public void EachPartnerOwnsTheirOwnDoubleBed_TheyEndUpTogether()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView hers = colony.AddPrivateDoubleBed(impressiveness: 50f);
            BedView his = colony.AddPrivateDoubleBed(impressiveness: 10f);
            colony.Owns(a, hers);
            colony.Owns(b, his);

            PlanAssert.AssignedTo(colony.Plan(), a, b, hers);
        }

        // ------------------------------------------------------------ access

        [Fact]
        public void ForbiddenBed_IsSkipped()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView forbidden = colony.AddPrivateDoubleBed(impressiveness: 500f);
            BedView allowed = colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess().Forbidden(a, forbidden);

            PlanAssert.AssignedTo(colony.Plan(access: access), a, b, allowed);
        }

        [Fact]
        public void BedUnreachableForOnePartner_IsSkipped()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView unreachable = colony.AddPrivateDoubleBed(impressiveness: 500f);
            BedView reachable = colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess().Unreachable(b, unreachable);

            PlanAssert.AssignedTo(colony.Plan(access: access), a, b, reachable);
        }

        [Fact]
        public void BedTheyCanNeverUse_IsSkipped()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView tooSmall = colony.AddPrivateDoubleBed(impressiveness: 500f);
            BedView fine = colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess().CannotUseEver(a, tooSmall);

            PlanAssert.AssignedTo(colony.Plan(access: access), a, b, fine);
        }

        [Fact]
        public void EveryBedUnreachable_NothingHappens()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView bed = colony.AddPrivateDoubleBed();

            FakeBedAccess access = new FakeBedAccess().Unreachable(a, bed);

            Assert.Empty(colony.Plan(access: access));
        }

        // ------------------------------------------------------------ choice

        [Fact]
        public void PrefersPrivateRoomOverMoreImpressiveBarracks()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView barracks = colony.AddBarracksDoubleBed(otherBeds: 1, impressiveness: 45f);
            BedView bedroom = colony.AddPrivateDoubleBed(impressiveness: 0f);

            PlanAssert.AssignedTo(colony.Plan(), a, b, bedroom);
        }

        [Fact]
        public void PrefersTheMoreImpressiveRoomWhenBothArePrivate()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            colony.AddPrivateDoubleBed(impressiveness: 20f);
            BedView grand = colony.AddPrivateDoubleBed(impressiveness: 120f);

            PlanAssert.AssignedTo(colony.Plan(), a, b, grand);
        }

        [Fact]
        public void PrefersTheMoreComfortableBedWhenRoomsAreEqual()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            colony.AddPrivateDoubleBed(comfort: 0.6f);
            BedView plush = colony.AddPrivateDoubleBed(comfort: 0.9f);

            PlanAssert.AssignedTo(colony.Plan(), a, b, plush);
        }

        [Fact]
        public void IndoorBarracksBeatsAnOutdoorPrivateBed()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView outdoors = colony.AddBed(bed =>
            {
                bed.SleepingSlots = 2;
                bed.RoomId = 1;
                bed.RoomOutdoors = true;
                bed.RoomImpressiveness = 400f;
            });
            BedView indoors = colony.AddBarracksDoubleBed(otherBeds: 4);

            PlanAssert.AssignedTo(colony.Plan(), a, b, indoors);
        }

        [Fact]
        public void EqualScores_TheFirstBedInIterationOrderWins()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView first = colony.AddPrivateDoubleBed(impressiveness: 50f);
            colony.AddPrivateDoubleBed(impressiveness: 50f);

            PlanAssert.AssignedTo(colony.Plan(), a, b, first);
        }

        [Fact]
        public void OutdoorBedIsStillBetterThanNoBed()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("ada");
            PawnView b = colony.AddPawn("bo");
            colony.Marry(a, b);
            BedView outdoors = colony.AddBed(bed => { bed.SleepingSlots = 2; bed.RoomId = BedView.NoRoom; });

            PlanAssert.AssignedTo(colony.Plan(), a, b, outdoors);
        }
    }
}
