// Couple Beds - automatically puts partners into a shared double bed.
//
// The decision logic lives in CoupleBeds.Core, which has no RimWorld
// dependencies and is unit tested. This file is the adapter: it turns a Map into
// a ColonySnapshot, asks the planner what to do, and carries it out.

using System;
using System.Collections.Generic;
using CoupleBeds.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CoupleBeds
{
    // ------------------------------------------------------------------ settings
    public class CoupleBedsSettings : ModSettings
    {
        public bool enabled = true;
        public bool includeLovers = true;   // false = spouses only
        public bool allowUpgrade = true;    // move couples that already share to a clearly better bed
        public bool notify = true;
        public int intervalHours = 1;
        public float upgradeMargin = 15f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref includeLovers, "includeLovers", true);
            Scribe_Values.Look(ref allowUpgrade, "allowUpgrade", true);
            Scribe_Values.Look(ref notify, "notify", true);
            Scribe_Values.Look(ref intervalHours, "intervalHours", 1);
            Scribe_Values.Look(ref upgradeMargin, "upgradeMargin", 15f);
        }

        public PlannerSettings ToPlannerSettings()
        {
            return new PlannerSettings
            {
                IncludeLovers = includeLovers,
                AllowUpgrade = allowUpgrade,
                UpgradeMargin = upgradeMargin,
            };
        }
    }

    public class CoupleBedsMod : Mod
    {
        public static CoupleBedsSettings Settings;

        private static readonly int[] IntervalOptions = { 1, 2, 4, 6, 12, 24 };
        private static readonly float[] MarginOptions = { 5f, 15f, 30f, 60f };

        public CoupleBedsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CoupleBedsSettings>();
        }

        public override string SettingsCategory()
        {
            return "Couple Beds";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            CoupleBedsSettings s = Settings;
            Listing_Standard l = new Listing_Standard();
            l.Begin(inRect);

            l.CheckboxLabeled("Enabled", ref s.enabled,
                "Automatically move partners into a shared double bed.");
            l.CheckboxLabeled("Include lovers and fiancés (not only spouses)", ref s.includeLovers,
                "Lovers and fiancés get the same 'Want to sleep with partner' penalty as spouses.");
            l.CheckboxLabeled("Upgrade couples to better free double beds", ref s.allowUpgrade,
                "If a couple already shares a bed but a clearly better free double bed exists (private room, more impressive), move them.");
            l.CheckboxLabeled("Show a message when a couple is moved", ref s.notify);
            l.Gap();

            if (l.ButtonText("Check every " + s.intervalHours + " in-game hour(s)  (click to change)"))
                s.intervalHours = NextInt(IntervalOptions, s.intervalHours);
            if (l.ButtonText("Upgrade only if new bed scores +" + s.upgradeMargin.ToString("0") + " better  (click to change)"))
                s.upgradeMargin = NextFloat(MarginOptions, s.upgradeMargin);
            l.Gap();

            if (Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null)
            {
                if (l.ButtonText("Assign couples on current map now"))
                {
                    int moved = CoupleBedAssigner.Run(Find.CurrentMap, s);
                    Messages.Message("Couple Beds: " + moved + " couple(s) reassigned.",
                        MessageTypeDefOf.NeutralEvent, false);
                }
            }

            l.Gap();
            l.Label("Bed choice: private bedroom > impressive room > comfortable bed. " +
                    "Beds owned by anyone else are never touched.");
            l.End();
        }

        private static int NextInt(int[] opts, int cur)
        {
            for (int i = 0; i < opts.Length; i++)
                if (opts[i] > cur) return opts[i];
            return opts[0];
        }

        private static float NextFloat(float[] opts, float cur)
        {
            for (int i = 0; i < opts.Length; i++)
                if (opts[i] > cur + 0.01f) return opts[i];
            return opts[0];
        }
    }

    // ------------------------------------------------------------------ ticker
    // RimWorld creates one of these per map automatically.
    public class MapComponent_CoupleBeds : MapComponent
    {
        public MapComponent_CoupleBeds(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            CoupleBedsSettings s = CoupleBedsMod.Settings;
            if (s == null || !s.enabled) return;

            int interval = Math.Max(1, s.intervalHours) * GenDate.TicksPerHour;
            // Offset per map so several maps don't all run on the same tick.
            if ((Find.TickManager.TicksGame + map.uniqueID * 97) % interval != 0) return;

            try
            {
                CoupleBedAssigner.Run(map, s);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[CoupleBeds] " + e, 0x5C0B1ED);
            }
        }
    }

    // ------------------------------------------------- game <-> core adapter
    public static class CoupleBedAssigner
    {
        /// Returns the number of couples moved.
        public static int Run(Map map, CoupleBedsSettings s)
        {
            Dictionary<int, Pawn> pawnsById;
            Dictionary<int, Building_Bed> bedsById;
            ColonySnapshot snapshot = BuildSnapshot(map, out pawnsById, out bedsById);

            List<BedAssignment> plan = CoupleBedPlanner.Plan(
                snapshot, s.ToPlannerSettings(), new GameBedAccess(pawnsById, bedsById));

            return Apply(plan, pawnsById, bedsById, s);
        }

        // ---------------------------------------------------------- snapshot
        public static ColonySnapshot BuildSnapshot(Map map,
            out Dictionary<int, Pawn> pawnsById, out Dictionary<int, Building_Bed> bedsById)
        {
            ColonySnapshot snapshot = new ColonySnapshot();
            pawnsById = new Dictionary<int, Pawn>();
            bedsById = new Dictionary<int, Building_Bed>();

            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn == null) continue;
                pawnsById[pawn.thingIDNumber] = pawn;
                snapshot.Pawns.Add(ViewOf(pawn));
            }

            foreach (Building_Bed bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
            {
                if (bed == null || bed.Destroyed || !bed.Spawned) continue;
                bedsById[bed.thingIDNumber] = bed;
                snapshot.Beds.Add(ViewOf(bed));
            }

            return snapshot;
        }

        private static PawnView ViewOf(Pawn pawn)
        {
            PawnView view = new PawnView
            {
                Id = pawn.thingIDNumber,
                Label = pawn.LabelShort,
                Dead = pawn.Dead,
                Downed = pawn.Downed,
                Humanlike = pawn.RaceProps != null && pawn.RaceProps.Humanlike,
                HasOwnership = pawn.ownership != null,
                HasRestNeed = pawn.needs != null && pawn.needs.rest != null,
                HasDeathrestGene = pawn.genes != null && pawn.genes.GetFirstGeneOfType<Gene_Deathrest>() != null,
                IsSlave = pawn.IsSlave,
            };

            if (pawn.ownership != null && pawn.ownership.OwnedBed != null)
                view.OwnedBedId = pawn.ownership.OwnedBed.thingIDNumber;

            if (pawn.relations != null)
            {
                List<DirectPawnRelation> relations = pawn.relations.DirectRelations;
                for (int i = 0; i < relations.Count; i++)
                {
                    DirectPawnRelation rel = relations[i];
                    if (rel.otherPawn == null) continue;
                    if (!LovePartnerRelationUtility.IsLovePartnerRelation(rel.def)) continue;
                    view.LovePartners.Add(new LoveRelation(
                        rel.otherPawn.thingIDNumber,
                        rel.def == PawnRelationDefOf.Spouse,
                        pawn.relations.OpinionOf(rel.otherPawn),
                        rel.otherPawn.Dead));
                }
            }

            return view;
        }

        private static BedView ViewOf(Building_Bed bed)
        {
            BedView view = new BedView
            {
                Id = bed.thingIDNumber,
                Medical = bed.Medical,
                ForPrisoners = bed.ForPrisoners,
                Humanlike = bed.def.building != null && bed.def.building.bed_humanlike,
                SleepingSlots = bed.SleepingSlotsCount,
                ForOwnerType = ToCore(bed.ForOwnerType),
                Comfort = bed.GetStatValue(StatDefOf.Comfort),
            };

            Room room = bed.GetRoom();
            if (room != null)
            {
                view.RoomId = room.ID;
                view.RoomOutdoors = room.PsychologicallyOutdoors;
                view.RoomImpressiveness = room.GetStat(RoomStatDefOf.Impressiveness);

                int others = 0;
                foreach (Building_Bed other in room.ContainedBeds)
                    if (other != bed && !other.Medical) others++;
                view.OtherNonMedicalBedsInRoom = others;
            }

            foreach (Pawn owner in bed.OwnersForReading)
                if (owner != null) view.OwnerIds.Add(owner.thingIDNumber);

            return view;
        }

        private static BedOwner ToCore(BedOwnerType type)
        {
            switch (type)
            {
                case BedOwnerType.Slave: return BedOwner.Slave;
                case BedOwnerType.Prisoner: return BedOwner.Prisoner;
                default: return BedOwner.Colonist;
            }
        }

        // ------------------------------------------------------------- apply
        private static int Apply(List<BedAssignment> plan,
            Dictionary<int, Pawn> pawnsById, Dictionary<int, Building_Bed> bedsById,
            CoupleBedsSettings s)
        {
            int moved = 0;
            for (int i = 0; i < plan.Count; i++)
            {
                BedAssignment assignment = plan[i];

                Pawn a, b;
                Building_Bed bed;
                if (!pawnsById.TryGetValue(assignment.PawnAId, out a)) continue;
                if (!pawnsById.TryGetValue(assignment.PawnBId, out b)) continue;
                if (!bedsById.TryGetValue(assignment.BedId, out bed)) continue;
                if (bed.Destroyed || !bed.Spawned) continue;

                if (!IsOwner(bed, a)) a.ownership.ClaimBedIfNonMedical(bed);
                if (!IsOwner(bed, b)) b.ownership.ClaimBedIfNonMedical(bed);

                // The game can refuse; only count and announce real moves.
                if (!IsOwner(bed, a) || !IsOwner(bed, b)) continue;
                moved++;

                if (s.notify)
                {
                    string text = assignment.IsUpgrade
                        ? a.LabelShort + " and " + b.LabelShort + " moved to a better shared bed."
                        : a.LabelShort + " and " + b.LabelShort + " now share a bed.";
                    Messages.Message(text, bed, MessageTypeDefOf.NeutralEvent, false);
                }
            }
            return moved;
        }

        private static bool IsOwner(Building_Bed bed, Pawn pawn)
        {
            List<Pawn> owners = bed.OwnersForReading;
            for (int i = 0; i < owners.Count; i++)
                if (owners[i] == pawn) return true;
            return false;
        }

        // ------------------------------------------------- IBedAccess over the game
        private sealed class GameBedAccess : IBedAccess
        {
            private readonly Dictionary<int, Pawn> pawns;
            private readonly Dictionary<int, Building_Bed> beds;

            public GameBedAccess(Dictionary<int, Pawn> pawns, Dictionary<int, Building_Bed> beds)
            {
                this.pawns = pawns;
                this.beds = beds;
            }

            private bool Resolve(int pawnId, int bedId, out Pawn pawn, out Building_Bed bed)
            {
                return pawns.TryGetValue(pawnId, out pawn) & beds.TryGetValue(bedId, out bed);
            }

            public bool CanUseBedEver(int pawnId, int bedId)
            {
                Pawn pawn; Building_Bed bed;
                if (!Resolve(pawnId, bedId, out pawn, out bed)) return false;
                return RestUtility.CanUseBedEver(pawn, bed.def);
            }

            public bool IsForbidden(int pawnId, int bedId)
            {
                Pawn pawn; Building_Bed bed;
                if (!Resolve(pawnId, bedId, out pawn, out bed)) return true;
                return bed.IsForbidden(pawn);
            }

            public bool CanReach(int pawnId, int bedId)
            {
                Pawn pawn; Building_Bed bed;
                if (!Resolve(pawnId, bedId, out pawn, out bed)) return false;
                return pawn.CanReach(bed, PathEndMode.Touch, Danger.Some);
            }
        }
    }
}
