// Couple Beds - automatically puts partners into a shared double bed.
// Written in plain C# 5 so it compiles with the csc.exe that ships with Windows.

using System;
using System.Collections.Generic;
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

    // ------------------------------------------------------------------ logic
    public static class CoupleBedAssigner
    {
        // Returns number of couples moved.
        public static int Run(Map map, CoupleBedsSettings s)
        {
            List<Pawn> pawns = new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            HashSet<Pawn> handled = new HashSet<Pawn>();
            List<Building_Bed> beds = null;
            int moved = 0;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn a = pawns[i];
                if (handled.Contains(a)) continue;

                Pawn b = GetPartner(a, s);
                if (b == null || handled.Contains(b)) continue;
                if (GetPartner(b, s) != a) continue;          // must be mutual (matters with polygamy)
                if (!b.Spawned || b.Map != map) continue;     // partner away (caravan etc.)

                handled.Add(a);
                handled.Add(b);

                if (!Eligible(a) || !Eligible(b)) continue;
                if (a.IsSlave != b.IsSlave) continue;         // slave and colonist beds are different

                if (beds == null) beds = CollectDoubleBeds(map);
                if (HandleCouple(a, b, beds, s)) moved++;
            }
            return moved;
        }

        private static Pawn GetPartner(Pawn p, CoupleBedsSettings s)
        {
            if (p.relations == null) return null;
            Pawn partner = LovePartnerRelationUtility.ExistingMostLikedLovePartner(p, false);
            if (partner == null) return null;
            if (!s.includeLovers && !p.relations.DirectRelationExists(PawnRelationDefOf.Spouse, partner))
                return null;
            return partner;
        }

        private static bool Eligible(Pawn p)
        {
            if (p.Dead || !p.Spawned || p.Downed) return false;
            if (p.ownership == null || p.RaceProps == null || !p.RaceProps.Humanlike) return false;
            if (p.needs == null || p.needs.rest == null) return false;   // doesn't sleep
            if (p.genes != null && p.genes.GetFirstGeneOfType<Gene_Deathrest>() != null)
                return false;                                           // don't break deathrest caskets
            return true;
        }

        private static List<Building_Bed> CollectDoubleBeds(Map map)
        {
            List<Building_Bed> result = new List<Building_Bed>();
            foreach (Building_Bed bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
            {
                if (bed == null || bed.Destroyed || !bed.Spawned) continue;
                if (bed.Medical || bed.ForPrisoners) continue;
                if (bed.SleepingSlotsCount < 2) continue;
                if (bed.def.building == null || !bed.def.building.bed_humanlike) continue;
                result.Add(bed);
            }
            return result;
        }

        private static bool HandleCouple(Pawn a, Pawn b, List<Building_Bed> beds, CoupleBedsSettings s)
        {
            Building_Bed bedA = a.ownership.OwnedBed;
            Building_Bed bedB = b.ownership.OwnedBed;
            bool alreadySharing = bedA != null && bedA == bedB;
            if (alreadySharing && !s.allowUpgrade) return false;

            Building_Bed best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < beds.Count; i++)
            {
                Building_Bed bed = beds[i];
                if (!Usable(bed, a, b)) continue;
                float score = Score(bed, a, b);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = bed;
                }
            }
            if (best == null) return false;

            if (alreadySharing)
            {
                if (best == bedA) return false;
                float current = Score(bedA, a, b);
                if (bestScore < current + s.upgradeMargin) return false;
            }

            if (!IsOwner(best, a)) a.ownership.ClaimBedIfNonMedical(best);
            if (!IsOwner(best, b)) b.ownership.ClaimBedIfNonMedical(best);

            if (!IsOwner(best, a) || !IsOwner(best, b)) return false;

            if (s.notify)
            {
                string text = alreadySharing
                    ? a.LabelShort + " and " + b.LabelShort + " moved to a better shared bed."
                    : a.LabelShort + " and " + b.LabelShort + " now share a bed.";
                Messages.Message(text, best, MessageTypeDefOf.NeutralEvent, false);
            }
            return true;
        }

        private static bool Usable(Building_Bed bed, Pawn a, Pawn b)
        {
            if (bed.Destroyed || !bed.Spawned || bed.Medical || bed.ForPrisoners) return false;
            if (bed.SleepingSlotsCount < 2) return false;

            BedOwnerType wanted = a.IsSlave ? BedOwnerType.Slave : BedOwnerType.Colonist;
            if (bed.ForOwnerType != wanted) return false;

            // Never evict anyone who isn't part of this couple.
            foreach (Pawn owner in bed.OwnersForReading)
                if (owner != a && owner != b) return false;

            if (!RestUtility.CanUseBedEver(a, bed.def) || !RestUtility.CanUseBedEver(b, bed.def)) return false;
            if (bed.IsForbidden(a) || bed.IsForbidden(b)) return false;
            if (!a.CanReach(bed, PathEndMode.Touch, Danger.Some)) return false;
            if (!b.CanReach(bed, PathEndMode.Touch, Danger.Some)) return false;
            return true;
        }

        // Higher = better. Mirrors what drives sleep-related mood:
        // private bedroom vs barracks, room impressiveness, bed comfort.
        private static float Score(Building_Bed bed, Pawn a, Pawn b)
        {
            float score = 0f;
            Room room = bed.GetRoom();
            if (room == null || room.PsychologicallyOutdoors)
            {
                score -= 100f;
            }
            else
            {
                score += room.GetStat(RoomStatDefOf.Impressiveness);

                int otherBeds = 0;
                foreach (Building_Bed other in room.ContainedBeds)
                    if (other != bed && !other.Medical) otherBeds++;

                if (otherBeds == 0) score += 40f;         // own bedroom, no "slept in barracks"
                else score -= 10f * otherBeds;
            }

            score += bed.GetStatValue(StatDefOf.Comfort) * 20f;

            // Small preference for a bed one of them already owns (less shuffling).
            if (IsOwner(bed, a) || IsOwner(bed, b)) score += 5f;
            return score;
        }

        private static bool IsOwner(Building_Bed bed, Pawn p)
        {
            foreach (Pawn owner in bed.OwnersForReading)
                if (owner == p) return true;
            return false;
        }
    }
}
