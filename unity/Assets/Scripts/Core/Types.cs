// The vocabulary the whole game shares. Kept in one file on purpose: every other system
// speaks in these enums, and a reader should be able to learn the nouns in one sitting.

using System;

namespace Mesruiyet.Core
{
    /// <summary>The six stockpiled resources. Index order is the HUD's left-to-right order.</summary>
    public enum Res
    {
        Para = 0,
        Yiyecek = 1,
        Su = 2,
        Enerji = 3,
        Malzeme = 4,
        Isgucu = 5,
    }

    /// <summary>The five factions. Loyalty is never shown as a number — see <see cref="Mood"/>.</summary>
    public enum Faction
    {
        Tuccarlar = 0,
        Isciler = 1,
        Ordu = 2,
        Gelenek = 3,
        Aydinlar = 4,
    }

    /// <summary>
    /// Qualitative faction mood. These are always honest, because you meet these people face
    /// to face — with the single exception of ORDU, which reaches you through your Güvenlik
    /// minister and is therefore the one deadly blind spot.
    /// </summary>
    public enum Mood
    {
        Hosnut = 0,
        Temkinli = 1,
        Kaygili = 2,
        Ofkeli = 3,
        Dusman = 4,
    }

    public enum DistrictId
    {
        Liman = 0,
        Tepe = 1,
        EskiSehir = 2,
        Sanayi = 3,
        Universite = 4,
        Kisla = 5,
    }

    /// <summary>What a tile is made of. Drives both the ground mesh and what may be built.</summary>
    public enum TileKind
    {
        Cayir = 0,      // plain buildable grass
        Verimli = 1,    // fertile soil near the river — farms want this
        Tepelik = 2,    // hills with ore — quarries want this
        Bataklik = 3,   // marsh, must be drained before it can be built on
        Su = 4,         // river, never buildable
        Yol = 5,        // road
    }

    /// <summary>Where a building's political weight lands.</summary>
    [Serializable]
    public struct PoliticalEffect
    {
        /// <summary>Push on OTORİTE(+) ↔ ÖZGÜRLÜK(−).</summary>
        public int Order;
        /// <summary>Push on SERMAYE(+) ↔ EŞİTLİK(−).</summary>
        public int Economy;

        public int Tuccar;
        public int Isci;
        public int Ordu;
        public int Gelenek;
        public int Aydin;

        /// <summary>Immediate grievance change in the district it was placed in.</summary>
        public int Grievance;

        /// <summary>One line the selection card prints, in Turkish. Empty means politically neutral.</summary>
        public string Tag;

        public static PoliticalEffect None => new PoliticalEffect { Tag = string.Empty };
    }

    /// <summary>
    /// A number as the player is allowed to see it. Slice one has no ministers yet, so every
    /// number comes back reliable — but the plumbing is already the only way the UI reads
    /// anything, which is the rule that makes the information system real later.
    /// </summary>
    public readonly struct Reported
    {
        public readonly float Value;
        public readonly bool Reliable;
        /// <summary>Half-width of the displayed range when the figure is too noisy to state.</summary>
        public readonly float Spread;

        public Reported(float value, bool reliable = true, float spread = 0f)
        {
            Value = value;
            Reliable = reliable;
            Spread = spread;
        }
    }

    public static class Naming
    {
        public static readonly string[] ResourceNames =
        {
            "Para", "Yiyecek", "Su", "Enerji", "Malzeme", "İşgücü",
        };

        public static readonly string[] ResourceGlyphs =
        {
            "₺", "◆", "≈", "⚡", "▧", "☗",
        };

        public static readonly string[] FactionNames =
        {
            "Tüccarlar", "İşçiler", "Ordu", "Gelenek", "Aydınlar",
        };

        public static readonly string[] MoodNames =
        {
            "HOŞNUT", "TEMKİNLİ", "KAYGILI", "ÖFKELİ", "DÜŞMAN",
        };

        public static readonly string[] SeasonNames =
        {
            "İLKBAHAR", "YAZ", "SONBAHAR", "KIŞ",
        };

        /// <summary>Band label for an axis value, by absolute magnitude. See PROMPT-CITY §4.</summary>
        public static string Band(int axis)
        {
            int a = axis < 0 ? -axis : axis;
            if (a >= 90) return "DÖNÜŞSÜZ";
            if (a >= 70) return "RADİKAL";
            if (a >= 40) return "KARARLI";
            return "PRAGMATİK";
        }
    }
}

