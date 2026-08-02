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

            // ---- food and land, continued
            new LawDef
            {
                Id = "nadas_zorunlulugu", Name = "Nadas Zorunluluğu", Glyph = "◇",
                Blurb = "Tarlanın bir kısmı her yıl dinlenir. Bu yıl az, her yıl kesin.",
                FarmThroughputMultiplier = 0.88f, GrainCapacityMultiplier = 1.25f,
                GelenekPerTurn = 2, TuccarPerTurn = -1,
            },
            new LawDef
            {
                Id = "tohum_bankasi", Name = "Tohum Bankası", Glyph = "⚬",
                Blurb = "Şehir tohumu toplar ve dağıtır. Kötü yıl daha az kötü geçer.",
                Economy = -4, FarmThroughputMultiplier = 1.12f, TaxMultiplier = 0.95f,
                IsciPerTurn = 2, GelenekPerTurn = 2,
            },
            new LawDef
            {
                Id = "ekmek_narhi", Name = "Ekmek Narhı", Glyph = "◎",
                Blurb = "Ekmeğin fiyatını vali koyar. Fırıncı kâr edemez, kimse aç kalmaz.",
                Economy = -9, FoodDemandMultiplier = 0.92f, TaxMultiplier = 0.9f,
                GrievanceSettleBonus = 2f, IsciPerTurn = 3, TuccarPerTurn = -4,
            },
            new LawDef
            {
                Id = "balikcilik_hakki", Name = "Balıkçılık Hakkı", Glyph = "≈",
                Blurb = "Körfez herkese açıktır. Kimse ruhsat parası ödemez.",
                Economy = -5, FoodDemandMultiplier = 0.94f,
                IsciPerTurn = 3, TuccarPerTurn = -3,
            },

            // ---- labour and industry, continued
            new LawDef
            {
                Id = "calisma_saati", Name = "Çalışma Saati Sınırı", Glyph = "◷",
                Blurb = "Gün on saatle sınırlıdır. Üretim düşer, insanlar yaşar.",
                Economy = -7, ChainThroughputMultiplier = 0.9f, GrievanceSettleBonus = 3f,
                IsciPerTurn = 5, TuccarPerTurn = -4,
            },
            new LawDef
            {
                Id = "cocuk_isciligi_yasagi", Name = "Çocuk İşçiliği Yasağı", Glyph = "✕",
                Blurb = "On beş yaşından küçük çalıştırılamaz. İşgücü küçülür.",
                Order = -3, LabourParticipationDelta = -0.06f, GrievanceSettleBonus = 2f,
                AydinPerTurn = 4, IsciPerTurn = 3, TuccarPerTurn = -4,
            },
            new LawDef
            {
                Id = "lonca_duzeni", Name = "Lonca Düzeni", Glyph = "⌘",
                Blurb = "Zanaata girmek loncadan geçer. Kalite artar, kapı daralır.",
                ChainThroughputMultiplier = 1.1f, LabourParticipationDelta = -0.04f,
                GelenekPerTurn = 4, TuccarPerTurn = 2, AydinPerTurn = -2,
            },
            new LawDef
            {
                Id = "kadin_isgucu", Name = "Kadınların Çalışma Hakkı", Glyph = "◈",
                Blurb = "Her iş her yurttaşa açıktır. Havuz büyür, bazı hane reisleri küser.",
                Order = -5, LabourParticipationDelta = 0.12f,
                AydinPerTurn = 5, IsciPerTurn = 3, GelenekPerTurn = -5,
            },
            new LawDef
            {
                Id = "is_kazasi_tazminati", Name = "İş Kazası Tazminatı", Glyph = "✚",
                Blurb = "Ocakta ölenin ailesi şehirden alır. Pahalı ve tartışmasız.",
                Economy = -6, TaxMultiplier = 0.92f, GrievanceSettleBonus = 3f,
                IsciPerTurn = 6, TuccarPerTurn = -5,
            },
            new LawDef
            {
                Id = "baca_filtresi", Name = "Baca Filtresi Mecburiyeti", Glyph = "◌",
                Blurb = "Her ocak filtre takar. Duman azalır, maliyet artar.",
                Economy = -5, PollutionDelta = -9, BuildCostMultiplier = 1.12f,
                AydinPerTurn = 4, IsciPerTurn = 2, TuccarPerTurn = -5,
            },

            // ---- capital and property, continued
            new LawDef
            {
                Id = "serbest_ticaret", Name = "Serbest Ticaret Kanunu", Glyph = "⇄",
                Blurb = "Gümrük düşer, mal akar. Kazanan da kaybeden de belli olur.",
                Economy = 10, TaxMultiplier = 1.18f, BuildCostMultiplier = 0.92f,
                TuccarPerTurn = 5, IsciPerTurn = -4,
            },
            new LawDef
            {
                Id = "tekel_yasagi", Name = "Tekel Yasağı", Glyph = "⊘",
                Blurb = "Hiçbir hane bir işi tek başına tutamaz. Fiyatlar iner, birileri küser.",
                Economy = -4, TaxMultiplier = 1.05f, GrievanceSettleBonus = 1f,
                AydinPerTurn = 3, IsciPerTurn = 3, TuccarPerTurn = -6,
            },
            new LawDef
            {
                Id = "miras_vergisi", Name = "Veraset Vergisi", Glyph = "⌸",
                Blurb = "Servet el değiştirirken şehir payını alır.",
                Economy = -8, TaxMultiplier = 1.16f,
                IsciPerTurn = 4, AydinPerTurn = 2, TuccarPerTurn = -6, GelenekPerTurn = -3,
            },
            new LawDef
            {
                Id = "kira_tavani", Name = "Kira Tavanı", Glyph = "⌂",
                Blurb = "Kiralar dondurulur. Mahalle rahatlar, kimse yeni ev yapmak istemez.",
                Economy = -9, BuildCostMultiplier = 1.2f, GrievanceSettleBonus = 4f,
                IsciPerTurn = 6, TuccarPerTurn = -7,
            },
            new LawDef
            {
                Id = "imar_serbestisi", Name = "İmar Serbestisi", Glyph = "▤",
                Blurb = "Kim nereye isterse yapar. Şehir hızla büyür, düzensiz büyür.",
                Economy = 7, BuildCostMultiplier = 0.78f, PollutionDelta = 6,
                TuccarPerTurn = 5, AydinPerTurn = -3, GelenekPerTurn = -2,
            },
            new LawDef
            {
                Id = "kamu_ihalesi", Name = "Açık İhale Kanunu", Glyph = "☰",
                Blurb = "Her iş ilan edilir ve açık verilir. Yavaş, ve kimse cebine atamaz.",
                Order = -5, BuildCostMultiplier = 1.1f, TransparencyDelta = 0.12f,
                AydinPerTurn = 4, TuccarPerTurn = -3,
            },

            // ---- order and security, continued
            new LawDef
            {
                Id = "sokak_devriyesi", Name = "Sürekli Devriye Kanunu", Glyph = "⚐",
                Blurb = "Mahalleler gece gündüz devriye görür. Sokak sakin, kimse rahat değil.",
                Order = 9, GrievanceSettleBonus = 2f, LabourParticipationDelta = -0.03f,
                OrduPerTurn = 4, AydinPerTurn = -4, IsciPerTurn = -2,
                RequiresOrderAtLeast = 25,
            },
            new LawDef
            {
                Id = "kimlik_mecburiyeti", Name = "Kimlik Mecburiyeti", Glyph = "▭",
                Blurb = "Herkes kim olduğunu yanında taşır. Kaçak azalır, kayıt büyür.",
                Order = 7, TaxMultiplier = 1.1f,
                OrduPerTurn = 2, AydinPerTurn = -4, GelenekPerTurn = -2,
            },
            new LawDef
            {
                Id = "seyahat_izni", Name = "Seyahat İzni", Glyph = "⊤",
                Blurb = "Şehre girmek ve çıkmak izne bağlıdır.",
                Order = 12, LabourParticipationDelta = -0.05f, Economy = -3,
                OrduPerTurn = 3, TuccarPerTurn = -6, AydinPerTurn = -6,
                RequiresOrderAtLeast = 45,
            },
            new LawDef
            {
                Id = "askeri_mahkeme", Name = "Askerî Mahkeme Yetkisi", Glyph = "⚔",
                Blurb = "Bazı davalar garnizonda görülür. Hızlı ve temyizsiz.",
                Order = 15, TransparencyDelta = -0.14f, GrievanceSettleBonus = 2f,
                OrduPerTurn = 6, AydinPerTurn = -8, IsciPerTurn = -4,
                RequiresOrderAtLeast = 55,
            },

            // ---- liberty and truth, continued
            new LawDef
            {
                Id = "toplanma_hurriyeti", Name = "Toplanma Hürriyeti", Glyph = "☷",
                Blurb = "Meydanlar izne tabi değildir. Şikâyet size doğrudan ulaşır.",
                Order = -11, GrievanceSettleBonus = 2f, TransparencyDelta = 0.1f,
                AydinPerTurn = 5, IsciPerTurn = 4, OrduPerTurn = -3,
            },
            new LawDef
            {
                Id = "bagimsiz_denetim", Name = "Bağımsız Denetim Kanunu", Glyph = "◉",
                Blurb = "Defterleri valiye bağlı olmayan bir kurul tutar. Rakamlar temizlenir.",
                Order = -8, TransparencyDelta = 0.3f, TaxMultiplier = 0.96f,
                AydinPerTurn = 5, TuccarPerTurn = -2,
            },
            new LawDef
            {
                Id = "dilekce_hakki", Name = "Dilekçe Hakkı", Glyph = "✉",
                Blurb = "Her yurttaş valiye doğrudan yazabilir. Çoğu okunmaz, hepsi kaydedilir.",
                Order = -6, TransparencyDelta = 0.12f, GrievanceSettleBonus = 2f,
                AydinPerTurn = 3, GelenekPerTurn = 2,
            },
            new LawDef
            {
                Id = "vicdan_hurriyeti", Name = "Vicdan Hürriyeti", Glyph = "☉",
                Blurb = "Kimse inancından dolayı sorgulanamaz.",
                Order = -7, GrievanceSettleBonus = 2f,
                AydinPerTurn = 4, GelenekPerTurn = -3, IsciPerTurn = 2,
            },
            new LawDef
            {
                // Not to be confused with the charter clause `meclis_ustundur`: that one is written
                // once at founding and cannot be repealed, this one occupies a law slot.
                Id = "veto_hakki_kaldirildi", Name = "Veto Hakkının Kaldırılması", Glyph = "⚖",
                Blurb = "Vali meclisin geçirdiği kanunu veto edemez. Yetkiniz daralır.",
                Order = -14, ExtraDecrees = -1, TransparencyDelta = 0.18f,
                AydinPerTurn = 5, IsciPerTurn = 3, OrduPerTurn = -4,
                RequiresOrderAtMost = 20,
            },

            // ---- the city itself
            new LawDef
            {
                Id = "umumi_sihhat", Name = "Umumi Sıhhat Kanunu", Glyph = "✚",
                Blurb = "Hekim ve ilaç şehrin yükümlülüğüdür. Pahalı, ve salgın geldiğinde ucuz.",
                Economy = -6, TaxMultiplier = 0.9f, GrievanceSettleBonus = 3f,
                AydinPerTurn = 4, IsciPerTurn = 4, TuccarPerTurn = -3,
            },
            new LawDef
            {
                Id = "su_hakki", Name = "Su Hakkı Kanunu", Glyph = "≋",
                Blurb = "Su satılamaz. Kuyular ve kemerler şehrindir.",
                Economy = -7, GrievanceSettleBonus = 2f, TaxMultiplier = 0.95f,
                IsciPerTurn = 5, GelenekPerTurn = 2, TuccarPerTurn = -5,
            },
            new LawDef
            {
                Id = "yol_vergisi", Name = "Yol Vergisi", Glyph = "═",
                Blurb = "Yollar kullanandan alınan parayla yapılır. Sokaklar açılır, cepler daralır.",
                Economy = 4, TaxMultiplier = 1.12f, BuildCostMultiplier = 0.9f,
                TuccarPerTurn = -2, IsciPerTurn = -3, GelenekPerTurn = -1,
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
