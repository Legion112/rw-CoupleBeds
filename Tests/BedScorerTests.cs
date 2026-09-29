using Xunit;
using System.Collections.Generic;
using CoupleBeds.Core;

namespace CoupleBeds.Tests
{
    public class BedScorerTests
    {
        private const int PawnA = 1;
        private const int PawnB = 2;

        private static Dictionary<int, List<int>> NoOwners()
        {
            return new Dictionary<int, List<int>>();
        }

        private static Dictionary<int, List<int>> OwnedBy(BedView bed, params int[] pawnIds)
        {
            return new Dictionary<int, List<int>> { { bed.Id, new List<int>(pawnIds) } };
        }

        private static BedView Bed(int roomId = 1)
        {
            return new BedView { Id = 900, RoomId = roomId };
        }

        [Fact]
        public void PrivateRoom_GetsTheBedroomBonus()
        {
            BedView bed = Bed();
            bed.OtherNonMedicalBedsInRoom = 0;

            Assert.Equal(BedScorer.PrivateRoomBonus, BedScorer.Score(bed, PawnA, PawnB, NoOwners()));
        }

        [Theory]
        [InlineData(1, -10f)]
        [InlineData(2, -20f)]
        [InlineData(5, -50f)]
        public void Barracks_IsPenalisedPerOtherBed(int otherBeds, float expected)
        {
            BedView bed = Bed();
            bed.OtherNonMedicalBedsInRoom = otherBeds;

            Assert.Equal(expected, BedScorer.Score(bed, PawnA, PawnB, NoOwners()));
        }

        [Fact]
        public void NoRoom_IsHeavilyPenalised()
        {
            BedView bed = new BedView { Id = 900, RoomId = BedView.NoRoom };

            Assert.Equal(BedScorer.OutdoorsOrNoRoomPenalty, BedScorer.Score(bed, PawnA, PawnB, NoOwners()));
        }

        [Fact]
        public void OutdoorRoom_IsHeavilyPenalised()
        {
            BedView bed = Bed();
            bed.RoomOutdoors = true;

            Assert.Equal(BedScorer.OutdoorsOrNoRoomPenalty, BedScorer.Score(bed, PawnA, PawnB, NoOwners()));
        }

        [Fact]
        public void OutdoorRoom_IgnoresImpressivenessAndBedCount()
        {
            // A magnificent open-air room must not beat any indoor bed on those terms.
            BedView bed = Bed();
            bed.RoomOutdoors = true;
            bed.RoomImpressiveness = 500f;
            bed.OtherNonMedicalBedsInRoom = 0;

            Assert.Equal(BedScorer.OutdoorsOrNoRoomPenalty, BedScorer.Score(bed, PawnA, PawnB, NoOwners()));
        }

        [Fact]
        public void Impressiveness_IsAddedDirectly()
        {
            BedView bed = Bed();
            bed.RoomImpressiveness = 62.5f;

            Assert.Equal(62.5f + BedScorer.PrivateRoomBonus, BedScorer.Score(bed, PawnA, PawnB, NoOwners()));
        }

        [Fact]
        public void Comfort_IsWeighted()
        {
            BedView bed = Bed();
            bed.Comfort = 0.8f;

            Assert.Equal(BedScorer.PrivateRoomBonus + 0.8f * BedScorer.ComfortWeight,
                         BedScorer.Score(bed, PawnA, PawnB, NoOwners()), precision: 4);
        }

        [Fact]
        public void BedOwnedByFirstPartner_GetsTheStayPutBonus()
        {
            BedView bed = Bed();

            Assert.Equal(BedScorer.PrivateRoomBonus + BedScorer.AlreadyOwnedBonus,
                         BedScorer.Score(bed, PawnA, PawnB, OwnedBy(bed, PawnA)));
        }

        [Fact]
        public void BedOwnedBySecondPartner_GetsTheStayPutBonus()
        {
            BedView bed = Bed();

            Assert.Equal(BedScorer.PrivateRoomBonus + BedScorer.AlreadyOwnedBonus,
                         BedScorer.Score(bed, PawnA, PawnB, OwnedBy(bed, PawnB)));
        }

        [Fact]
        public void BedOwnedByBothPartners_CountsTheBonusOnlyOnce()
        {
            BedView bed = Bed();

            Assert.Equal(BedScorer.PrivateRoomBonus + BedScorer.AlreadyOwnedBonus,
                         BedScorer.Score(bed, PawnA, PawnB, OwnedBy(bed, PawnA, PawnB)));
        }

        [Fact]
        public void BedOwnedByStranger_GetsNoBonus()
        {
            BedView bed = Bed();

            Assert.Equal(BedScorer.PrivateRoomBonus, BedScorer.Score(bed, PawnA, PawnB, OwnedBy(bed, 999)));
        }

        [Fact]
        public void FullFormula()
        {
            BedView bed = new BedView
            {
                Id = 900,
                RoomId = 1,
                RoomImpressiveness = 90f,
                OtherNonMedicalBedsInRoom = 3,
                Comfort = 0.75f,
            };

            // 90 impressiveness - 3*10 barracks + 0.75*20 comfort + 5 already owned
            Assert.Equal(90f - 30f + 15f + 5f, BedScorer.Score(bed, PawnA, PawnB, OwnedBy(bed, PawnB)), precision: 4);
        }

        [Fact]
        public void PrivateRoomBeatsMoreImpressiveBarracks_UpToTheBonus()
        {
            BedView privateBed = new BedView { Id = 1, RoomId = 1, RoomImpressiveness = 0f, OtherNonMedicalBedsInRoom = 0 };
            BedView barracks = new BedView { Id = 2, RoomId = 2, RoomImpressiveness = 45f, OtherNonMedicalBedsInRoom = 1 };

            // 40 vs 45-10=35
            Assert.True(BedScorer.Score(privateBed, PawnA, PawnB, NoOwners())
                      > BedScorer.Score(barracks, PawnA, PawnB, NoOwners()));
        }

        [Fact]
        public void IsOwner_ToleratesMissingAndNullEntries()
        {
            Dictionary<int, List<int>> owners = new Dictionary<int, List<int>> { { 900, null } };

            Assert.False(BedScorer.IsOwner(owners, 900, PawnA));
            Assert.False(BedScorer.IsOwner(owners, 901, PawnA));
            Assert.False(BedScorer.IsOwner(null, 900, PawnA));
        }
    }
}
