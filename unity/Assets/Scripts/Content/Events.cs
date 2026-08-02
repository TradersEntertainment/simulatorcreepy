// Event cards: the outside world arriving at the gate.
//
// The shape of every card is the same and it is the design's central bargain — a slow clean
// option that costs money or standing you may not have, and a fast one that costs a few points
// of an axis nobody is watching. Under pressure players take the fast one. Nothing is labelled.
//
// Tone: comic characters, ice-cold consequences. The bull in the market is funny. The famine
// that follows because you spent the treasury on the bull is not, and the ledger never winks.

namespace Mesruiyet.Core
{
    public enum EventEffect
    {
        None = 0,
        Refugees,        // adds population and labour, and mouths
        Grain,           // adds or removes grain
        Requisition,     // takes grain from private stores
        Raid,            // resolves against the garrison
        Tribute,         // pays Mersa off
        Conscript,       // moves labour into the garrison
        KadraCollapse,   // the mid-game squeeze
    }

    public sealed class EventOption
    {
        public string Label;
        /// <summary>What it does, in the player's words. Never a verdict.</summary>
        public string Blurb;

        public int CostMoney;
        public int CostLegitimacy;

        public int Order, Economy;
        public int Tuccar, Isci, Ordu, Gelenek, Aydin;
        public int Grievance;

        public int Threat;
        public int Garrison;

        public EventEffect Effect = EventEffect.None;
        public float Magnitude;

        /// <summary>Hidden unless the garrison is at least this strong.</summary>
        public int NeedsGarrison;
    }

    public sealed class EventDef
    {
        public string Id;
        public string Title;
        public string Body;
        /// <summary>Who is speaking, for the card header.</summary>
        public string Source;

        public int MinTurn = 11;
        public int MaxTurn = 60;
        public int MinThreat;
        public float MinGrievance;
        public bool NeedsFoodShortage;
        /// <summary>Fires on exactly this turn, ignoring the random pool. −1 for the pool.</summary>
        public int ScheduledTurn = -1;
        /// <summary>Relative likelihood inside the pool.</summary>
        public float Weight = 1f;
        /// <summary>Fires at most once a run.</summary>
        public bool Once;

        public EventOption[] Options;
    }

    public static class Events
    {
        /// <summary>Turns 1–10 carry no crises. The player is learning the systems.</summary>
        public const int FirstCrisisTurn = 11;

        /// <summary>The mid-game squeeze. Telegraphed five turns ahead; see ThreatManager.</summary>
        public const int KadraTurn = 30;

        public static readonly EventDef[] All =
        {
            // ================================================ MERSA
            new EventDef
            {
                Id = "harac", Title = "MERSA'DAN HARAÇ TALEBİ", Source = "Mersa elçisi",
                Body = "Elçi üç gündür misafirhanede oturuyor ve gitmiyor. \"Dostluğun bir " +
                       "bedeli var,\" diyor, \"ve bu yıl bedel biraz arttı.\"",
                MinTurn = 12, MinThreat = 25, Weight = 1.4f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Öde", Blurb = "Haraç ödenir. Mersa bir süre daha sessiz kalır.",
                        CostMoney = 320, Threat = -18, Economy = 2, Ordu = -3,
                        Effect = EventEffect.Tribute,
                    },
                    new EventOption
                    {
                        Label = "Reddet", Blurb = "Elçi eli boş döner.",
                        Threat = 14, Ordu = 4, Gelenek = 3, CostLegitimacy = 4,
                    },
                    new EventOption
                    {
                        Label = "Garnizonu göster", Blurb = "Elçi şehirden geçirilir, tabur " +
                            "sıraya dizilir. Mersa hesabını yeniden yapar.",
                        Threat = -10, Order = 4, Ordu = 5, Aydin = -3, NeedsGarrison = 40,
                    },
                },
            },

            new EventDef
            {
                Id = "sinir_baskini", Title = "SINIR BASKINI", Source = "Güvenlik bakanlığı",
                Body = "Kuzey yolunda bir kervan basıldı. Kimin yaptığı belli, kimsenin " +
                       "söyleyemediği de belli.",
                MinTurn = 14, MinThreat = 40, Weight = 1.2f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Karşılık ver", Blurb = "Garnizon sınıra çıkar.",
                        Effect = EventEffect.Raid, Magnitude = 30,
                        Threat = -8, Order = 3, Ordu = 4,
                    },
                    new EventOption
                    {
                        Label = "Yolu kapat", Blurb = "Ticaret durur, kayıp da durur.",
                        CostMoney = 120, Economy = -4, Tuccar = -6, Threat = -4,
                    },
                    new EventOption
                    {
                        Label = "Görmezden gel", Blurb = "Kervan sahibi zararını kendi karşılar.",
                        Threat = 8, Tuccar = -5, Grievance = 4, CostLegitimacy = 3,
                    },
                },
            },

            // ================================================ REFUGEES
            new EventDef
            {
                Id = "multeci_kolu", Title = "KAPIDA MÜLTECİ KOLU", Source = "Kapı zabiti",
                Body = "Sabaha karşı geldiler. Çoğu yürüyerek. Zabit kaç kişi olduklarını " +
                       "sayamamış, \"çok\" demiş, \"ve hepsi aç.\"",
                MinTurn = 13, Weight = 1.3f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Kabul et", Blurb = "Kapılar açılır. Nüfus ve işgücü artar, " +
                            "tampondan yenir.",
                        Effect = EventEffect.Refugees, Magnitude = 90,
                        Aydin = 5, Gelenek = 3, Isci = 2, Grievance = 5,
                    },
                    new EventOption
                    {
                        Label = "Barındır ve besle", Blurb = "Çadır kurulur, tayın bağlanır. " +
                            "Pahalı, ama huzursuzluk doğurmaz.",
                        CostMoney = 280, Effect = EventEffect.Refugees, Magnitude = 90,
                        Aydin = 7, Gelenek = 5, Isci = 3, Economy = -3,
                    },
                    new EventOption
                    {
                        Label = "Geri çevir", Blurb = "Kol yoluna devam eder.",
                        Aydin = -9, Gelenek = -7, Order = 5, Ordu = 2,
                    },
                },
            },

            // ================================================ FOOD
            new EventDef
            {
                Id = "kuraklik", Title = "KURAKLIK", Source = "Ziraat komisyonu",
                Body = "Nehir çekildi. Delta'nın yarısı bu yıl beklediğini vermeyecek.",
                MinTurn = 12, Weight = 1.1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Dışarıdan al", Blurb = "Tahıl satın alınır.",
                        CostMoney = 300, Effect = EventEffect.Grain, Magnitude = 140, Economy = 3,
                    },
                    new EventOption
                    {
                        Label = "Ambarlara el koy", Blurb = "Özel ambarlar sayılır ve boşaltılır.",
                        Effect = EventEffect.Requisition, Magnitude = 170,
                        Order = 7, Tuccar = -8, Gelenek = -4, Grievance = 7,
                    },
                    new EventOption
                    {
                        Label = "Kemer sık", Blurb = "Hiçbir şey yapılmaz. Zincir kendi başına " +
                            "toparlamaya çalışır.",
                        Effect = EventEffect.Grain, Magnitude = -110, Grievance = 9, CostLegitimacy = 5,
                    },
                },
            },

            new EventDef
            {
                Id = "bereketli_hasat", Title = "BEREKETLİ HASAT", Source = "Ziraat komisyonu",
                Body = "Delta bu yıl cömert davrandı. Ambarlar zor yetişiyor.",
                MinTurn = 11, Weight = 0.8f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Ambara koy", Blurb = "Fazlası saklanır.",
                        Effect = EventEffect.Grain, Magnitude = 160, Grievance = -3,
                    },
                    new EventOption
                    {
                        Label = "Sat", Blurb = "Fazlası satılır, hazineye girer.",
                        Effect = EventEffect.Grain, Magnitude = 60,
                        Economy = 4, Tuccar = 5, Isci = -2,
                    },
                },
            },

            // ================================================ UNREST
            new EventDef
            {
                Id = "liman_grevi", Title = "RIHTIMDA İŞ BIRAKMA", Source = "Liman vekili",
                Body = "Rıhtımda kimse çalışmıyor. Vinçler duruyor, gemiler bekliyor, " +
                       "ve kimse acele etmiyor.",
                MinTurn = 12, MinGrievance = 45, Weight = 1.3f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Ücretleri artır", Blurb = "Talep karşılanır.",
                        CostMoney = 260, Economy = -5, Isci = 8, Tuccar = -4, Grievance = -12,
                    },
                    new EventOption
                    {
                        Label = "Rıhtımı açtır", Blurb = "Rıhtım boşaltılır ve iş yeniden başlar.",
                        Order = 8, Isci = -10, Aydin = -5, Ordu = 3, Grievance = 6,
                    },
                    new EventOption
                    {
                        Label = "Bekle", Blurb = "Bir şey yapılmaz.",
                        Economy = -2, Tuccar = -5, Grievance = 3, CostLegitimacy = 4,
                    },
                },
            },

            new EventDef
            {
                Id = "ogrenci_yuruyusu", Title = "ÜNİVERSİTE'DE YÜRÜYÜŞ", Source = "Halk bakanlığı",
                Body = "Öğrenciler meydana yürüdü. Talepleri uzun, pankartları kısa.",
                MinTurn = 12, MinGrievance = 35, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Heyeti dinle", Blurb = "Bir heyet kabul edilir.",
                        CostLegitimacy = 3, Order = -4, Aydin = 7, Gelenek = -3, Grievance = -6,
                    },
                    new EventOption
                    {
                        Label = "Meydanı boşalt", Blurb = "Meydan akşama kadar boşaltılır.",
                        Order = 7, Aydin = -10, Ordu = 3, Grievance = 5,
                    },
                },
            },

            // ================================================ ABSURD ON THE SURFACE
            new EventDef
            {
                Id = "sapkalar", Title = "DÖRT BİN ŞAPKA", Source = "Gümrük memuru",
                Body = "Limana kimsenin sipariş etmediği dört bin şapka geldi. Faturası " +
                       "şehre kesilmiş. Gönderen adres yok.",
                MinTurn = 11, Weight = 0.7f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Faturayı öde", Blurb = "Ödenir. Şapkalar depoda durur.",
                        CostMoney = 180, Tuccar = 3,
                    },
                    new EventOption
                    {
                        Label = "Halka dağıt", Blurb = "Şapkalar dağıtılır. Kimse anlamaz, " +
                            "herkes memnun olur.",
                        CostMoney = 60, Grievance = -6, Gelenek = 4, Aydin = -2,
                    },
                    new EventOption
                    {
                        Label = "Müzayedeye çıkar", Blurb = "Satılır.",
                        Economy = 3, Tuccar = 4, Effect = EventEffect.None, Magnitude = 120,
                    },
                },
            },

            new EventDef
            {
                Id = "boga", Title = "ÇARŞIDA BOĞA", Source = "Eski Şehir vekili",
                Body = "Ödüllü bir boğa çarşıya girdi ve iki tezgâh devirdi. Sahibi boğanın " +
                       "kusuru olmadığında ısrarlı.",
                MinTurn = 11, Weight = 0.6f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Zararı hazine karşılasın", Blurb = "Tezgâhlar tazmin edilir.",
                        CostMoney = 90, Grievance = -4, Tuccar = 3,
                    },
                    new EventOption
                    {
                        Label = "Sahibine ödet", Blurb = "Fatura boğanın sahibine çıkarılır.",
                        Gelenek = -4, Tuccar = -2, Order = 2,
                    },
                },
            },

            // ================================================ THE MID-GAME SQUEEZE
            new EventDef
            {
                Id = "kadra", Title = "KADRA DÜŞTÜ", Source = "Telgrafhane",
                Body = "Kadra bu sabah teslim oldu. Üç şey aynı anda oldu: kapımızda büyük " +
                       "bir mülteci kolu var, Mersa bir komşusundan kurtuldu, ve alacaklılar " +
                       "riski yeniden fiyatladı.",
                ScheduledTurn = KadraTurn, Once = true,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Kapıları aç", Blurb = "Kol içeri alınır. Şehir büyür, tampon erir.",
                        Effect = EventEffect.KadraCollapse, Magnitude = 180,
                        Aydin = 8, Gelenek = 5, Isci = 3, Grievance = 9, Threat = 18,
                    },
                    new EventOption
                    {
                        Label = "Kampa al ve besle", Blurb = "Surların dışında kamp kurulur. " +
                            "Pahalı ve yavaş, ama şehir sarsılmaz.",
                        CostMoney = 620, Effect = EventEffect.KadraCollapse, Magnitude = 180,
                        Aydin = 10, Gelenek = 7, Economy = -4, Grievance = 3, Threat = 18,
                    },
                    new EventOption
                    {
                        Label = "Kapıları kapat", Blurb = "Kol geri çevrilir.",
                        Effect = EventEffect.KadraCollapse, Magnitude = 0,
                        Order = 9, Ordu = 4, Aydin = -14, Gelenek = -10, Threat = 18,
                    },
                },
            },

            // ================================================ THE ARMY
            new EventDef
            {
                Id = "asker_maasi", Title = "GARNİZON MAAŞ İSTİYOR", Source = "Albay",
                Body = "Garnizon üç aydır maaş almamış. Albay bunu son derece nazik " +
                       "bir dille hatırlatıyor.",
                MinTurn = 14, Weight = 1.1f, MinThreat = 20,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Öde", Blurb = "Maaşlar ödenir.",
                        CostMoney = 300, Ordu = 9,
                    },
                    new EventOption
                    {
                        Label = "Yarısını öde", Blurb = "Yarısı ödenir, gerisi söz verilir.",
                        CostMoney = 150, Ordu = 2, CostLegitimacy = 2,
                    },
                    new EventOption
                    {
                        Label = "Erteleyin", Blurb = "Bir şey ödenmez.",
                        Ordu = -12, Order = -2,
                    },
                },
            },

            new EventDef
            {
                Id = "askere_alma", Title = "TABUR EKSİK", Source = "Güvenlik bakanlığı",
                Body = "Garnizon kâğıt üstünde var, sahada yok. Bakanlık askere alma " +
                       "kararnamesi bekliyor.",
                MinTurn = 13, MinThreat = 35, Weight = 1.2f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Askere al", Blurb = "Hane başına bir asker. Garnizon büyür, " +
                            "tarlada eksilen eller başka bir yerde çıkar.",
                        Effect = EventEffect.Conscript, Magnitude = 45,
                        Garrison = 30, Order = 6, Ordu = 7, Isci = -5,
                    },
                    new EventOption
                    {
                        Label = "Paralı asker tut", Blurb = "Dışarıdan asker tutulur. " +
                            "İşgücüne dokunulmaz.",
                        CostMoney = 420, Garrison = 26, Economy = 4, Ordu = 3,
                    },
                    new EventOption
                    {
                        Label = "Şimdilik gerek yok", Blurb = "Bir şey yapılmaz.",
                        Threat = 6, Ordu = -5,
                    },
                },
            },
        };


        public static EventDef Get(string id)
        {
            foreach (var e in All)
                if (e.Id == id) return e;
            return null;
        }
    }
}

