// The law book: four slots, expandable to seven.
//
// The slot limit is the design. A city that can adopt every good idea has no identity and makes
// no choices; a city with four slots has to decide what it is. Adopting a fifth law means
// repealing one of the four you already live by, and the repeal is usually the harder decision.
//
// Laws are modifiers, not code. Everything below is a multiplier or an increment the simulation
// already knows how to apply, so a new law is one line here and nothing else anywhere.

namespace Mesruiyet.Core
{
    public sealed class LawDef
    {
        public string Id;
        public string Name;
        public string Glyph;
        public string Blurb;

        /// <summary>Permanent push while the law is in a slot.</summary>
        public int Order;
        public int Economy;

        /// <summary>Loyalty drift per turn while it stands. This is how a law makes enemies.</summary>
        public int TuccarPerTurn, IsciPerTurn, OrduPerTurn, GelenekPerTurn, AydinPerTurn;

        // ---- modifiers the simulation reads directly
        public float TaxMultiplier = 1f;
        public float ChainThroughputMultiplier = 1f;
        public float FarmThroughputMultiplier = 1f;
        public float BuildCostMultiplier = 1f;
        public float FoodDemandMultiplier = 1f;
        public float LabourParticipationDelta;
        public float TransparencyDelta;
        public float GrainCapacityMultiplier = 1f;
        public float GrievanceSettleBonus;
        public int PollutionDelta;
        /// <summary>Extra decrees per turn.</summary>
        public int ExtraDecrees;

        /// <summary>Minimum band on an axis before the council will even hear it.</summary>
        public int RequiresOrderAtLeast = -100;
        public int RequiresOrderAtMost = 100;
    }

    public static class Laws
    {
        public const int BaseSlots = 4;
        public const int MaxSlots = 7;

        public static readonly LawDef[] All =
        {
            // ---- food and land
            new LawDef
            {
                Id = "zorunlu_stok", Name = "Zorunlu Tahıl Stoku", Glyph = "▣",
                Blurb = "Her hasadın bir kısmı ambara ayrılır. Ambar büyür, gelir düşer.",
                GrainCapacityMultiplier = 1.4f, TaxMultiplier = 0.94f,
                GelenekPerTurn = 1,
            },
            new LawDef
            {
                Id = "toprak_reformu", Name = "Toprak Reformu", Glyph = "≡",
                Blurb = "Büyük araziler bölünür ve işleyene verilir.",
                Economy = -9, FarmThroughputMultiplier = 1.25f,
                TuccarPerTurn = -3, IsciPerTurn = 3,
            },
            new LawDef
            {
                Id = "tayinlama_kanunu", Name = "Tayınlama Kanunu", Glyph = "🍞",
                Blurb = "Ekmek karneye bağlanır. Herkes yer, kimse doymaz.",
                Economy = -8, FoodDemandMultiplier = 0.78f,
                TuccarPerTurn = -3, IsciPerTurn = 2,
            },

            // ---- labour and industry
            new LawDef
            {
                Id = "grev_yasagi", Name = "Grev Yasağı", Glyph = "⚒",
                Blurb = "İş bırakma eylemi kaldırılır. Üretim düzenli akar.",
                Order = 9, ChainThroughputMultiplier = 1.12f,
                IsciPerTurn = -4, AydinPerTurn = -2, TuccarPerTurn = 2,
            },
            new LawDef
            {
                Id = "asgari_ucret", Name = "Asgari Ücret", Glyph = "₺",
                Blurb = "Ücretlere alt sınır konur.",
                Economy = -8, TaxMultiplier = 0.9f, GrievanceSettleBonus = 1.5f,
                IsciPerTurn = 4, TuccarPerTurn = -3,
            },
            new LawDef
            {
                Id = "sanayi_tesviki", Name = "Sanayi Teşviki", Glyph = "⌗",
                Blurb = "Atölyelere ucuz kredi ve arsa verilir.",
                Economy = 7, ChainThroughputMultiplier = 1.18f, PollutionDelta = 4,
                TuccarPerTurn = 3, IsciPerTurn = -1,
            },
            new LawDef
            {
                Id = "mulkiyet", Name = "Mülkiyet Dokunulmazlığı", Glyph = "⚖",
                Blurb = "Mülke el konulamaz. İnşaat ucuzlar, elinizi bağlar.",
                Economy = 10, BuildCostMultiplier = 0.88f,
                TuccarPerTurn = 4, IsciPerTurn = -2,
            },
            new LawDef
            {
                Id = "servet_vergisi", Name = "Geçici Servet Vergisi", Glyph = "◈",
                Blurb = "Büyük servetlerden bir defalık pay alınır. Bir defalık kalmaz.",
                Economy = -7, TaxMultiplier = 1.28f,
                TuccarPerTurn = -5, IsciPerTurn = 2,
            },
            new LawDef
            {
                Id = "liman_imtiyazi", Name = "Liman İmtiyazı", Glyph = "⚓",
                Blurb = "Liman işletmesi bir şirkete devredilir. Gelir artar, rıhtım küser.",
                Economy = 9, TaxMultiplier = 1.2f,
                TuccarPerTurn = 4, IsciPerTurn = -4,
            },

            // ---- order
            new LawDef
            {
                Id = "kontrol_noktalari", Name = "Kontrol Noktaları Kanunu", Glyph = "⚑",
                Blurb = "Mahalle girişlerinde kayıt tutulur. Sokak sakinleşir.",
                Order = 11, GrievanceSettleBonus = 2.5f,
                AydinPerTurn = -4, OrduPerTurn = 2, IsciPerTurn = -2,
            },
            new LawDef
            {
                Id = "zorunlu_askerlik", Name = "Zorunlu Askerlik", Glyph = "🪖",
                Blurb = "Her hane bir asker verir. Tarlada eksilir.",
                Order = 8, LabourParticipationDelta = -0.07f,
                OrduPerTurn = 5, IsciPerTurn = -2, AydinPerTurn = -2,
            },
            new LawDef
            {
                Id = "olaganustu_hal", Name = "Olağanüstü Hâl", Glyph = "!",
                Blurb = "Vali kararnameleri meclise uğramadan yürürlüğe girer.",
                Order = 14, ExtraDecrees = 2, TransparencyDelta = -0.18f,
                AydinPerTurn = -5, IsciPerTurn = -3, OrduPerTurn = 3,
                RequiresOrderAtLeast = 40,
            },

            // ---- freedom and truth
            new LawDef
            {
                Id = "serbest_basin", Name = "Serbest Basın Kanunu", Glyph = "▥",
                Blurb = "Gazeteler izin almadan basar. Rakamlarınız temizlenir, sırlarınız da.",
                Order = -9, TransparencyDelta = 0.26f,
                AydinPerTurn = 4, OrduPerTurn = -2,
            },
            new LawDef
            {
                Id = "acik_meclis", Name = "Açık Meclis Kanunu", Glyph = "☷",
                Blurb = "Meclis oturumları herkese açıktır. Şikâyetler size daha çabuk ulaşır.",
                Order = -7, TransparencyDelta = 0.16f, GrievanceSettleBonus = 1f,
                AydinPerTurn = 3, GelenekPerTurn = 1,
            },
            new LawDef
            {
                Id = "yerel_yonetim", Name = "Yerel Yönetim Kanunu", Glyph = "◫",
                Blurb = "Mahalleler kendi işlerini görür. Onay almak zaman alır.",
                Order = -10, BuildCostMultiplier = 1.18f, GrievanceSettleBonus = 3f,
                IsciPerTurn = 2, GelenekPerTurn = 2, TuccarPerTurn = -2,
            },
            new LawDef
            {
                Id = "zorunlu_egitim", Name = "Zorunlu Eğitim", Glyph = "✎",
                Blurb = "Her çocuk okula gider. Bugünün işgücü, yarının şehri için azalır.",
                Order = -4, LabourParticipationDelta = -0.04f, TaxMultiplier = 0.94f,
                AydinPerTurn = 4, GelenekPerTurn = -2,
            },
        };

        public static LawDef Get(string id)
        {
            foreach (var l in All)
                if (l.Id == id) return l;
            return null;
        }
    }
}
