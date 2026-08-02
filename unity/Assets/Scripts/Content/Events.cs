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
        Plague,          // takes people out of the districts and leaves fear behind
        Material,        // adds to or drains the construction depot
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

            // ================================================ MERSA, CONTINUED
            new EventDef
            {
                Id = "abluka", Title = "LİMAN ABLUKA ALTINDA", Source = "Liman reisi",
                Body = "Mersa'nın gemileri açıkta duruyor. Ateş etmiyorlar, sadece " +
                       "duruyorlar. Bizim gemiler de öyle.",
                MinTurn = 18, MinThreat = 45, Weight = 1.2f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Kara yolundan getirt", Blurb = "Mallar dolambaçlı yoldan gelir. " +
                            "Pahalı ve yavaş, ama liman kapanmaz.",
                        CostMoney = 380, Economy = -3, Tuccar = 4,
                        Effect = EventEffect.Material, Magnitude = 60,
                    },
                    new EventOption
                    {
                        Label = "Filoyu çıkar", Blurb = "Garnizonun gemileri açığa çıkar.",
                        Effect = EventEffect.Raid, Magnitude = 45,
                        Order = 5, Ordu = 6, Threat = -6,
                    },
                    new EventOption
                    {
                        Label = "Bekle", Blurb = "Abluka kendiliğinden kalkana kadar beklenir.",
                        Economy = -5, Tuccar = -9, Grievance = 6, CostLegitimacy = 4,
                    },
                },
            },

            new EventDef
            {
                Id = "casus", Title = "TELGRAFHANEDE BİR ADAM", Source = "Güvenlik bakanlığı",
                Body = "Telgrafhanede çalışan bir memurun, gönderdiği her telgrafın bir " +
                       "kopyasını sakladığı anlaşıldı. Kime sakladığı anlaşılmadı.",
                MinTurn = 16, MinThreat = 25, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Mahkemeye ver", Blurb = "Açık duruşma yapılır. Yavaş, ve " +
                            "herkes ne olduğunu öğrenir.",
                        CostMoney = 90, Order = -3, Aydin = 6, Threat = 4,
                    },
                    new EventOption
                    {
                        Label = "Sessizce kaybettir", Blurb = "Memur bir daha görünmez. " +
                            "Kimse sormaz, herkes bilir.",
                        Order = 8, Ordu = 4, Aydin = -9, Threat = -6,
                    },
                    new EventOption
                    {
                        Label = "Çevir", Blurb = "Memura yanlış telgraflar yazdırılır.",
                        CostMoney = 140, Order = 5, Threat = -12, Aydin = -3,
                    },
                },
            },

            new EventDef
            {
                Id = "silah_taciri", Title = "BİR TÜCCARIN TEKLİFİ", Source = "Adı verilmeyen tacir",
                Body = "Adam kendini tanıtmadı ama fiyat listesini bıraktı. Listede " +
                       "şehirde olmaması gereken şeyler var.",
                MinTurn = 20, MinThreat = 35, Weight = 0.9f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Satın al", Blurb = "Garnizon donanır. Kimse nereden geldiğini sormaz.",
                        CostMoney = 460, Garrison = 22, Ordu = 7, Order = 4, Aydin = -4,
                    },
                    new EventOption
                    {
                        Label = "Tacirı tutukla", Blurb = "Mal el konur, adam içeri alınır.",
                        Effect = EventEffect.Material, Magnitude = 70,
                        Order = 5, Tuccar = -6, Threat = 5,
                    },
                    new EventOption
                    {
                        Label = "Listeyi geri gönder", Blurb = "Teklif reddedilir.",
                        Aydin = 4, Gelenek = 3, Ordu = -4,
                    },
                },
            },

            new EventDef
            {
                Id = "mersa_ic_karisiklik", Title = "MERSA'DA KARIŞIKLIK", Source = "Telgrafhane",
                Body = "Mersa'nın kendi taşrasında bir ayaklanma var. Bir süre bizimle " +
                       "ilgilenemeyecekler. Bir süre.",
                MinTurn = 22, MinThreat = 40, Weight = 0.8f, Once = true,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Nefes al", Blurb = "Fırsat garnizonu büyütmek yerine " +
                            "tarlalara dönmek için kullanılır.",
                        Threat = -20, Isci = 5, Gelenek = 4,
                        Effect = EventEffect.Grain, Magnitude = 90,
                    },
                    new EventOption
                    {
                        Label = "Asilere yardım gönder", Blurb = "Mersa'nın başı uzun süre " +
                            "dertte kalır. Öğrenirlerse bu bir savaş sebebidir.",
                        CostMoney = 340, Threat = -30, Order = 3, Aydin = 3,
                    },
                    new EventOption
                    {
                        Label = "Sınırı zorla", Blurb = "Karışıklıktan yararlanılır.",
                        Effect = EventEffect.Raid, Magnitude = 35,
                        Order = 7, Ordu = 8, Aydin = -6, Threat = 10,
                    },
                },
            },

            // ================================================ ARRIVALS
            new EventDef
            {
                Id = "usta_gocmenler", Title = "USTALAR GELDİ", Source = "Kapı zabiti",
                Body = "Kadra'dan gelen kafilede otuz kadar usta var. Dokumacı, demirci, " +
                       "değirmenci. Hepsi kalmak istiyor, hiçbiri boş oturmak istemiyor.",
                MinTurn = 15, Weight = 0.9f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Atölyelere yerleştir", Blurb = "Ustalar işe koyulur.",
                        Effect = EventEffect.Refugees, Magnitude = 40,
                        Isci = 6, Tuccar = 4, Aydin = 3,
                    },
                    new EventOption
                    {
                        Label = "Loncalara sor", Blurb = "Şehrin kendi ustaları rakip istemiyor. " +
                            "Kafile başka kapıya gider.",
                        Tuccar = 5, Gelenek = 4, Isci = -6, Aydin = -5,
                    },
                },
            },

            new EventDef
            {
                Id = "gocmen_karsiti", Title = "MAHALLEDEN DİLEKÇE", Source = "Eski Şehir vekili",
                Body = "Dört yüz imzalı bir dilekçe geldi. \"Gelenler bizden çok oldu,\" " +
                       "diyor. Sayı doğru değil ama imzalar gerçek.",
                MinTurn = 20, MinGrievance = 35, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Dilekçeyi cevapla", Blurb = "Vali mahalleye iner ve konuşur. " +
                            "Kimse ikna olmaz, herkes dinlendiğini bilir.",
                        CostLegitimacy = 2, Grievance = -7, Aydin = 4, Gelenek = 3,
                    },
                    new EventOption
                    {
                        Label = "Yerleşimi sınırla", Blurb = "Yeni gelenlere mahalle tahsisi durur.",
                        Order = 6, Gelenek = 7, Aydin = -8, Isci = -4, Grievance = -4,
                    },
                    new EventOption
                    {
                        Label = "Cevap verme", Blurb = "Dilekçe dosyalanır.",
                        Grievance = 5, Gelenek = -5, CostLegitimacy = 3,
                    },
                },
            },

            // ================================================ FOOD, CONTINUED
            new EventDef
            {
                Id = "ambar_yangini", Title = "AMBAR YANDI", Source = "İtfaiye zabiti",
                Body = "Gece yarısı çıktı. Sabaha kadar söndürülemedi. Nasıl çıktığı " +
                       "konusunda üç ayrı hikâye anlatılıyor.",
                MinTurn = 14, Weight = 1.1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Açığı satın al", Blurb = "Kayıp dışarıdan kapatılır.",
                        CostMoney = 340, Effect = EventEffect.Grain, Magnitude = -40, Economy = 3,
                    },
                    new EventOption
                    {
                        Label = "Soruşturma aç", Blurb = "Kimin ihmali olduğu aranır. " +
                            "Bulunur ya da bulunmaz, ambar dolmaz.",
                        Effect = EventEffect.Grain, Magnitude = -160,
                        Aydin = 5, Grievance = 4,
                    },
                    new EventOption
                    {
                        Label = "Ambarcıyı astır", Blurb = "Bir sorumlu bulunur ve gösterilir.",
                        Effect = EventEffect.Grain, Magnitude = -160,
                        Order = 9, Ordu = 3, Aydin = -11, Gelenek = -5, Grievance = -3,
                    },
                },
            },

            new EventDef
            {
                Id = "bugday_biti", Title = "AMBARDA BİT", Source = "Ziraat komisyonu",
                Body = "Ambarın alt katındaki çuvallar kımıldıyor. Komisyon bunu " +
                       "\"kısmi bir mesele\" diye tarif ediyor.",
                MinTurn = 13, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Hepsini yak", Blurb = "Şüpheli olan her şey yakılır. " +
                            "Bulaşma durur, ambar boşalır.",
                        Effect = EventEffect.Grain, Magnitude = -130, Grievance = 4,
                    },
                    new EventOption
                    {
                        Label = "Ayıklat", Blurb = "İşçi tutulur, çuvallar tek tek elenir.",
                        CostMoney = 200, Effect = EventEffect.Grain, Magnitude = -40, Isci = 3,
                    },
                    new EventOption
                    {
                        Label = "Karıştırıp dağıt", Blurb = "İyisi kötüsüne karıştırılıp " +
                            "fırınlara gönderilir. Kimse fark etmez, herkes yer.",
                        Grievance = 8, Aydin = -5, CostLegitimacy = 4,
                    },
                },
            },

            new EventDef
            {
                Id = "karaborsa_ekmek", Title = "EKMEK KARABORSAYA DÜŞTÜ", Source = "Halk bakanlığı",
                Body = "Fırınların önünde sıra var, arka kapılarında yok. Fiyat " +
                       "arka kapıda üç katı.",
                MinTurn = 16, NeedsFoodShortage = true, Weight = 1.4f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Fiyatı sabitle", Blurb = "Narh konur. Karaborsa küçülür, " +
                            "fırıncılar küser.",
                        Economy = -7, Tuccar = -8, Isci = 6, Grievance = -8,
                    },
                    new EventOption
                    {
                        Label = "Fırınları bastır", Blurb = "Zabıta arka kapıları mühürler.",
                        Order = 8, Tuccar = -6, Grievance = -5, Ordu = 2,
                    },
                    new EventOption
                    {
                        Label = "Piyasa dengelesin", Blurb = "Karışılmaz.",
                        Economy = 5, Tuccar = 6, Isci = -9, Grievance = 9, CostLegitimacy = 4,
                    },
                },
            },

            new EventDef
            {
                Id = "balik_surusu", Title = "KÖRFEZDE BALIK", Source = "Liman reisi",
                Body = "Kimsenin hatırlamadığı kadar büyük bir sürü girdi körfeze. " +
                       "Rıhtımda herkes ağ arıyor.",
                MinTurn = 11, Weight = 0.7f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Herkes tutsun", Blurb = "Kapılar açılır, ne tutulursa halkın.",
                        Effect = EventEffect.Grain, Magnitude = 120,
                        Isci = 6, Grievance = -7, Economy = -2,
                    },
                    new EventOption
                    {
                        Label = "İhale et", Blurb = "Avlanma hakkı satılır.",
                        Effect = EventEffect.Grain, Magnitude = 50,
                        Economy = 4, Tuccar = 7, Isci = -5,
                    },
                },
            },

            // ================================================ HEALTH
            new EventDef
            {
                Id = "salgin", Title = "SANAYİ'DE HUMMA", Source = "Şehir hekimi",
                Body = "Önce üç hane, sonra bir sokak. Hekim adını koymaktan çekiniyor " +
                       "ama ne olduğunu biliyor.",
                MinTurn = 15, Weight = 1.2f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Mahalleyi kapat", Blurb = "Sokaklar kordona alınır. " +
                            "Yayılma durur, mahalle bunu unutmaz.",
                        Effect = EventEffect.Plague, Magnitude = 30,
                        Order = 8, Isci = -7, Grievance = 9, Ordu = 3,
                    },
                    new EventOption
                    {
                        Label = "Hekim ve ilaç gönder", Blurb = "Pahalı, yavaş, ve işe yarar.",
                        CostMoney = 420, Effect = EventEffect.Plague, Magnitude = 25,
                        Aydin = 7, Isci = 5, Gelenek = 4, Grievance = -4,
                    },
                    new EventOption
                    {
                        Label = "Kendi geçer", Blurb = "Bir şey yapılmaz.",
                        Effect = EventEffect.Plague, Magnitude = 110,
                        CostLegitimacy = 8, Grievance = 12,
                    },
                },
            },

            new EventDef
            {
                Id = "su_kirlendi", Title = "KUYULARDA TAT", Source = "Şehir hekimi",
                Body = "Üç kuyunun suyu tuhaf. Hekim içmeyin diyor, mahalle başka " +
                       "kuyu olmadığını söylüyor.",
                MinTurn = 13, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Kuyuları kapat, su taşıt", Blurb = "Su arabayla taşınır.",
                        CostMoney = 260, Grievance = -3, Isci = 4, Aydin = 4,
                    },
                    new EventOption
                    {
                        Label = "Kaynatın deyip geç", Blurb = "Duyuru yapılır, kuyular açık kalır.",
                        Effect = EventEffect.Plague, Magnitude = 45,
                        Grievance = 7, CostLegitimacy = 3,
                    },
                },
            },

            // ================================================ MATERIALS
            new EventDef
            {
                Id = "ocak_grevi", Title = "OCAKTA İŞ BIRAKMA", Source = "Sanayi vekili",
                Body = "Ocakçılar aşağı inmiyor. Sebep olarak üç ölüm sayıyorlar, " +
                       "ocak sahibi bunları kaza sayıyor.",
                MinTurn = 15, MinGrievance = 30, Weight = 1.2f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Ocağı güvenceye al", Blurb = "Tahkimat yapılır, ölümler durur.",
                        CostMoney = 310, Isci = 9, Tuccar = -5, Economy = -4, Grievance = -8,
                    },
                    new EventOption
                    {
                        Label = "Ocağı açtır", Blurb = "Ocak zorla çalıştırılır.",
                        Effect = EventEffect.Material, Magnitude = 60,
                        Order = 8, Isci = -11, Aydin = -4, Grievance = 7,
                    },
                    new EventOption
                    {
                        Label = "Beklet", Blurb = "Bir şey yapılmaz. Depo kendi başına erir.",
                        Effect = EventEffect.Material, Magnitude = -80,
                        Tuccar = -4, CostLegitimacy = 3,
                    },
                },
            },

            new EventDef
            {
                Id = "ocak_cokmesi", Title = "OCAK GÖÇTÜ", Source = "İmar bakanlığı",
                Body = "Ana galeri çöktü. Aşağıda kimse kalmadı — bunu söyleyen de " +
                       "aşağı inmedi.",
                MinTurn = 18, Weight = 0.9f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Galeriyi yeniden aç", Blurb = "Aylar sürer, para gider, ocak döner.",
                        CostMoney = 450, Effect = EventEffect.Material, Magnitude = -60, Isci = 5,
                    },
                    new EventOption
                    {
                        Label = "Yan galeriden devam", Blurb = "Daha dar, daha tehlikeli, " +
                            "daha hızlı.",
                        Effect = EventEffect.Material, Magnitude = 40,
                        Order = 4, Isci = -8, Economy = 3, Grievance = 5,
                    },
                },
            },

            new EventDef
            {
                Id = "yeni_damar", Title = "YENİ DAMAR", Source = "İmar bakanlığı",
                Body = "Tepenin arkasında beklenmedik bir damar bulundu. Bulan çoban " +
                       "ödül bekliyor.",
                MinTurn = 12, Weight = 0.7f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Şehir işletsin", Blurb = "Damar şehrin olur.",
                        Effect = EventEffect.Material, Magnitude = 130,
                        Economy = -3, Isci = 5, Tuccar = -3,
                    },
                    new EventOption
                    {
                        Label = "İşletmeyi sat", Blurb = "Hak bir şirkete devredilir.",
                        Effect = EventEffect.Material, Magnitude = 60,
                        CostMoney = -280, Economy = 5, Tuccar = 8, Isci = -4,
                    },
                },
            },

            new EventDef
            {
                Id = "isli_bacalar", Title = "BACALARDAN ŞİKÂYET", Source = "Tepe vekili",
                Body = "Tepe'nin çamaşırları is içinde. Vekil bunu bir yaşam kalitesi " +
                       "meselesi olarak sunuyor; Sanayi'de kimse çamaşır asmıyor zaten.",
                MinTurn = 16, Weight = 0.9f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Filtre zorunlu kıl", Blurb = "Bacalara filtre takılır.",
                        CostMoney = 280, Economy = -4, Tuccar = -6, Aydin = 6, Grievance = -5,
                    },
                    new EventOption
                    {
                        Label = "Vardiyayı geceye al", Blurb = "Duman geceleri çıkar. " +
                            "Kimse görmez, herkes solur.",
                        Isci = -5, Tuccar = 3, Grievance = 3,
                    },
                    new EventOption
                    {
                        Label = "Şikâyeti reddet", Blurb = "Sanayi çalışmaya devam eder.",
                        Economy = 4, Tuccar = 5, Aydin = -5, Grievance = 4,
                    },
                },
            },

            // ================================================ MONEY
            new EventDef
            {
                Id = "alacakli_ultimatomu", Title = "ALACAKLIDAN ÜLTİMATOM", Source = "Banka temsilcisi",
                Body = "Temsilci gülümseyerek oturuyor. \"Anlaşmamızın şartlarını " +
                       "hatırlatmak istedim,\" diyor. Hatırlatmıyor, bildiriyor.",
                MinTurn = 20, Weight = 1.1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Borcu kapat", Blurb = "Hazineden ödenir. Alacaklı çekilir.",
                        CostMoney = 560, Tuccar = 6, Economy = 3,
                    },
                    new EventOption
                    {
                        Label = "Yeni şartları kabul et", Blurb = "Bir yasa yuvası daha " +
                            "alacaklının eline geçer.",
                        Economy = 6, Tuccar = 4, Isci = -6, Aydin = -4, CostLegitimacy = 5,
                    },
                    new EventOption
                    {
                        Label = "Ödemeyi durdur", Blurb = "Şehir borcunu tanımaz.",
                        Economy = -8, Tuccar = -14, Isci = 6, Threat = 8, CostLegitimacy = 6,
                    },
                },
            },

            new EventDef
            {
                Id = "vergi_kacagi", Title = "DEFTERLER TUTMUYOR", Source = "Maliye bakanlığı",
                Body = "Tepe'nin en büyük altı hanesi geçen yıl toplam olarak bir " +
                       "değirmenci kadar vergi ödemiş.",
                MinTurn = 17, Weight = 1.1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Denetim başlat", Blurb = "Defterler tek tek açılır. " +
                            "Uzun sürer, sonunda para gelir.",
                        CostMoney = 120, Economy = -3, Tuccar = -8, Isci = 6, Aydin = 5,
                    },
                    new EventOption
                    {
                        Label = "Haneleri mühürle", Blurb = "Ödemeyenin kapısına mühür vurulur.",
                        CostMoney = -420, Order = 7, Tuccar = -12, Isci = 8, Grievance = -4,
                    },
                    new EventOption
                    {
                        Label = "Uzlaş", Blurb = "Bir rakamda anlaşılır ve konu kapanır.",
                        CostMoney = -160, Tuccar = 5, Isci = -7, Aydin = -5, CostLegitimacy = 3,
                    },
                },
            },

            new EventDef
            {
                Id = "sahte_sikke", Title = "ÇARŞIDA SAHTE SİKKE", Source = "Maliye bakanlığı",
                Body = "Çarşıda dolaşan her yirmi sikkeden biri sahte. Kalıbın nerede " +
                       "olduğu bilinmiyor, kalıbın iyi olduğu biliniyor.",
                MinTurn = 15, Weight = 0.9f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Parayı yenile", Blurb = "Bütün sikkeler toplanıp yeniden basılır.",
                        CostMoney = 380, Economy = 3, Tuccar = 5, Grievance = 3,
                    },
                    new EventOption
                    {
                        Label = "Kalıbı aramaya çık", Blurb = "Çarşı didik didik aranır.",
                        Order = 6, Tuccar = -5, Grievance = 5, Ordu = 2,
                    },
                    new EventOption
                    {
                        Label = "Görmezden gel", Blurb = "Sahte sikke de sikkedir.",
                        Economy = -4, Tuccar = -7, CostLegitimacy = 3,
                    },
                },
            },

            // ================================================ POLITICS
            new EventDef
            {
                Id = "ayaklanma", Title = "MAHALLE SOKAĞA ÇIKTI", Source = "Halk bakanlığı",
                Body = "Bir mahalle bu sabah işe gitmedi. Sokaklarda duruyorlar, " +
                       "bir yere yürümüyorlar. Bu daha kötü.",
                MinTurn = 14, MinGrievance = 55, Weight = 1.5f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Taleplerini karşıla", Blurb = "Ne istiyorlarsa verilir. Pahalı.",
                        CostMoney = 400, Grievance = -16, Isci = 7, Economy = -4, Tuccar = -4,
                    },
                    new EventOption
                    {
                        Label = "Sokağa çıkma yasağı", Blurb = "Sokaklar boşaltılır ve " +
                            "birkaç tur sessiz kalır.",
                        Order = 10, Ordu = 4, Aydin = -8, Isci = -8, Grievance = -6,
                    },
                    new EventOption
                    {
                        Label = "Elebaşlarını al", Blurb = "Birkaç isim alınır, kalabalık dağılır.",
                        Order = 12, Ordu = 5, Aydin = -12, Isci = -10, Grievance = -10,
                    },
                },
            },

            new EventDef
            {
                Id = "suikast", Title = "ARABAYA ATEŞ AÇILDI", Source = "Güvenlik bakanlığı",
                Body = "Valinin arabasına ateş açıldı. Kimse yaralanmadı. Ateş eden " +
                       "kaçtı, arkasında bir şey bırakmadı — bir şey bırakmamış olması dışında.",
                MinTurn = 24, Weight = 0.9f, Once = true,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Soruşturmayı mahkemeye bırak", Blurb = "Usulünce yürütülür.",
                        CostMoney = 150, Aydin = 7, Gelenek = 4, Order = -3,
                    },
                    new EventOption
                    {
                        Label = "Şehri tarat", Blurb = "Kapılar kapanır, evler aranır.",
                        Order = 11, Ordu = 6, Aydin = -10, Isci = -6, Grievance = 8,
                    },
                    new EventOption
                    {
                        Label = "Hiç olmamış say", Blurb = "Haber yayılmaz.",
                        Order = 4, CostLegitimacy = 2, Threat = 4,
                    },
                },
            },

            new EventDef
            {
                Id = "yolsuzluk", Title = "İHALEDE İSİM ÇIKTI", Source = "Denetim kurulu",
                Body = "Liman ihalesini alan şirketin ortaklarından biri, ihaleyi " +
                       "veren kurulun üyesiymiş. Kurul bunu bir tesadüf sayıyor.",
                MinTurn = 18, Weight = 1.1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "İhaleyi iptal et", Blurb = "İhale bozulur, iş gecikir, " +
                            "kimse dokunulmaz kalmaz.",
                        CostMoney = 220, Effect = EventEffect.Material, Magnitude = -50,
                        Aydin = 9, Isci = 6, Tuccar = -8,
                    },
                    new EventOption
                    {
                        Label = "Kurulu değiştir", Blurb = "Kurul dağıtılır, yenisi kurulur.",
                        CostLegitimacy = 3, Aydin = 4, Tuccar = -4, Order = 3,
                    },
                    new EventOption
                    {
                        Label = "Haberi bastır", Blurb = "Gazeteye çıkmaz.",
                        Order = 7, Aydin = -11, Isci = -5, CostLegitimacy = 6,
                    },
                },
            },

            new EventDef
            {
                Id = "gazete_ifsasi", Title = "GAZETE BİR ŞEY YAZDI", Source = "Matbaa",
                Body = "Muhalif gazete, bakanlıklardan birinin rakamlarını yan yana " +
                       "dizmiş. Rakamlar birbirini tutmuyor ve gazete bunu biliyor.",
                MinTurn = 20, Weight = 1.2f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Rakamları aç", Blurb = "Bütün defterler yayımlanır. " +
                            "Utanç verici ve temizleyici.",
                        CostLegitimacy = 6, Aydin = 11, Isci = 5, Order = -6,
                    },
                    new EventOption
                    {
                        Label = "Tekzip yayımla", Blurb = "Resmî bir açıklama yapılır.",
                        CostMoney = 80, CostLegitimacy = 2, Aydin = -4,
                    },
                    new EventOption
                    {
                        Label = "Matbaayı mühürle", Blurb = "Gazete kapanır.",
                        Order = 10, Aydin = -14, Gelenek = 3, Ordu = 3,
                    },
                },
            },

            new EventDef
            {
                Id = "meclis_cekilme", Title = "MECLİSTEN ÇEKİLME", Source = "Muhalefet sözcüsü",
                Body = "Muhalefet vekilleri oturumu terk etti. Salonda kalanlar " +
                       "her şeyi oybirliğiyle geçirebilir. Sorun tam olarak bu.",
                MinTurn = 22, MinGrievance = 40, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Geri çağır", Blurb = "Bir taviz verilir ve salon dolar.",
                        CostLegitimacy = 5, Order = -5, Aydin = 6, Isci = 5, Grievance = -5,
                    },
                    new EventOption
                    {
                        Label = "Oturumu kalanla sürdür", Blurb = "Meclis eksik çalışır.",
                        Order = 8, Aydin = -9, Isci = -5,
                    },
                },
            },

            new EventDef
            {
                Id = "dini_uyanis", Title = "ESKİ ŞEHİR'DE VAAZ", Source = "Eski Şehir vekili",
                Body = "Bir vaiz her akşam meydanda konuşuyor ve her akşam kalabalık " +
                       "büyüyor. Söyledikleri yasak değil, söylemediklerini herkes anlıyor.",
                MinTurn = 17, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Vaizi kabul et", Blurb = "Vali onu dinler ve bir tapınak " +
                            "sözü verir.",
                        CostMoney = 190, Gelenek = 10, Aydin = -5, Grievance = -7,
                    },
                    new EventOption
                    {
                        Label = "Meydanı kapat", Blurb = "Akşam toplanmaları yasaklanır.",
                        Order = 7, Gelenek = -11, Ordu = 2, Grievance = 6,
                    },
                    new EventOption
                    {
                        Label = "Bırak konuşsun", Blurb = "Karışılmaz.",
                        Gelenek = 5, Aydin = 2, Order = -3,
                    },
                },
            },

            // ================================================ THE ARMY, CONTINUED
            new EventDef
            {
                Id = "subay_dilekcesi", Title = "SUBAYLARDAN DİLEKÇE", Source = "Albay",
                Body = "Kırk subay imzalamış. Dilekçe son derece saygılı bir dille " +
                       "yazılmış ve son derece açık bir şey istiyor.",
                MinTurn = 25, MinThreat = 30, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "İsteklerini karşıla", Blurb = "Garnizona söz geçer.",
                        CostMoney = 360, Ordu = 10, Order = 4, Aydin = -4,
                    },
                    new EventOption
                    {
                        Label = "İmzacıları dağıt", Blurb = "Subaylar farklı taburlara verilir.",
                        Ordu = -9, Order = 5, Threat = 5,
                    },
                    new EventOption
                    {
                        Label = "Dilekçeyi meclise gönder", Blurb = "Karar meclisin olur.",
                        CostLegitimacy = 2, Order = -5, Aydin = 6, Ordu = -4,
                    },
                },
            },

            new EventDef
            {
                Id = "firar", Title = "TABURDA FİRAR", Source = "Albay",
                Body = "Bir gecede otuz kişi kayboldu. Albay bunu \"izinsiz uzaklaşma\" " +
                       "diye kaydetmiş.",
                MinTurn = 22, MinThreat = 25, Weight = 0.9f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Affet ve geri çağır", Blurb = "Dönene bir şey olmaz.",
                        Garrison = 12, Ordu = -3, Isci = 5, Gelenek = 4,
                    },
                    new EventOption
                    {
                        Label = "Yakala ve cezalandır", Blurb = "Firariler aranır, bulunanlar " +
                            "meydanda cezalandırılır.",
                        Order = 8, Ordu = 5, Isci = -7, Aydin = -6, Grievance = 6,
                    },
                    new EventOption
                    {
                        Label = "Kayıt düzelt", Blurb = "Defterde otuz kişi hâlâ var.",
                        Order = 3, CostLegitimacy = 3, Threat = 6,
                    },
                },
            },

            // ================================================ THE CABINET
            new EventDef
            {
                Id = "bakan_ozel_gorusme", Title = "BAKAN ÖZEL GÖRÜŞME İSTİYOR",
                Source = "Bakanlık kalemi",
                Body = "Bakanlardan biri, kâtipsiz ve kayıtsız görüşmek istediğini " +
                       "bildirdi. Konu hakkında hiçbir şey yazmamış.",
                MinTurn = 16, Weight = 1.1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Kabul et", Blurb = "Kapı kapanır. Ne konuşulduğu " +
                            "kayda geçmez — ama bakan bir daha size doğruyu söylemek zorunda hisseder.",
                        CostLegitimacy = 2, Order = 3, Aydin = -3,
                    },
                    new EventOption
                    {
                        Label = "Meclis önünde konuşsun", Blurb = "Görüşme açık oturumda yapılır.",
                        Order = -5, Aydin = 7, Isci = 3, CostLegitimacy = 3,
                    },
                    new EventOption
                    {
                        Label = "Reddet", Blurb = "Görüşme olmaz.",
                        Order = 2, Grievance = 2,
                    },
                },
            },

            new EventDef
            {
                Id = "bakan_istifa", Title = "BAKAN İSTİFA EDİYOR", Source = "Bakanlık kalemi",
                Body = "Bir bakan istifasını sundu. Gerekçe olarak \"sıhhi sebepler\" " +
                       "yazmış. Sıhhati yerinde.",
                MinTurn = 20, Weight = 0.9f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Kabul et", Blurb = "İstifa kabul edilir, yeri boşalır.",
                        CostLegitimacy = 3, Aydin = 3,
                    },
                    new EventOption
                    {
                        Label = "Kalmasını iste", Blurb = "Bakan kalır ve bunu hatırlatır.",
                        CostMoney = 180, Order = 3,
                    },
                    new EventOption
                    {
                        Label = "İstifayı kabul etme ve açıkla", Blurb = "Bakanın neden " +
                            "gitmek istediği ilan edilir.",
                        CostLegitimacy = 5, Aydin = 8, Order = -4,
                    },
                },
            },

            new EventDef
            {
                Id = "bakan_akrabasi", Title = "BİR İSİM TAVSİYE EDİLDİ", Source = "Bakanlık kalemi",
                Body = "Bakanlardan biri, boşalan liman müdürlüğü için bir isim " +
                       "önerdi. İsim, bakanın kayınbiraderi.",
                MinTurn = 14, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Ata", Blurb = "İsim atanır. Bakan minnettar kalır.",
                        Order = 4, Tuccar = 3, Aydin = -6, Isci = -4, CostLegitimacy = 3,
                    },
                    new EventOption
                    {
                        Label = "Sınav aç", Blurb = "Görev sınavla verilir. Yavaş ve tartışmasız.",
                        CostMoney = 90, Aydin = 7, Isci = 4, Order = -3,
                    },
                },
            },

            // ================================================ ABSURD, CONTINUED
            new EventDef
            {
                Id = "saat_kulesi", Title = "SAAT KULESİ HEDİYE EDİLDİ", Source = "Protokol",
                Body = "Komşu bir şehir dostluk nişanesi olarak bir saat kulesi " +
                       "gönderdi. Parça parça geldi ve tarifesi yok.",
                MinTurn = 11, Weight = 0.6f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Kur", Blurb = "Kule meydana dikilir. Saati hep yanlış " +
                            "gösterir ve herkes ona göre buluşmayı öğrenir.",
                        CostMoney = 210, Effect = EventEffect.Material, Magnitude = -30,
                        Gelenek = 5, Grievance = -5, Tuccar = 2,
                    },
                    new EventOption
                    {
                        Label = "Depoda kalsın", Blurb = "Sandıklar açılmaz.",
                        Effect = EventEffect.Material, Magnitude = 40, Gelenek = -3,
                    },
                },
            },

            new EventDef
            {
                Id = "filozof", Title = "MEYDANDA BİR FİLOZOF", Source = "Üniversite vekili",
                Body = "Bir adam meydana oturdu ve yönetim biçimleri üzerine " +
                       "konuşmaya başladı. Üç gündür kalkmıyor. Öğrenciler not tutuyor.",
                MinTurn = 13, Weight = 0.6f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Kürsü ver", Blurb = "Üniversite'de ders vermesi sağlanır.",
                        CostMoney = 70, Aydin = 8, Gelenek = -4, Order = -3,
                    },
                    new EventOption
                    {
                        Label = "Kaldır", Blurb = "Meydan temizlenir.",
                        Order = 5, Aydin = -7, Gelenek = 3,
                    },
                    new EventOption
                    {
                        Label = "Otursun", Blurb = "Karışılmaz.",
                        Aydin = 3, Order = -2,
                    },
                },
            },

            new EventDef
            {
                Id = "fil", Title = "LİMANA BİR FİL İNDİ", Source = "Gümrük memuru",
                Body = "Gemiden bir fil indi. Evrakında alıcı olarak şehir yazıyor. " +
                       "Fil şu an rıhtımda duruyor ve hiçbir yere gitmiyor.",
                MinTurn = 12, Weight = 0.5f, Once = true,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Şehrin olsun", Blurb = "Fil beslenir. Çocuklar bakmaya gelir, " +
                            "yem masrafı her tur hazineden çıkar.",
                        CostMoney = 160, Grievance = -8, Gelenek = 5, Aydin = 3,
                    },
                    new EventOption
                    {
                        Label = "Geri gönder", Blurb = "Fil bir sonraki gemiye bindirilir.",
                        CostMoney = 90, Tuccar = 2,
                    },
                    new EventOption
                    {
                        Label = "Sat", Blurb = "Fil satılır.",
                        Effect = EventEffect.None, Magnitude = 240,
                        Tuccar = 4, Gelenek = -4, Grievance = 4,
                    },
                },
            },

            // ================================================ WEATHER AND ACCIDENT
            new EventDef
            {
                Id = "sel", Title = "NEHİR TAŞTI", Source = "İmar bakanlığı",
                Body = "Nehir alçak mahalleleri bastı. Su çekilecek; çekildiğinde " +
                       "orada ne kalacağı ayrı mesele.",
                MinTurn = 13, Weight = 1.1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Set yaptır", Blurb = "Kalıcı set çekilir. Pahalı, ve " +
                            "nehir bir daha aynı yerden taşmaz.",
                        CostMoney = 470, Effect = EventEffect.Material, Magnitude = -70,
                        Isci = 6, Aydin = 4, Grievance = -6,
                    },
                    new EventOption
                    {
                        Label = "Zararı karşıla", Blurb = "Evler onarılır, tarla gider.",
                        CostMoney = 210, Effect = EventEffect.Grain, Magnitude = -90,
                        Grievance = -3, Gelenek = 3,
                    },
                    new EventOption
                    {
                        Label = "Su çekilsin", Blurb = "Beklenir.",
                        Effect = EventEffect.Grain, Magnitude = -140,
                        Grievance = 10, Isci = -5, CostLegitimacy = 4,
                    },
                },
            },

            new EventDef
            {
                Id = "sert_kis", Title = "KIŞ ERKEN BASTIRDI", Source = "Ziraat komisyonu",
                Body = "Kar, hasat bitmeden geldi. Tarlada kalan tarlada kaldı.",
                MinTurn = 14, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Yakacak dağıt", Blurb = "Odun ve kömür hanelere dağıtılır.",
                        CostMoney = 290, Grievance = -9, Isci = 5, Gelenek = 4,
                    },
                    new EventOption
                    {
                        Label = "Tayını azalt", Blurb = "Herkese daha az verilir, " +
                            "ambar bahara yeter.",
                        Effect = EventEffect.Grain, Magnitude = 60,
                        Grievance = 8, Isci = -5, CostLegitimacy = 3,
                    },
                    new EventOption
                    {
                        Label = "Bir şey yapma", Blurb = "Kış kendi hesabını görür.",
                        Effect = EventEffect.Plague, Magnitude = 55,
                        Grievance = 11, CostLegitimacy = 5,
                    },
                },
            },

            new EventDef
            {
                Id = "liman_kazasi", Title = "RIHTIMDA VİNÇ DEVRİLDİ", Source = "Liman reisi",
                Body = "Vinç yükle beraber devrildi. Altında kim olduğu sabah " +
                       "sayımında anlaşıldı.",
                MinTurn = 15, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Tazminat öde ve donanımı yenile", Blurb = "Ailelere ödenir, " +
                            "vinçler değiştirilir.",
                        CostMoney = 350, Isci = 9, Tuccar = -4, Economy = -3, Grievance = -6,
                    },
                    new EventOption
                    {
                        Label = "Kazayı kayda geç", Blurb = "Tutanak tutulur, iş devam eder.",
                        Isci = -7, Grievance = 6, Aydin = -3, CostLegitimacy = 3,
                    },
                },
            },

            new EventDef
            {
                Id = "kacakcilik", Title = "GÜMRÜKTE BOŞ SANDIKLAR", Source = "Gümrük memuru",
                Body = "Kayıtlarda dolu görünen kırk sandık boş çıktı. Memur " +
                       "sandıkların yolda hafiflediğini söylüyor.",
                MinTurn = 16, Weight = 1.1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Kontrol noktası kur", Blurb = "Yollara nokta konur. " +
                            "Kaçak azalır, herkes durdurulur.",
                        CostMoney = 180, Order = 7, Tuccar = -5, Grievance = 5, Economy = 3,
                    },
                    new EventOption
                    {
                        Label = "Gümrüğü yenile", Blurb = "Memurlar değiştirilir, " +
                            "defterler yeniden açılır.",
                        CostMoney = 240, Aydin = 6, Tuccar = -3, Economy = 3,
                    },
                    new EventOption
                    {
                        Label = "Payını al", Blurb = "Kaçakçılıktan hazineye bir pay ayrılır.",
                        CostMoney = -300, Economy = 5, Tuccar = 6,
                        Aydin = -8, Isci = -4, CostLegitimacy = 5,
                    },
                },
            },

            new EventDef
            {
                Id = "sergi", Title = "ŞEHİR SERGİSİ TEKLİFİ", Source = "Üniversite vekili",
                Body = "Delta'nın on beş yılını anlatan bir sergi açılması teklif " +
                       "ediliyor. Neyin sergileneceği valinin takdirinde.",
                MinTurn = 18, Weight = 0.7f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Aç ve her şeyi göster", Blurb = "Rakamlar, haritalar, " +
                            "eksikler dahil. Pahalı bir dürüstlük.",
                        CostMoney = 260, Aydin = 9, Isci = 4, Order = -4, Grievance = -6,
                    },
                    new EventOption
                    {
                        Label = "Aç ve seçerek göster", Blurb = "Sergi düzenlenir, " +
                            "bazı salonlar açılmaz.",
                        CostMoney = 180, Order = 5, Gelenek = 5, Aydin = -4, Grievance = -7,
                    },
                    new EventOption
                    {
                        Label = "Gerek yok", Blurb = "Teklif reddedilir.",
                        Aydin = -3,
                    },
                },
            },

            new EventDef
            {
                Id = "okul_talebi", Title = "MAHALLE OKUL İSTİYOR", Source = "Halk bakanlığı",
                Body = "Liman'dan gelen dilekçe kısa: \"Çocuklarımız rıhtımda " +
                       "büyüyor.\" Altında yüz elli imza var, çoğu parmak izi.",
                MinTurn = 14, Weight = 1f,
                Options = new[]
                {
                    new EventOption
                    {
                        Label = "Okul yaptır", Blurb = "Okul açılır.",
                        CostMoney = 320, Effect = EventEffect.Material, Magnitude = -60,
                        Aydin = 8, Isci = 7, Grievance = -8, Economy = -3,
                    },
                    new EventOption
                    {
                        Label = "Akşam dersi aç", Blurb = "Mevcut bir binada akşamları ders verilir.",
                        CostMoney = 110, Aydin = 4, Isci = 3, Grievance = -3,
                    },
                    new EventOption
                    {
                        Label = "Sıraya al", Blurb = "Talep listeye yazılır.",
                        Isci = -5, Aydin = -3, Grievance = 4,
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

