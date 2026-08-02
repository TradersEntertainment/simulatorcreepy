// The founding charter: three clauses out of twelve, chosen on turn five.
//
// This is the main replayability lever in the design, and it works by *removing* options rather
// than adding them. Each clause sets run-long multipliers and, more importantly, one locked
// axis floor or ceiling — a wall the term can never cross however the crises go. A city that
// wrote "Söz Serbesttir" into its charter cannot legislate its way to full authority no matter
// how badly it needs to, and that constraint is the whole point: you are choosing, on turn
// five, which emergency answers you will not be allowed to reach for on turn forty.

namespace Mesruiyet.Core
{
    public sealed class ClauseDef
    {
        public string Id;
        public string Name;
        public string Blurb;

        public float TaxMultiplier = 1f;
        public float ChainThroughputMultiplier = 1f;
        public float BuildCostMultiplier = 1f;
        public float FoodDemandMultiplier = 1f;
        public float GrievanceSettleBonus;
        public float TransparencyDelta;
        public int ExtraLawSlots;

        /// <summary>Hard walls on the axes for the whole run. −100/100 means no wall.</summary>
        public int OrderFloor = -100, OrderCeiling = 100;
        public int EconomyFloor = -100, EconomyCeiling = 100;
    }

    public static class Constitution
    {
        /// <summary>The charter is written on this turn and never again.</summary>
        public const int Turn = 5;
        public const int Picks = 3;

        public static readonly ClauseDef[] All =
        {
            new ClauseDef
            {
                Id = "mulkiyet_kutsal", Name = "Mülkiyet Kutsaldır",
                Blurb = "Kimsenin malına el konulamaz. İnşaat ucuzlar; ambarlara el koymak " +
                        "bir daha asla mümkün olmaz.",
                BuildCostMultiplier = 0.9f, TaxMultiplier = 0.92f,
                EconomyFloor = -40,
            },
            new ClauseDef
            {
                Id = "herkese_ekmek", Name = "Herkese Ekmek",
                Blurb = "Şehir kimseyi aç bırakmaz. Tüketim düşer, sermaye birikmez.",
                FoodDemandMultiplier = 0.86f, GrievanceSettleBonus = 1.5f,
                EconomyCeiling = 45,
            },
            new ClauseDef
            {
                Id = "soz_serbest", Name = "Söz Serbesttir",
                Blurb = "Basın izin almaz, meclis kapanmaz. Rakamlarınız temiz kalır ve " +
                        "otoriteyi sonuna kadar götüremezsiniz.",
                TransparencyDelta = 0.22f,
                OrderCeiling = 55,
            },
            new ClauseDef
            {
                Id = "sehir_savunur", Name = "Şehir Kendini Savunur",
                Blurb = "Savunma bir vatandaşlık ödevidir. Garnizon ucuz kurulur, " +
                        "ordu her zaman masada oturur.",
                ChainThroughputMultiplier = 0.95f,
                OrderFloor = -45,
            },
            new ClauseDef
            {
                Id = "meclis_ustundur", Name = "Meclis Üstündür",
                Blurb = "Vali meclise hesap verir. Bir yasa yuvası daha açılır, " +
                        "kararname ile yönetmek imkânsızlaşır.",
                ExtraLawSlots = 1, TransparencyDelta = 0.12f,
                OrderCeiling = 70,
            },
            new ClauseDef
            {
                Id = "toprak_isleyenin", Name = "Toprak İşleyenindir",
                Blurb = "Arazi işleyene aittir. Tarla verimi artar, büyük mülk oluşamaz.",
                ChainThroughputMultiplier = 1.08f,
                EconomyCeiling = 55,
            },
            new ClauseDef
            {
                Id = "ticaret_serbest", Name = "Ticaret Serbesttir",
                Blurb = "Şehir alım satıma karışmaz. Vergi geliri yükselir, " +
                        "fiyat tavanı koymak bir daha mümkün olmaz.",
                TaxMultiplier = 1.18f,
                EconomyFloor = -25,
            },
            new ClauseDef
            {
                Id = "her_haneye_cati", Name = "Her Haneye Çatı",
                Blurb = "Barınma bir haktır. Konut ucuzlar, arsa değeriyle kazanılamaz.",
                BuildCostMultiplier = 0.88f, GrievanceSettleBonus = 1f,
                EconomyCeiling = 60,
            },
            new ClauseDef
            {
                Id = "vali_yetkilidir", Name = "Vali Yetkilidir",
                Blurb = "Olağanüstü hâlde vali tek başına karar verir. Hızlı yönetirsiniz " +
                        "ve özgürlük tarafına bir daha geçemezsiniz.",
                BuildCostMultiplier = 0.94f,
                OrderFloor = -20,
            },
            new ClauseDef
            {
                Id = "okul_zorunlu", Name = "Okul Zorunludur",
                Blurb = "Her çocuk okur. Bugünün işgücü azalır, şehrin aklı artar.",
                ChainThroughputMultiplier = 0.94f, TransparencyDelta = 0.1f,
                OrderCeiling = 75,
            },
            new ClauseDef
            {
                Id = "kimse_borclandiramaz", Name = "Şehir Rehin Verilemez",
                Blurb = "Hiçbir alacaklı yasa yuvası isteyemez — ama kimse de bu şehre " +
                        "kolay borç vermez.",
                TaxMultiplier = 0.94f,
                EconomyCeiling = 70,
            },
            new ClauseDef
            {
                Id = "mahalleler_ozerktir", Name = "Mahalleler Özerktir",
                Blurb = "Her mahalle kendi işini görür. Huzursuzluk çabuk yatışır, " +
                        "inşaat onay bekler.",
                BuildCostMultiplier = 1.15f, GrievanceSettleBonus = 3f,
                OrderCeiling = 60,
            },
        };

        public static ClauseDef Get(string id)
        {
            foreach (var c in All)
                if (c.Id == id) return c;
            return null;
        }
    }
}
