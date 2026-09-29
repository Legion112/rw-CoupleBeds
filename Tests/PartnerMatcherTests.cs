using Xunit;
using CoupleBeds.Core;

namespace CoupleBeds.Tests
{
    /// PartnerMatcher must match LovePartnerRelationUtility.ExistingMostLikedLovePartner:
    /// highest opinion wins, first relation wins a tie, dead partners are skipped.
    public class PartnerMatcherTests
    {
        private static readonly PlannerSettings Lovers = new PlannerSettings { IncludeLovers = true };
        private static readonly PlannerSettings SpousesOnly = new PlannerSettings { IncludeLovers = false };

        [Fact]
        public void NoLovePartners_ReturnsNone()
        {
            Colony colony = new Colony();
            PawnView loner = colony.AddPawn("loner");

            Assert.Equal(PawnView.NoPawn, PartnerMatcher.PartnerOf(loner, Lovers));
        }

        [Fact]
        public void NullPawn_ReturnsNone()
        {
            Assert.Equal(PawnView.NoPawn, PartnerMatcher.PartnerOf(null, Lovers));
        }

        [Fact]
        public void SinglePartner_IsReturned()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView b = colony.AddPawn("b");
            colony.Marry(a, b);

            Assert.Equal(b.Id, PartnerMatcher.PartnerOf(a, Lovers));
        }

        [Fact]
        public void PicksHighestOpinion()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView meh = colony.AddPawn("meh");
            PawnView favourite = colony.AddPawn("favourite");
            colony.LovesOneWay(a, meh, opinion: 20);
            colony.LovesOneWay(a, favourite, opinion: 75);

            Assert.Equal(favourite.Id, PartnerMatcher.PartnerOf(a, Lovers));
        }

        [Fact]
        public void PicksHighestOpinion_RegardlessOfRelationOrder()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView favourite = colony.AddPawn("favourite");
            PawnView meh = colony.AddPawn("meh");
            colony.LovesOneWay(a, favourite, opinion: 75);
            colony.LovesOneWay(a, meh, opinion: 20);

            Assert.Equal(favourite.Id, PartnerMatcher.PartnerOf(a, Lovers));
        }

        [Fact]
        public void EqualOpinion_FirstRelationWins()
        {
            // The game uses a strict > comparison, so the earlier DirectRelation keeps it.
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView first = colony.AddPawn("first");
            PawnView second = colony.AddPawn("second");
            colony.LovesOneWay(a, first, opinion: 50);
            colony.LovesOneWay(a, second, opinion: 50);

            Assert.Equal(first.Id, PartnerMatcher.PartnerOf(a, Lovers));
        }

        [Fact]
        public void NegativeOpinionPartner_StillReturned()
        {
            // A hated spouse is still the most-liked love partner if they are the only one.
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView b = colony.AddPawn("b");
            colony.LovesOneWay(a, b, opinion: -60);

            Assert.Equal(b.Id, PartnerMatcher.PartnerOf(a, Lovers));
        }

        [Fact]
        public void DeadPartner_IsSkipped()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView living = colony.AddPawn("living");
            PawnView deceased = colony.AddPawn("deceased");
            a.LovePartners.Add(new LoveRelation(deceased.Id, true, 90, partnerDead: true));
            a.LovePartners.Add(new LoveRelation(living.Id, true, 30));

            Assert.Equal(living.Id, PartnerMatcher.PartnerOf(a, Lovers));
        }

        [Fact]
        public void AllPartnersDead_ReturnsNone()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView deceased = colony.AddPawn("deceased");
            a.LovePartners.Add(new LoveRelation(deceased.Id, true, 90, partnerDead: true));

            Assert.Equal(PawnView.NoPawn, PartnerMatcher.PartnerOf(a, Lovers));
        }

        [Fact]
        public void SpousesOnly_RejectsLover()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView lover = colony.AddPawn("lover");
            colony.LovesOneWay(a, lover, opinion: 60, spouse: false);

            Assert.Equal(PawnView.NoPawn, PartnerMatcher.PartnerOf(a, SpousesOnly));
        }

        [Fact]
        public void SpousesOnly_AcceptsSpouse()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView spouse = colony.AddPawn("spouse");
            colony.LovesOneWay(a, spouse, opinion: 60, spouse: true);

            Assert.Equal(spouse.Id, PartnerMatcher.PartnerOf(a, SpousesOnly));
        }

        [Fact]
        public void SpousesOnly_MostLikedIsLover_SpouseIsNeverConsidered()
        {
            // Characterises a real quirk inherited from the original GetPartner:
            // the most-liked partner is chosen first and the spouse filter is
            // applied afterwards, so an affair with a higher opinion than the
            // spouse makes the pawn unmanageable in "spouses only" mode.
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView spouse = colony.AddPawn("spouse");
            PawnView lover = colony.AddPawn("lover");
            colony.LovesOneWay(a, spouse, opinion: 40, spouse: true);
            colony.LovesOneWay(a, lover, opinion: 90, spouse: false);

            Assert.Equal(PawnView.NoPawn, PartnerMatcher.PartnerOf(a, SpousesOnly));
        }

        [Fact]
        public void AreMutualPartners_TrueWhenBothPointAtEachOther()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView b = colony.AddPawn("b");
            colony.Marry(a, b);

            Assert.True(PartnerMatcher.AreMutualPartners(a, b, Lovers));
        }

        [Fact]
        public void AreMutualPartners_FalseWhenOneLooksElsewhere()
        {
            Colony colony = new Colony();
            PawnView a = colony.AddPawn("a");
            PawnView b = colony.AddPawn("b");
            PawnView c = colony.AddPawn("c");
            colony.LovesOneWay(a, b, opinion: 50);
            colony.LovesOneWay(b, c, opinion: 90);

            Assert.False(PartnerMatcher.AreMutualPartners(a, b, Lovers));
        }
    }

    public class EligibilityTests
    {
        private static readonly PlannerSettings Default = new PlannerSettings();
        private static readonly PlannerSettings SkipSleepless = new PlannerSettings { ManageSleepless = false };

        private static PawnView Healthy()
        {
            return new Colony().AddPawn("healthy");
        }

        [Fact]
        public void HealthyColonist_IsEligible()
        {
            Assert.True(Eligibility.IsEligible(Healthy(), Default));
        }

        [Fact]
        public void Null_IsNotEligible()
        {
            Assert.False(Eligibility.IsEligible(null, Default));
        }

        [Fact]
        public void Dead_IsNotEligible()
        {
            PawnView pawn = Healthy();
            pawn.Dead = true;
            Assert.False(Eligibility.IsEligible(pawn, Default));
        }

        [Fact]
        public void Downed_IsNotEligible()
        {
            PawnView pawn = Healthy();
            pawn.Downed = true;
            Assert.False(Eligibility.IsEligible(pawn, Default));
        }

        [Fact]
        public void WithoutOwnership_IsNotEligible()
        {
            PawnView pawn = Healthy();
            pawn.HasOwnership = false;
            Assert.False(Eligibility.IsEligible(pawn, Default));
        }

        [Fact]
        public void NonHumanlike_IsNotEligible()
        {
            PawnView pawn = Healthy();
            pawn.Humanlike = false;
            Assert.False(Eligibility.IsEligible(pawn, Default));
        }

        [Fact]
        public void WithoutRestNeed_IsEligibleByDefault()
        {
            // They never sleep, but the game still hands them the
            // "sleeping alone" thought, so a bed is still worth having.
            PawnView pawn = Healthy();
            pawn.HasRestNeed = false;
            Assert.True(Eligibility.IsEligible(pawn, Default));
        }

        [Fact]
        public void WithoutRestNeed_IsNotEligibleWhenTheOptionIsOff()
        {
            PawnView pawn = Healthy();
            pawn.HasRestNeed = false;
            Assert.False(Eligibility.IsEligible(pawn, SkipSleepless));
        }

        [Fact]
        public void SleeplessOption_DoesNotChangeAnybodyElse()
        {
            Assert.True(Eligibility.IsEligible(Healthy(), SkipSleepless));
        }

        [Fact]
        public void NullSettings_IsNotEligible()
        {
            Assert.False(Eligibility.IsEligible(Healthy(), null));
        }

        [Fact]
        public void WithDeathrestGene_IsNotEligible()
        {
            PawnView pawn = Healthy();
            pawn.HasDeathrestGene = true;
            Assert.False(Eligibility.IsEligible(pawn, Default));
        }

        [Fact]
        public void SlaveIsEligible_SlaveryIsHandledByBedOwnerType()
        {
            PawnView pawn = Healthy();
            pawn.IsSlave = true;
            Assert.True(Eligibility.IsEligible(pawn, Default));
        }
    }
}
