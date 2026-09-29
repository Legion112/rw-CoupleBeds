using Xunit;
using System.Collections.Generic;
using CoupleBeds.Core;

namespace CoupleBeds.Tests
{
    /// CollectDoubleBeds: which beds could ever host a couple, before any
    /// couple-specific check.
    public class BedCandidateTests
    {
        private static List<BedView> Candidates(Colony colony)
        {
            return CoupleBedPlanner.CollectDoubleBeds(colony.Snapshot);
        }

        [Fact]
        public void DoubleBed_IsACandidate()
        {
            Colony colony = new Colony();
            BedView bed = colony.AddPrivateDoubleBed();

            Assert.Equal(new[] { bed.Id }, Candidates(colony).ConvertAll(b => b.Id));
        }

        [Fact]
        public void SingleBed_IsNot()
        {
            Colony colony = new Colony();
            colony.AddBed(b => b.SleepingSlots = 1);

            Assert.Empty(Candidates(colony));
        }

        [Fact]
        public void ZeroSlotBed_IsNot()
        {
            Colony colony = new Colony();
            colony.AddBed(b => b.SleepingSlots = 0);

            Assert.Empty(Candidates(colony));
        }

        [Fact]
        public void MedicalBed_IsNot()
        {
            Colony colony = new Colony();
            colony.AddBed(b => { b.SleepingSlots = 2; b.Medical = true; });

            Assert.Empty(Candidates(colony));
        }

        [Fact]
        public void PrisonerBed_IsNot()
        {
            Colony colony = new Colony();
            colony.AddBed(b => { b.SleepingSlots = 2; b.ForPrisoners = true; });

            Assert.Empty(Candidates(colony));
        }

        [Fact]
        public void AnimalBed_IsNot()
        {
            Colony colony = new Colony();
            colony.AddBed(b => { b.SleepingSlots = 2; b.Humanlike = false; });

            Assert.Empty(Candidates(colony));
        }

        [Fact]
        public void BedWithMoreThanTwoSlots_IsACandidate()
        {
            Colony colony = new Colony();
            BedView wide = colony.AddBed(b => { b.SleepingSlots = 3; b.RoomId = 1; });

            Assert.Equal(new[] { wide.Id }, Candidates(colony).ConvertAll(b => b.Id));
        }

        [Fact]
        public void OrderIsPreserved_SoTiesResolveDeterministically()
        {
            Colony colony = new Colony();
            BedView first = colony.AddPrivateDoubleBed();
            BedView second = colony.AddPrivateDoubleBed();

            Assert.Equal(new[] { first.Id, second.Id }, Candidates(colony).ConvertAll(b => b.Id));
        }

        [Fact]
        public void MixedList_KeepsOnlyUsableDoubles()
        {
            Colony colony = new Colony();
            colony.AddBed(b => b.SleepingSlots = 1);
            BedView good = colony.AddPrivateDoubleBed();
            colony.AddBed(b => { b.SleepingSlots = 2; b.Medical = true; });
            BedView alsoGood = colony.AddBarracksDoubleBed();
            colony.AddBed(b => { b.SleepingSlots = 2; b.Humanlike = false; });

            Assert.Equal(new[] { good.Id, alsoGood.Id }, Candidates(colony).ConvertAll(b => b.Id));
        }
    }
}
