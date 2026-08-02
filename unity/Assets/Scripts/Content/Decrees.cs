// Decrees: two per turn, immediate, and the engine of drift.
//
// Design pillar two, and the hardest rule in the whole document to hold: the player must never
// choose "become a dictator". So every entry below is named and described by *what it does* —
// never as extreme, never as harsh, never with a warning label. Requisitioning grain is called
// requisitioning grain. Twelve reasonable emergency measures later there are checkpoints on the
// streets and nobody ever picked authoritarianism off a menu.
//
// The pairs matter more than the individual cards. Almost every problem here has a clean answer
// that costs money you may not have, and a fast answer that costs a few points of an axis you
// are not watching. Under pressure players take the fast one. That is the whole game.

namespace Mesruiyet.Core
{
    /// <summary>The handful of decrees that need more than a number change.</summary>
    public enum DecreeEffect
    {
        None = 0,
        BuyGrain,          // spend treasury, fill the granary
        SeizeGrain,        // fill it for free, out of somebody's barn
        Rationing,         // cut food demand for a few turns
        Overtime,          // raise chain throughput for a turn
        LabourSurge,       // add to the labour pool for a turn
        ImportMaterial,    // fill the depot
        OpenSession,       // a temporary transparency bump
        PressDirective,    // a lasting transparency cut
        CalmStreets,       // grievance down now, resentment later
    }

    public sealed class DecreeDef
    {
        public string Id;
        public string Name;
        /// <summary>What it does, in plain Turkish. Never a moral label.</summary>
        public string Blurb;

        public int CostMoney;
        public int CostLegitimacy;

        /// <summary>OTORİTE(+) ↔ ÖZGÜRLÜK(−).</summary>
        public int Order;
        /// <summary>SERMAYE(+) ↔ EŞİTLİK(−).</summary>
        public int Economy;

        public int Tuccar, Isci, Ordu, Gelenek, Aydin;

        /// <summary>Immediate grievance change in every district.</summary>
        public int Grievance;

        public DecreeEffect Effect = DecreeEffect.None;
        public float Magnitude;

        /// <summary>Turns the effect lasts, where that applies.</summary>
        public int Duration = 1;
    }

    public static class Decrees
    {
        /// <summary>Two a turn, unless a law says otherwise.</summary>
        public const int PerTurn = 2;

        public static readonly DecreeDef[] All =
        {
            // ---- food: the clean answer and the fast one
            new DecreeDef
            {
                Id = "tahil_al", Name = "Tahıl Satın Al",
                Blurb = "Komşu kasabalardan tahıl alınır ve ambara konur.",
                CostMoney = 240,
                Effect = DecreeEffect.BuyGrain, Magnitude = 120,
            },
            new DecreeDef
            {
                Id = "tahil_el_koy", Name = "Ambarlara El Konulması",
                Blurb = "Özel ambarlardaki tahıl sayılır ve şehir ambarına aktarılır.",
                Order = 6, Tuccar = -7, Gelenek = -3, Grievance = 6,
                Effect = DecreeEffect.SeizeGrain, Magnitude = 150,
            },
            new DecreeDef
            {
                Id = "tayin", Name = "Tayınlama",
                Blurb = "Ekmek kişi başına paylaştırılır; tüketim düşer.",
                Economy = -5, Isci = 4, Tuccar = -4,
                Effect = DecreeEffect.Rationing, Magnitude = 0.25f, Duration = 3,
            },

            // ---- production
            new DecreeDef
            {
                Id = "fazla_mesai", Name = "Fazla Mesai Buyruğu",
                Blurb = "Değirmen ve fırınlar bir tur boyunca uzun vardiya çalışır.",
                CostMoney = 60, Order = 3, Isci = -5,
                Effect = DecreeEffect.Overtime, Magnitude = 0.35f,
            },
            new DecreeDef
            {
                Id = "isgucu_seferberligi", Name = "İşgücü Seferberliği",
                Blurb = "Bir tur boyunca herkes bir işe yazılır.",
                Order = 4, Isci = -6, Aydin = -3,
                Effect = DecreeEffect.LabourSurge, Magnitude = 60,
            },
            new DecreeDef
            {
                Id = "malzeme_ithal", Name = "Malzeme İthalatı",
                Blurb = "Depoya dışarıdan işlenmiş malzeme alınır.",
                CostMoney = 200, Economy = 3,
                Effect = DecreeEffect.ImportMaterial, Magnitude = 110,
            },

            // ---- money
            new DecreeDef
            {
                Id = "bagis", Name = "Bağış Kampanyası",
                Blurb = "Tüccarlardan ve loncalardan bağış toplanır.",
                CostLegitimacy = 4, Tuccar = -3,
                Effect = DecreeEffect.None, Magnitude = 200,
            },
            new DecreeDef
            {
                Id = "serbest_fiyat", Name = "Fiyatların Serbest Bırakılması",
                Blurb = "Fiyat sınırları kaldırılır; ticaret canlanır, kira yükselir.",
                Economy = 7, Tuccar = 6, Isci = -5, Grievance = 5,
            },
            new DecreeDef
            {
                Id = "fiyat_tavani", Name = "Fiyat Tavanı",
                Blurb = "Temel malların fiyatı sabitlenir.",
                CostMoney = 80, Economy = -7, Isci = 6, Tuccar = -6, Grievance = -4,
            },
            new DecreeDef
            {
                Id = "vergi_affi", Name = "Vergi Affı",
                Blurb = "Birikmiş vergi borçları silinir.",
                CostMoney = 160, Economy = 4, Tuccar = 5, Grievance = -3,
            },

            // ---- order and streets
            new DecreeDef
            {
                Id = "sokaga_cikma", Name = "Sokağa Çıkma Sınırlaması",
                Blurb = "Akşam saatlerinde sokaklar boşaltılır. Huzursuzluk bir süre görünmez olur.",
                Order = 6, Aydin = -5, Ordu = 3,
                Effect = DecreeEffect.CalmStreets, Magnitude = 14, Duration = 3,
            },
            new DecreeDef
            {
                Id = "mahalle_fonu", Name = "Mahalle Fonu",
                Blurb = "En huzursuz mahalleye doğrudan ödenek aktarılır.",
                CostMoney = 180, Economy = -3, Isci = 3, Grievance = -9,
            },
            new DecreeDef
            {
                Id = "temizlik", Name = "Temizlik Seferberliği",
                Blurb = "Kanallar ve bacalar temizlenir.",
                CostMoney = 90, Grievance = -4, Gelenek = 2,
            },

            // ---- information: the two that decide whether you can see anything at all
            new DecreeDef
            {
                Id = "acik_kurul", Name = "Açık Kurul Toplantısı",
                Blurb = "Defterler meclise açılır, rakamlar bir süre daha temiz gelir.",
                CostMoney = 40, CostLegitimacy = 2, Order = -5, Aydin = 5,
                Effect = DecreeEffect.OpenSession, Magnitude = 0.22f, Duration = 4,
            },
            new DecreeDef
            {
                Id = "basin_talimatnamesi", Name = "Basın Talimatnamesi",
                Blurb = "Gazetelere neyin basılacağı hakkında talimat gönderilir.",
                Order = 5, Aydin = -7, Gelenek = 2,
                Effect = DecreeEffect.PressDirective, Magnitude = 0.14f, Duration = 6,
            },

            // ---- the army
            new DecreeDef
            {
                Id = "asker_alimi", Name = "Asker Alımı",
                Blurb = "Garnizon büyütülür. İşgücü tarlalardan çekilir.",
                Order = 4, Ordu = 8, Isci = -4,
                Effect = DecreeEffect.LabourSurge, Magnitude = -50, Duration = 4,
            },
            new DecreeDef
            {
                Id = "nutuk", Name = "Nutuk",
                Blurb = "Meydanda konuşulur. İnsanlar bir süre daha bekler.",
                Order = 2, Gelenek = 3, Aydin = -2, Grievance = -3,
                Effect = DecreeEffect.None, Magnitude = 0,
            },
            new DecreeDef
            {
                Id = "imar_affi", Name = "İmar Affı",
                Blurb = "Ruhsatsız yapılar kayda geçirilir; harç geliri toplanır.",
                Economy = 6, Tuccar = 4, Aydin = -4, Grievance = 3,
                Effect = DecreeEffect.None, Magnitude = 140,
            },

            new DecreeDef
            {
                Id = "gumruk_indirimi", Name = "Gümrük İndirimi",
                Blurb = "Gümrük bir süre düşürülür. Mal akar, hazine daha az alır.",
                CostMoney = 60, Economy = 5, Tuccar = 6, Isci = -3,
                Effect = DecreeEffect.ImportMaterial, Magnitude = 90,
            },
            new DecreeDef
            {
                Id = "istikraz", Name = "İç İstikraz",
                Blurb = "Şehrin kendi eşrafından borç alınır. Bugün para, yarın minnet.",
                Economy = 4, Tuccar = -4, Gelenek = 2, CostLegitimacy = 3,
                Effect = DecreeEffect.None, Magnitude = 320,
            },
            new DecreeDef
            {
                Id = "hekim_seferberligi", Name = "Hekim Seferberliği",
                Blurb = "Bütün hekimler mahallelere dağıtılır. Pahalı ve etkili.",
                CostMoney = 220, Economy = -4, Aydin = 5, Isci = 4, Grievance = -8,
            },
            new DecreeDef
            {
                Id = "yol_calismasi", Name = "Yol Çalışması Buyruğu",
                Blurb = "İşgücü sokaklara verilir. Yollar açılır, atölyeler bir tur boşalır.",
                Order = 3, Isci = 2, Tuccar = 3,
                Effect = DecreeEffect.LabourSurge, Magnitude = -35, Duration = 2,
            },
            new DecreeDef
            {
                Id = "genel_af", Name = "Genel Af",
                Blurb = "Tutukluların bir kısmı salıverilir. Sokak rahatlar, garnizon somurtur.",
                Order = -6, Aydin = 7, Isci = 4, Ordu = -5, Grievance = -7,
                CostLegitimacy = 3,
            },
            new DecreeDef
            {
                Id = "olaganustu_arama", Name = "Genel Arama Buyruğu",
                Blurb = "Haneler aranır. Kaçak ve silah bulunur, mahalle bunu hatırlar.",
                Order = 9, Ordu = 5, Aydin = -8, Isci = -5, Grievance = 8,
                Effect = DecreeEffect.None, Magnitude = 180,
            },
            new DecreeDef
            {
                Id = "sansur_talimati", Name = "Telgraf Denetimi",
                Blurb = "Giden gelen telgraf okunur. Dışarı sızan haber azalır.",
                Order = 7, Aydin = -7, Ordu = 3,
                Effect = DecreeEffect.PressDirective, Magnitude = 0.1f, Duration = 6,
            },
            new DecreeDef
            {
                Id = "bayram", Name = "Bayram İlanı",
                Blurb = "Üç gün tatil. Kimse çalışmaz, herkes biraz daha iyi hisseder.",
                CostMoney = 130, Gelenek = 6, Isci = 3, Grievance = -9,
                Effect = DecreeEffect.LabourSurge, Magnitude = -25, Duration = 1,
            },
        };

        public static DecreeDef Get(string id)
        {
            foreach (var d in All)
                if (d.Id == id) return d;
            return null;
        }
    }
}


