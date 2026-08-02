// Every building in the game, as one flat data table.
//
// The rule from CLAUDE.md: adding a building must be a one-line data change and never new
// branching logic. So a definition carries its own numbers AND its own politics, including
// the per-district overrides that make placement a political act rather than a menu choice.

using System.Collections.Generic;
using UnityEngine;

namespace Mesruiyet.Core
{
    public sealed class BuildingDef
    {
        public string Id;
        public string Name;
        /// <summary>Short caption for the build dock, where a tile is 62 px wide.</summary>
        public string Short;
        /// <summary>Hotbar group: konut · sanayi · tarim · altyapi · kamu · ordu.</summary>
        public string Category;
        public string Glyph;

        public string DockLabel => string.IsNullOrEmpty(Short) ? Name : Short;

        public int CostMoney;
        public int CostMaterial;
        public int Upkeep;
        /// <summary>Draws from the single shared İşgücü pool — the pool conscription also raids.</summary>
        public int Workers;

        /// <summary>Per-turn production, indexed by <see cref="Res"/>. Negative means consumption.</summary>
        public float[] Output = new float[6];

        /// <summary>Extra housing capacity, in people.</summary>
        public int Housing;

        public int Health, Education, Security, Culture, Pollution;

        /// <summary>Politics that apply wherever it is built.</summary>
        public PoliticalEffect Base = PoliticalEffect.None;

        /// <summary>Politics that replace <see cref="Base"/> in specific districts.</summary>
        public Dictionary<DistrictId, PoliticalEffect> ByDistrict;

        public Vector2Int Size = Vector2Int.one;
        public Color Tint = Color.gray;
        public int Storeys = 1;
        /// <summary>Terrain this building insists on, or null for anything buildable.</summary>
        public TileKind? Requires;
        public string Blurb;

        /// <summary>The politics of putting this building in that district. §3 of the design.</summary>
        public PoliticalEffect PoliticsIn(DistrictId d)
        {
            if (ByDistrict != null && ByDistrict.TryGetValue(d, out var e)) return e;
            return Base;
        }
    }

    public static class Buildings
    {
        static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static float[] Out(float para = 0, float yiyecek = 0, float su = 0,
                           float enerji = 0, float malzeme = 0, float isgucu = 0)
            => new[] { para, yiyecek, su, enerji, malzeme, isgucu };

        public static readonly BuildingDef[] All =
        {
            new BuildingDef
            {
                Id = "yol", Name = "Yol", Category = "altyapi", Glyph = "═",
                CostMoney = 8, CostMaterial = 3, Upkeep = 1,
                Tint = C("#41454E"), Storeys = 0,
                Base = PoliticalEffect.None,
                Blurb = "Bağlantı olmadan hiçbir bina hizmet vermez.",
            },
            new BuildingDef
            {
                Id = "konut", Name = "Konut", Category = "konut", Glyph = "⌂",
                CostMoney = 60, CostMaterial = 20, Upkeep = 2,
                Housing = 48, Output = Out(isgucu: 22),
                Tint = C("#B9A88C"), Storeys = 2,
                Base = PoliticalEffect.None,
                Blurb = "Nüfus ve işgücü. Kalabalık, konutsuz kalırsa hoşnutsuzluğa döner.",
            },
            new BuildingDef
            {
                Id = "toplukonut", Name = "Toplu Konut", Short = "T. KONUT", Category = "konut", Glyph = "▤",
                CostMoney = 90, CostMaterial = 45, Upkeep = 4,
                Housing = 130, Output = Out(isgucu: 58),
                Tint = C("#9AA3B0"), Storeys = 4,
                Base = new PoliticalEffect
                {
                    Economy = -4, Tuccar = -3, Isci = 4,
                    Tag = "UCUZ VE YOĞUN · +EŞİTLİK, −TÜCCAR",
                },
                ByDistrict = new Dictionary<DistrictId, PoliticalEffect>
                {
                    [DistrictId.Tepe] = new PoliticalEffect
                    {
                        Economy = -7, Tuccar = -8, Isci = 5, Grievance = 9,
                        Tag = "TEPE'YE KURULDU · ARSA DEĞERİ DÜŞTÜ, TÜCCARLAR ÖFKELİ",
                    },
                },
                Blurb = "Herkese çatı. Arsası pahalı olan mahallede bedeli politiktir.",
            },
            new BuildingDef
            {
                Id = "tarla", Name = "Tarla", Category = "tarim", Glyph = "≡",
                CostMoney = 40, CostMaterial = 8, Upkeep = 1, Workers = 18,
                Output = Out(yiyecek: 16, su: -4),
                Requires = TileKind.Verimli,
                Tint = C("#7E9455"), Storeys = 0,
                Base = PoliticalEffect.None,
                Blurb = "Yiyecek zincirinin ilk halkası. Nehir kıyısındaki verimli toprağa kurulur.",
            },
            new BuildingDef
            {
                Id = "degirmen", Name = "Değirmen", Category = "tarim", Glyph = "✳",
                CostMoney = 90, CostMaterial = 30, Upkeep = 3, Workers = 14,
                Output = Out(yiyecek: 10, enerji: -3),
                Tint = C("#C2AE86"), Storeys = 2,
                Base = PoliticalEffect.None,
                Blurb = "Zincirin tıkanan halkası. Ambar dolu görünürken şehir aç kalabilir.",
            },
            new BuildingDef
            {
                Id = "firin", Name = "Fırın", Category = "tarim", Glyph = "◍",
                CostMoney = 70, CostMaterial = 22, Upkeep = 3, Workers = 12,
                Output = Out(yiyecek: 12, enerji: -4),
                Tint = C("#C98F5E"), Storeys = 1,
                Base = PoliticalEffect.None,
                Blurb = "Ekmek buradan çıkar. Kapanırsa bunu herkes aynı gün öğrenir.",
            },
            new BuildingDef
            {
                Id = "ambar", Name = "Tahıl Ambarı", Short = "AMBAR", Category = "tarim", Glyph = "▣",
                CostMoney = 80, CostMaterial = 30, Upkeep = 2,
                Output = Out(yiyecek: 4),
                Tint = C("#A8956E"), Storeys = 2,
                Base = PoliticalEffect.None,
                Blurb = "Tampon. Oyunun tek gerçek para birimi olan güvenlik payını büyütür.",
            },
            new BuildingDef
            {
                Id = "kuyu", Name = "Su Kuyusu", Short = "KUYU", Category = "altyapi", Glyph = "≋",
                CostMoney = 50, CostMaterial = 12, Upkeep = 1, Workers = 4,
                Output = Out(su: 26, enerji: -2),
                Tint = C("#6E8A9C"), Storeys = 1,
                Base = PoliticalEffect.None,
                Blurb = "Susuz mahalle üç turda ayaklanır.",
            },
            new BuildingDef
            {
                Id = "santral", Name = "Enerji Santrali", Short = "SANTRAL", Category = "altyapi", Glyph = "⚡",
                CostMoney = 160, CostMaterial = 55, Upkeep = 8, Workers = 22,
                Output = Out(enerji: 48, malzeme: -2),
                Pollution = 9,
                Tint = C("#77808C"), Storeys = 3,
                Base = new PoliticalEffect { Grievance = 3, Tag = "KİRLETİR · KURULDUĞU MAHALLE ÖDER" },
                ByDistrict = new Dictionary<DistrictId, PoliticalEffect>
                {
                    [DistrictId.Tepe] = new PoliticalEffect
                    {
                        Tuccar = -7, Grievance = 12,
                        Tag = "TEPE'YE KURULDU · MANZARAYI VE TÜCCARLARI KAYBETTİNİZ",
                    },
                    [DistrictId.Sanayi] = new PoliticalEffect
                    {
                        Grievance = 2, Tag = "SANAYİ'YE KURULDU · KİMSE ŞAŞIRMADI",
                    },
                },
                Blurb = "Enerji her şeyi çalıştırır, dumanı da birine düşer.",
            },
            new BuildingDef
            {
                Id = "ocak", Name = "Taş Ocağı", Short = "OCAK", Category = "sanayi", Glyph = "⛰",
                CostMoney = 110, CostMaterial = 20, Upkeep = 4, Workers = 26,
                Output = Out(malzeme: 20, enerji: -4),
                Requires = TileKind.Tepelik,
                Pollution = 5,
                Tint = C("#8A8175"), Storeys = 1,
                Base = new PoliticalEffect { Grievance = 2, Tag = "MALZEME ZİNCİRİNİN BAŞI" },
                Blurb = "Malzeme kesilirse inşaat üç tur sonra durur — o gün değil.",
            },
            new BuildingDef
            {
                Id = "dokuma", Name = "Dokuma Atölyesi", Short = "DOKUMA", Category = "sanayi", Glyph = "⌗",
                CostMoney = 140, CostMaterial = 40, Upkeep = 6, Workers = 40,
                Output = Out(para: 52, enerji: -6),
                Pollution = 6,
                Tint = C("#94513B"), Storeys = 2,
                Base = new PoliticalEffect { Tuccar = 3, Tag = "GELİR GETİRİR" },
                ByDistrict = new Dictionary<DistrictId, PoliticalEffect>
                {
                    [DistrictId.Sanayi] = new PoliticalEffect
                    {
                        Economy = 3, Tuccar = 5,
                        Tag = "SANAYİ'YE KURULDU · +TÜCCAR",
                    },
                    [DistrictId.Liman] = new PoliticalEffect
                    {
                        Economy = 5, Tuccar = 4, Isci = -6, Grievance = 8,
                        Tag = "LİMAN'A KURULDU · +₺ AMA HOŞNUTSUZLUK",
                    },
                },
                Blurb = "Ucuz işgücünün yanına kurulursa daha çok kazandırır. Bedelini oradakiler öder.",
            },
            new BuildingDef
            {
                Id = "karakol", Name = "Karakol", Category = "kamu", Glyph = "⚑",
                CostMoney = 90, CostMaterial = 28, Upkeep = 5, Workers = 12,
                Security = 12,
                Tint = C("#5F6B7A"), Storeys = 1,
                Base = new PoliticalEffect { Order = 3, Ordu = 2, Tag = "+GÜVENLİK" },
                ByDistrict = new Dictionary<DistrictId, PoliticalEffect>
                {
                    [DistrictId.Liman] = new PoliticalEffect
                    {
                        Order = 6, Isci = -7, Ordu = 3, Grievance = 7,
                        Tag = "LİMAN'A KURULDU · −İŞÇİ, +OTORİTE",
                    },
                    [DistrictId.Tepe] = new PoliticalEffect
                    {
                        Tuccar = 5, Ordu = 2,
                        Tag = "TEPE'YE KURULDU · TÜCCARLAR MEMNUN, EKSEN OYNAMADI",
                    },
                },
                Blurb = "Aynı bina, iki mahalle, iki bambaşka anlam.",
            },
            new BuildingDef
            {
                Id = "matbaa", Name = "Matbaa", Category = "kamu", Glyph = "▥",
                CostMoney = 120, CostMaterial = 30, Upkeep = 5, Workers = 10,
                Education = 10,
                Tint = C("#8FA5B8"), Storeys = 2,
                Base = new PoliticalEffect
                {
                    Order = -6, Aydin = 6, Gelenek = -2,
                    Tag = "+ŞEFFAFLIK · BAKANLARIN RAKAMLARI TEMİZLENİR",
                },
                Blurb = "Rakamlarınızı dürüstleştirir. Karşılığında skandalları da yüzünüze çıkarır.",
            },
            new BuildingDef
            {
                Id = "park", Name = "Park", Category = "kamu", Glyph = "❦",
                CostMoney = 50, CostMaterial = 10, Upkeep = 2,
                Health = 4, Culture = 3,
                Tint = C("#5C8A4C"), Storeys = 0,
                Base = new PoliticalEffect { Grievance = -5, Tag = "−HOŞNUTSUZLUK" },
                ByDistrict = new Dictionary<DistrictId, PoliticalEffect>
                {
                    [DistrictId.Tepe] = new PoliticalEffect
                    {
                        Tuccar = 4, Grievance = -6, Tag = "TEPE'YE KURULDU · +TÜCCAR",
                    },
                    [DistrictId.Liman] = new PoliticalEffect
                    {
                        Isci = 5, Tuccar = -3, Grievance = -8,
                        Tag = "LİMAN'A KURULDU · +İŞÇİ, −TÜCCAR (\"NEDEN ONLARA?\")",
                    },
                },
                Blurb = "Ucuz, etkili, ve kime verdiğiniz herkesin dikkatini çeker.",
            },
            new BuildingDef
            {
                Id = "tapinak", Name = "Tapınak", Category = "kamu", Glyph = "⛩",
                CostMoney = 130, CostMaterial = 40, Upkeep = 4,
                Culture = 12,
                Tint = C("#C9A86A"), Storeys = 3,
                Base = new PoliticalEffect
                {
                    Gelenek = 7, Aydin = -4, Grievance = -6,
                    Tag = "+GELENEK, −AYDIN",
                },
                Blurb = "Hoşnutsuzluğu en ucuz söndüren bina, ve en pahalı ittifak.",
            },
            new BuildingDef
            {
                Id = "anit", Name = "Anıt", Category = "kamu", Glyph = "▲",
                CostMoney = 260, CostMaterial = 80, Upkeep = 6,
                Culture = 6,
                Tint = C("#D8D2C4"), Storeys = 4,
                Base = new PoliticalEffect
                {
                    Order = 8, Ordu = 3, Aydin = -6, Gelenek = 3,
                    Tag = "+MEŞRUİYET · +OTORİTE, −AYDIN",
                },
                Blurb = "Meşruiyet satın alır. Neyin anıtı olduğunu sonra siz de merak edeceksiniz.",
            },
            new BuildingDef
            {
                Id = "kisla", Name = "Kışla", Category = "ordu", Glyph = "▮",
                CostMoney = 150, CostMaterial = 50, Upkeep = 9, Workers = 30,
                Security = 8,
                Tint = C("#6E7A6A"), Storeys = 2,
                Base = new PoliticalEffect
                {
                    Order = 5, Ordu = 8, Aydin = -3,
                    Tag = "+GARNİZON · ASKERE ALMAYI AÇAR",
                },
                Blurb = "Sizi kurtaracak kadar büyük ordu, sizi devirecek kadar büyük ordudur.",
            },
        };

        static Dictionary<string, BuildingDef> _byId;

        public static BuildingDef Get(string id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, BuildingDef>(All.Length);
                foreach (var b in All) _byId[b.Id] = b;
            }
            return _byId.TryGetValue(id, out var def) ? def : null;
        }

        /// <summary>Hotbar order, matching the reference mockup's bottom dock.</summary>
        public static readonly string[] Hotbar =
        {
            "konut", "toplukonut", "tarla", "degirmen", "firin", "ambar",
            "kuyu", "santral", "ocak", "dokuma", "karakol", "matbaa",
            "park", "tapinak", "anit", "kisla", "yol",
        };
    }
}




