// The six districts, as data. Adding or moving one is a one-line change here and nothing
// else in the codebase needs to know.

using UnityEngine;

namespace Mesruiyet.Core
{
    public sealed class DistrictDef
    {
        public DistrictId Id;
        public string Name;
        /// <summary>Tile-space rectangle on the 48×32 grid: xMin, yMin, width, height.</summary>
        public RectInt Bounds;
        /// <summary>The faction whose fortunes this district is tied to.</summary>
        public Faction Affinity;
        public int StartPopulation;
        /// <summary>0..1 — how much discomfort this district tolerates before grievance climbs.</summary>
        public float Patience;
        /// <summary>Local wealth, 0..100. Drives building palette and tax yield.</summary>
        public int Wealth;
        /// <summary>Base building colours, so each quarter reads differently from the air.</summary>
        public Color[] Palette;
        public Vector2Int Storeys;
        /// <summary>One line of character the selection card and telegrams can borrow.</summary>
        public string Flavour;
    }

    public static class Districts
    {
        static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static readonly DistrictDef[] All =
        {
            new DistrictDef
            {
                Id = DistrictId.Liman,
                Name = "LİMAN",
                Bounds = new RectInt(2, 2, 15, 10),
                Affinity = Faction.Isciler,
                StartPopulation = 92,
                Patience = 0.45f,            // tolerates discomfort, not injustice
                Wealth = 24,
                Palette = new[] { C("#7E8AA6"), C("#98A3BD"), C("#6B7A96"), C("#B85C3F"), C("#A34A3C") },
                Storeys = new Vector2Int(1, 2),
                Flavour = "Rıhtım, ambarlar, ve her şeyi ilk duyan insanlar.",
            },
            new DistrictDef
            {
                Id = DistrictId.Sanayi,
                Name = "SANAYİ",
                Bounds = new RectInt(18, 2, 14, 10),
                Affinity = Faction.Isciler,
                StartPopulation = 72,
                Patience = 0.5f,
                Wealth = 30,
                Palette = new[] { C("#8A6F52"), C("#A0805C"), C("#75604A"), C("#A8492F"), C("#93724E") },
                Storeys = new Vector2Int(1, 2),
                Flavour = "Duman, vardiya düdüğü, ve şehrin geri kalanının görmediği eller.",
            },
            new DistrictDef
            {
                Id = DistrictId.Universite,
                Name = "ÜNİVERSİTE",
                Bounds = new RectInt(2, 14, 15, 10),
                Affinity = Faction.Aydinlar,
                StartPopulation = 56,
                Patience = 0.6f,
                Wealth = 46,
                Palette = new[] { C("#B9BFC9"), C("#CDD3DC"), C("#A6ADB8"), C("#8FA5B8"), C("#D6DAE2") },
                Storeys = new Vector2Int(2, 3),
                Flavour = "Okullar, matbaa, klinik. Yanlış bir rakamı ilk buradan duyarsınız.",
            },
            new DistrictDef
            {
                Id = DistrictId.EskiSehir,
                Name = "ESKİ ŞEHİR",
                Bounds = new RectInt(18, 14, 14, 10),
                Affinity = Faction.Gelenek,
                StartPopulation = 104,
                Patience = 0.55f,
                Wealth = 38,
                Palette = new[] { C("#C9A76A"), C("#DCC084"), C("#B08D5C"), C("#B85C3F"), C("#D2A45E") },
                Storeys = new Vector2Int(1, 3),
                Flavour = "Çarşı ve tapınak. Burada hiçbir şey hızlı değişmez, değişirse de affedilmez.",
            },
            new DistrictDef
            {
                Id = DistrictId.Tepe,
                Name = "TEPE",
                Bounds = new RectInt(33, 12, 13, 14),
                Affinity = Faction.Tuccarlar,
                StartPopulation = 44,
                Patience = 0.25f,            // tolerates injustice, not discomfort
                Wealth = 78,
                Palette = new[] { C("#D8DCE4"), C("#E6E9EF"), C("#C4CAD6"), C("#A8B49C"), C("#EDEFF3") },
                Storeys = new Vector2Int(2, 4),
                Flavour = "Bahçeli evler ve manzara. Rahatsızlığa tahammülleri yoktur.",
            },
            new DistrictDef
            {
                Id = DistrictId.Kisla,
                Name = "KIŞLA",
                Bounds = new RectInt(18, 25, 14, 5),
                Affinity = Faction.Ordu,
                StartPopulation = 32,
                Patience = 0.7f,
                Wealth = 34,
                Palette = new[] { C("#6E7A6A"), C("#7E8A78"), C("#5E6A5A"), C("#8A8F72"), C("#909A88") },
                Storeys = new Vector2Int(1, 2),
                Flavour = "Garnizon ve depolar. Buradan gelen haber daima bir başkasının ağzından gelir.",
            },
        };

        static DistrictDef[] _byId;

        /// <summary>
        /// Look up by enum, not by position. The declaration order of All is editorial — it
        /// reads south-to-north — and it does not match DistrictId's numbering. Indexing All
        /// with (int)id silently hands you a different district, which is exactly the kind of
        /// bug that shows up as a label swap and gets blamed on the UI.
        /// </summary>
        public static DistrictDef Get(DistrictId id)
        {
            if (_byId == null)
            {
                _byId = new DistrictDef[All.Length];
                foreach (var d in All) _byId[(int)d.Id] = d;
            }
            return _byId[(int)id];
        }

        /// <summary>Which district owns a tile, or null for the unclaimed river and hinterland.</summary>
        public static DistrictDef At(int x, int y)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Bounds.Contains(new Vector2Int(x, y)))
                    return All[i];
            return null;
        }
    }
}

