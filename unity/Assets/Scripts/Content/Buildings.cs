// Every building in the game, as one flat data table.
//
// The rule from CLAUDE.md: adding a building must be a one-line data change and never new
// branching logic. So a definition carries its own numbers AND its own politics, including
// the per-district overrides that make placement a political act rather than a menu choice.

using System.Collections.Generic;
using UnityEngine;

namespace Mesruiyet.Core
{
    /// <summary>
    /// The silhouette a building is drawn as. Thirty-one entries drawn as one box in thirty-one
    /// tints is thirty-one tints, not thirty-one buildings — from the air a clinic and a library
    /// were the same object. A form is a shape the eye can name from three hundred metres up, and
    /// picking one stays a single line of data, which is the rule this table lives by.
    ///
    /// Every form is built from parts that stand on something reaching the ground. That is not a
    /// convention; CityRenderer measures the lowest vertex of each building and the `siluet`
    /// scenario fails if any of them is above the floor.
    /// </summary>
    public enum Form
    {
        Duz = 0,     // a slab: roads, fields, parks
        Ev,          // pitched roof, chimney, door — a house
        Blok,        // flat top with roof clutter — dense housing, the exchange
        Salon,       // long body, columned porch, shallow roof — civic halls
        Atolye,      // wide shed, saw-tooth roof, chimney — workshops
        Ocak,        // squat kiln with a domed cap and a stack
        Ambar,       // barn with a silo beside it
        Degirmen,    // tapered tower with sails
        Tapinak,     // stepped base, deep roof, ridge ornament
        Anit,        // plinth and obelisk
        Baca,        // a chimney stack on a small plant house
        Kemer,       // an arcade of arches
        Kuyu,        // a low ring with a headframe
        Karakol,     // guard house with a watch tower
        Pazar,       // an open canopy on posts
    }

    public sealed class BuildingDef
    {
        public string Id;

        /// <summary>How it is drawn. See <see cref="Core.Form"/>.</summary>
        public Form Form = Form.Blok;
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

        /// <summary>
        /// Terrain this building must sit next to without standing on. A shipyard belongs on the
        /// bank, not in the river — and the river itself is never buildable, so <see cref="Requires"/>
        /// cannot express it.
        /// </summary>
        public TileKind? Adjacent;
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
                Id = "yol", Form = Form.Duz, Name = "Yol", Category = "altyapi", Glyph = "═",
                CostMoney = 8, CostMaterial = 3, Upkeep = 1,
                Tint = C("#41454E"), Storeys = 0,
                Base = PoliticalEffect.None,
                Blurb = "Bağlantı olmadan hiçbir bina hizmet vermez.",
            },
            new BuildingDef
            {
                Id = "konut", Form = Form.Ev, Name = "Konut", Category = "konut", Glyph = "⌂",
                CostMoney = 60, CostMaterial = 20, Upkeep = 2,
                Housing = 48, Output = Out(isgucu: 22),
                Tint = C("#B9A88C"), Storeys = 2,
                Base = PoliticalEffect.None,
                Blurb = "Nüfus ve işgücü. Kalabalık, konutsuz kalırsa hoşnutsuzluğa döner.",
            },
            new BuildingDef
            {
                Id = "toplukonut", Form = Form.Blok, Name = "Toplu Konut", Short = "T. KONUT", Category = "konut", Glyph = "▤",
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
                Id = "tarla", Form = Form.Duz, Name = "Tarla", Category = "tarim", Glyph = "≡",
                CostMoney = 40, CostMaterial = 8, Upkeep = 1, Workers = 14,
                Output = Out(su: -4),                       // grain itself flows through the chain
                Requires = TileKind.Verimli,
                Tint = C("#7E9455"), Storeys = 0,
                Base = PoliticalEffect.None,
                Blurb = "Yiyecek zincirinin ilk halkası. Nehir kıyısındaki verimli toprağa kurulur.",
            },
            new BuildingDef
            {
                Id = "degirmen", Form = Form.Degirmen, Name = "Değirmen", Category = "tarim", Glyph = "✳",
                CostMoney = 90, CostMaterial = 30, Upkeep = 3, Workers = 14,
                Output = Out(enerji: -3),
                Tint = C("#C2AE86"), Storeys = 2,
                Base = PoliticalEffect.None,
                Blurb = "Zincirin tıkanan halkası. Ambar dolu görünürken şehir aç kalabilir.",
            },
            new BuildingDef
            {
                Id = "firin", Form = Form.Ocak, Name = "Fırın", Category = "tarim", Glyph = "◍",
                CostMoney = 70, CostMaterial = 22, Upkeep = 3, Workers = 12,
                Output = Out(enerji: -4),
                Tint = C("#C98F5E"), Storeys = 1,
                Base = PoliticalEffect.None,
                Blurb = "Ekmek buradan çıkar. Kapanırsa bunu herkes aynı gün öğrenir.",
            },
            new BuildingDef
            {
                Id = "ambar", Form = Form.Ambar, Name = "Tahıl Ambarı", Short = "AMBAR", Category = "tarim", Glyph = "▣",
                CostMoney = 80, CostMaterial = 30, Upkeep = 2,
                Tint = C("#A8956E"), Storeys = 2,
                Base = PoliticalEffect.None,
                Blurb = "Tahıl deposunu büyütür — ama tahıl ekmek değildir. Değirmen tıkalıysa " +
                        "ambar dolar, şehir aç kalır ve defterdeki toplam hiçbir şey belli etmez.",
            },
            new BuildingDef
            {
                Id = "islik", Form = Form.Atolye, Name = "Taş İşliği", Short = "İŞLİK", Category = "sanayi", Glyph = "⛏",
                CostMoney = 100, CostMaterial = 30, Upkeep = 4, Workers = 16,
                Output = Out(enerji: -5),
                Pollution = 3,
                Tint = C("#8A7F6E"), Storeys = 1,
                Base = new PoliticalEffect { Tag = "HAM TAŞI İNŞAAT MALZEMESİNE ÇEVİRİR" },
                Blurb = "Ocak ile depo arasındaki halka. Durursa ham taş birikir ve üç tur " +
                        "sonra hiçbir şey inşa edemezsiniz.",
            },
            new BuildingDef
            {
                Id = "depo", Form = Form.Ambar, Name = "İnşaat Deposu", Short = "DEPO", Category = "altyapi", Glyph = "▦",
                CostMoney = 90, CostMaterial = 35, Upkeep = 3, Workers = 6,
                Tint = C("#9A9384"), Storeys = 2,
                Base = PoliticalEffect.None,
                Blurb = "İnşaat buradan çeker. Deponun büyüklüğü, kesinti anında kaç tur " +
                        "inşaata devam edebileceğinizdir.",
            },
            new BuildingDef
            {
                Id = "kuyu", Form = Form.Kuyu, Name = "Su Kuyusu", Short = "KUYU", Category = "altyapi", Glyph = "≋",
                CostMoney = 50, CostMaterial = 12, Upkeep = 1, Workers = 4,
                Output = Out(su: 26, enerji: -2),
                Tint = C("#6E8A9C"), Storeys = 1,
                Base = PoliticalEffect.None,
                Blurb = "Susuz mahalle üç turda ayaklanır.",
            },
            new BuildingDef
            {
                Id = "santral", Form = Form.Baca, Name = "Enerji Santrali", Short = "SANTRAL", Category = "altyapi", Glyph = "⚡",
                CostMoney = 160, CostMaterial = 55, Upkeep = 8, Workers = 22,
                Output = Out(enerji: 48),
                Pollution = 9,
                Tint = C("#77808C"), Storeys = 3, Size = new Vector2Int(2, 2),
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
                Id = "ocak", Form = Form.Ocak, Name = "Taş Ocağı", Short = "OCAK", Category = "sanayi", Glyph = "⛰",
                CostMoney = 110, CostMaterial = 20, Upkeep = 4, Workers = 22,
                Output = Out(enerji: -4),
                Requires = TileKind.Tepelik,
                Pollution = 5,
                Tint = C("#8A8175"), Storeys = 1,
                Base = new PoliticalEffect { Grievance = 2, Tag = "MALZEME ZİNCİRİNİN BAŞI" },
                Blurb = "Malzeme kesilirse inşaat üç tur sonra durur — o gün değil.",
            },
            new BuildingDef
            {
                Id = "dokuma", Form = Form.Atolye, Name = "Dokuma Atölyesi", Short = "DOKUMA", Category = "sanayi", Glyph = "⌗",
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
                Id = "karakol", Form = Form.Karakol, Name = "Karakol", Category = "kamu", Glyph = "⚑",
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
                Id = "matbaa", Form = Form.Salon, Name = "Matbaa", Category = "kamu", Glyph = "▥",
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
                Id = "park", Form = Form.Duz, Name = "Park", Category = "kamu", Glyph = "❦",
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
                Id = "tapinak", Form = Form.Tapinak, Name = "Tapınak", Category = "kamu", Glyph = "⛩",
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
                Id = "anit", Form = Form.Anit, Name = "Anıt", Category = "kamu", Glyph = "▲",
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
                Id = "kisla", Form = Form.Karakol, Name = "Kışla", Category = "ordu", Glyph = "▮",
                CostMoney = 150, CostMaterial = 50, Upkeep = 9, Workers = 24,
                Security = 8,
                Tint = C("#6E7A6A"), Storeys = 2, Size = new Vector2Int(2, 2),
                Base = new PoliticalEffect
                {
                    Order = 5, Ordu = 8, Aydin = -3,
                    Tag = "+GARNİZON · ASKERE ALMAYI AÇAR",
                },
                Blurb = "Sizi kurtaracak kadar büyük ordu, sizi devirecek kadar büyük ordudur.",
            },

            // ---------------------------------------------------------------- health
            // Grievance has always read `50 − Sağlık`, and until now nothing in the game raised
            // Sağlık except a park at +4. Every city therefore carried a permanent penalty it
            // could not answer. These are the answer.
            new BuildingDef
            {
                Id = "klinik", Form = Form.Salon, Name = "Klinik", Category = "kamu", Glyph = "✚",
                CostMoney = 110, CostMaterial = 25, Upkeep = 5, Workers = 8,
                Health = 14,
                Tint = C("#D6E2E6"), Storeys = 1,
                Base = new PoliticalEffect
                {
                    Aydin = 4, Isci = 3, Grievance = -4,
                    Tag = "+SAĞLIK · +AYDIN, +İŞÇİ",
                },
                ByDistrict = new Dictionary<DistrictId, PoliticalEffect>
                {
                    [DistrictId.Sanayi] = new PoliticalEffect
                    {
                        Isci = 7, Tuccar = -3, Grievance = -8,
                        Tag = "SANAYİ'YE KURULDU · DUMANIN DİBİNDE, İŞÇİLER SAYDI",
                    },
                },
                Blurb = "Salgın geldiğinde kaç kişi kaybedeceğinizi bu bina belirler.",
            },
            new BuildingDef
            {
                Id = "hastane", Form = Form.Salon, Name = "Hastane", Category = "kamu", Glyph = "✢",
                CostMoney = 340, CostMaterial = 95, Upkeep = 14, Workers = 26,
                Health = 34,
                Tint = C("#E3ECEF"), Storeys = 3, Size = new Vector2Int(2, 2),
                Base = new PoliticalEffect
                {
                    Economy = -3, Aydin = 7, Isci = 5, Grievance = -7,
                    Tag = "+++SAĞLIK · PAHALI VE HERKESE AÇIK",
                },
                Blurb = "Şehrin en pahalı binası, ve tek bir kışta bedelini çıkaran tek bina.",
            },

            // ---------------------------------------------------------------- learning
            new BuildingDef
            {
                Id = "okul", Form = Form.Salon, Name = "Okul", Category = "kamu", Glyph = "✎",
                CostMoney = 95, CostMaterial = 22, Upkeep = 4, Workers = 9,
                Education = 13,
                Tint = C("#C6B89C"), Storeys = 2,
                Base = new PoliticalEffect
                {
                    Aydin = 5, Isci = 3, Grievance = -3,
                    Tag = "+EĞİTİM · +AYDIN",
                },
                ByDistrict = new Dictionary<DistrictId, PoliticalEffect>
                {
                    [DistrictId.Liman] = new PoliticalEffect
                    {
                        Isci = 8, Aydin = 4, Grievance = -7,
                        Tag = "LİMAN'A KURULDU · ÇOCUKLAR RIHTIMDAN ÇIKTI",
                    },
                },
                Blurb = "Bu turda hiçbir şey vermez. On beş yılda şehri değiştirir.",
            },
            new BuildingDef
            {
                Id = "kutuphane", Form = Form.Salon, Name = "Kütüphane", Category = "kamu", Glyph = "▤",
                CostMoney = 150, CostMaterial = 38, Upkeep = 6, Workers = 6,
                Education = 9, Culture = 8,
                Tint = C("#A89478"), Storeys = 2,
                Base = new PoliticalEffect
                {
                    Order = -4, Aydin = 8, Gelenek = -3,
                    Tag = "+EĞİTİM, +KÜLTÜR · +AYDIN, −OTORİTE",
                },
                Blurb = "Neyin okunacağına karışmadığınız sürece işe yarar.",
            },
            new BuildingDef
            {
                Id = "hamam", Form = Form.Salon, Name = "Hamam", Category = "kamu", Glyph = "≈",
                CostMoney = 105, CostMaterial = 30, Upkeep = 5,
                Health = 8, Culture = 7, Output = Out(su: -6),
                Tint = C("#B9C6C2"), Storeys = 1,
                Base = new PoliticalEffect
                {
                    Gelenek = 6, Grievance = -7,
                    Tag = "+SAĞLIK, +KÜLTÜR · +GELENEK",
                },
                Blurb = "Suyu çok içer, hoşnutsuzluğu ucuza söndürür.",
            },

            // ---------------------------------------------------------------- commerce
            new BuildingDef
            {
                Id = "pazar", Form = Form.Pazar, Name = "Pazar Yeri", Short = "PAZAR", Category = "sanayi", Glyph = "⌸",
                CostMoney = 80, CostMaterial = 18, Upkeep = 3, Workers = 12,
                Output = Out(para: 16),
                Tint = C("#C2A15E"), Storeys = 1, Size = new Vector2Int(2, 1),
                Base = new PoliticalEffect
                {
                    Economy = 3, Tuccar = 5, Grievance = -4,
                    Tag = "+₺ · +TÜCCAR",
                },
                Blurb = "Ucuz gelir ve kalabalık. Halkın birbirini gördüğü yer de burasıdır.",
            },
            new BuildingDef
            {
                Id = "borsa", Form = Form.Blok, Name = "Serbest Borsa", Short = "BORSA", Category = "sanayi", Glyph = "⌷",
                CostMoney = 300, CostMaterial = 70, Upkeep = 10, Workers = 18,
                Output = Out(para: 62),
                Tint = C("#D4B978"), Storeys = 4,
                Base = new PoliticalEffect
                {
                    Economy = 9, Tuccar = 10, Isci = -6,
                    Tag = "+++₺ · +SERMAYE · KİRALAR YÜKSELİR",
                },
                ByDistrict = new Dictionary<DistrictId, PoliticalEffect>
                {
                    [DistrictId.Liman] = new PoliticalEffect
                    {
                        Economy = 9, Tuccar = 10, Isci = -10, Grievance = 11,
                        Tag = "LİMAN'A KURULDU · KİRALAR ARTTI, MAHALLE ÖDEDİ",
                    },
                },
                Blurb = "Hazineyi en hızlı dolduran bina. Parayı kimin ödediği haritada görünür.",
            },
            new BuildingDef
            {
                Id = "tayinlama", Form = Form.Ambar, Name = "Tayınlama Deposu", Short = "TAYIN", Category = "tarim", Glyph = "⊞",
                CostMoney = 140, CostMaterial = 45, Upkeep = 8, Workers = 10,
                Output = Out(yiyecek: 12),
                Tint = C("#8C9E7E"), Storeys = 2,
                Base = new PoliticalEffect
                {
                    Economy = -8, Tuccar = -7, Isci = 8, Grievance = -11,
                    Tag = "PARASI OLMAYANA DA EKMEK · +EŞİTLİK, −TÜCCAR",
                },
                Blurb = "Kıtlıkta kimsenin aç kalmamasını sağlar. Tüccarlar bunu bir daha unutmaz.",
            },

            // ---------------------------------------------------------------- water and power
            new BuildingDef
            {
                Id = "sukemeri", Form = Form.Kemer, Name = "Su Kemeri", Short = "KEMER", Category = "altyapi", Glyph = "∩",
                CostMoney = 230, CostMaterial = 85, Upkeep = 6,
                Output = Out(su: 48),
                Tint = C("#A9B3BC"), Storeys = 3,
                Base = new PoliticalEffect { Order = 3, Tag = "+++SU" },
                Blurb = "Kuyulardan pahalı, kuyulardan güvenilir. Kuraklık geldiğinde fark edilir.",
            },
            new BuildingDef
            {
                Id = "aritma", Form = Form.Kuyu, Name = "Arıtma", Category = "altyapi", Glyph = "◌",
                CostMoney = 175, CostMaterial = 55, Upkeep = 7, Workers = 8,
                Output = Out(su: 22), Health = 9, Pollution = -8,
                Tint = C("#8FA8AE"), Storeys = 1,
                Base = new PoliticalEffect
                {
                    Aydin = 5, Grievance = -4,
                    Tag = "+SU, +SAĞLIK · −KİRLİLİK",
                },
                Blurb = "Kuyulardaki tadı alır. Hekim heyeti bunu ister.",
            },

            // ---------------------------------------------------------------- the hard edge
            new BuildingDef
            {
                Id = "kontrol", Form = Form.Karakol, Name = "Kontrol Noktası", Short = "KONTROL", Category = "ordu", Glyph = "⊤",
                CostMoney = 70, CostMaterial = 20, Upkeep = 4, Workers = 6,
                Security = 10,
                Tint = C("#6A6E75"), Storeys = 1,
                Base = new PoliticalEffect
                {
                    Order = 6, Ordu = 3, Aydin = -4, Grievance = 7,
                    Tag = "+GÜVENLİK · +OTORİTE · KURULDUĞU MAHALLE ÖDER",
                },
                ByDistrict = new Dictionary<DistrictId, PoliticalEffect>
                {
                    [DistrictId.Universite] = new PoliticalEffect
                    {
                        Order = 6, Ordu = 3, Aydin = -11, Grievance = 10,
                        Tag = "ÜNİVERSİTE'YE KURULDU · AYDINLAR BUNU BİR DAHA UNUTMAZ",
                    },
                },
                Blurb = "Kaçakçılığı keser. Her sabah işe giden herkes de durdurulur.",
            },
            new BuildingDef
            {
                Id = "tersane", Form = Form.Atolye, Name = "Tersane", Category = "ordu", Glyph = "⊿",
                CostMoney = 280, CostMaterial = 90, Upkeep = 12, Workers = 30,
                Output = Out(para: 20), Security = 6, Pollution = 5,
                Adjacent = TileKind.Su,
                Tint = C("#77828C"), Storeys = 2, Size = new Vector2Int(2, 2),
                Base = new PoliticalEffect
                {
                    Order = 4, Ordu = 7, Tuccar = 4, Isci = 3,
                    Tag = "+GARNİZON, +₺ · SUYA KURULUR",
                },
                Blurb = "Abluka geldiğinde limanın açık kalıp kalmayacağını bu bina belirler.",
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
            "konut", "toplukonut", "tarla", "degirmen", "firin", "ambar", "tayinlama",
            "ocak", "islik", "depo", "pazar", "borsa", "dokuma",
            "kuyu", "sukemeri", "aritma", "santral", "yol",
            "klinik", "hastane", "okul", "kutuphane", "hamam", "park", "tapinak", "matbaa", "anit",
            "karakol", "kontrol", "kisla", "tersane",
        };
    }
}






