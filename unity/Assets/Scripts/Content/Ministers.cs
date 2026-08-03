// The five ministers, their candidates, and their distortion styles.
//
// This is the thesis of the whole game as a data table. Each minister owns one domain and
// reports on it; a loyalist reports what you want to hear, an expert reports what is true.
// The bias applies ONLY to their own domain, which is what makes the fog partial rather than
// total — you can always see clearly somewhere, just never everywhere.
//
// The trap has to be fair, so the loyalist is genuinely the better short-term answer: cheaper
// decrees, no friction, no complaints to the council. You will take it, and you should. What
// it costs you is the ability to see the crisis coming.

namespace Mesruiyet.Core
{
    public enum Domain
    {
        Maliye = 0,      // ₺, tax
        Tarim = 1,       // the food chain
        Guvenlik = 2,    // security, garrison, the army's mood
        Imar = 3,        // materials, construction, housing
        Halk = 4,        // grievance, population, health
    }

    /// <summary>
    /// How this person behaves when a bot plays their seat in co-op. COOP.md §2: empty seats
    /// go to bots, and a bot is the formula plus one of these. The profile lives on the def
    /// because it IS character — Rıza Efendi hides the mill behind the granary whether a
    /// formula, a bot or (in spirit) the single-player prose is doing the talking.
    /// </summary>
    public enum BotProfile
    {
        /// <summary>ŞİŞİRİCİ — every comfortable number, half again as comfortable.</summary>
        Sisirici,
        /// <summary>SAKLAYICI — the chain blockage disappears behind a healthy total.</summary>
        Saklayici,
        /// <summary>ALARMCI — everything is direr than it is, two battalions are always short.</summary>
        Alarmci,
        /// <summary>DÜRÜST AMA BECERİKSİZ — no agenda, wrong anyway. No range warns you.</summary>
        Beceriksiz,
        /// <summary>YALAKA — magnificent, everything, always, whatever transparency says.</summary>
        Yalaka,
    }

    public sealed class MinisterDef
    {
        public string Name;
        public Domain Domain;

        /// <summary>SADIK reports what you want to hear; UZMAN reports what is true.</summary>
        public bool Loyalist;

        /// <summary>Their personality when a bot holds their seat.</summary>
        public BotProfile Profile;

        /// <summary>
        /// How hard they lean on their own numbers, before transparency and authority scale it.
        /// Always in the flattering direction; an expert sits at zero.
        /// </summary>
        public float StyleBias;

        /// <summary>Their running gag, printed on the appointment card.</summary>
        public string Trait;

        /// <summary>The telegram that introduces them and their domain. Onboarding is diegetic.</summary>
        public string Intro;

        /// <summary>Drives the procedural portrait, so a face is stable across a run.</summary>
        public int Seed;
    }

    public static class Ministers
    {
        public static readonly string[] DomainNames = { "MALİYE", "TARIM", "GÜVENLİK", "İMAR", "HALK" };

        public static readonly string[] DomainBlurbs =
        {
            "Hazine, vergi, bütçe",
            "Yiyecek zinciri, ambar",
            "Asayiş, garnizon, ordunun havası",
            "Malzeme, inşaat, barınma",
            "Hoşnutsuzluk, nüfus, sağlık",
        };

        /// <summary>What each domain's reports cover, in the player's words.</summary>
        public static string Covers(Domain d) => DomainBlurbs[(int)d];

        public static Domain DomainOf(Res r)
        {
            switch (r)
            {
                case Res.Para: return Domain.Maliye;
                case Res.Yiyecek: return Domain.Tarim;
                case Res.Malzeme: return Domain.Imar;
                default: return Domain.Halk;
            }
        }

        // ---------------------------------------------------------------- the diegetic tutorial

        /// <summary>One onboarding telegram: a turn, whose desk it comes from, and the text.</summary>
        public sealed class OnboardingLine
        {
            public int Turn;
            public Domain Domain;
            public string Text;
        }

        /// <summary>
        /// There are no tutorial screens. The cabinet teaches the game by doing its job — one
        /// system and one piece of the HUD per telegram, one or two a turn, across the ten turns
        /// that carry no crises. The design reason matters: this establishes the ministers as the
        /// player's information channel and teaches reliance on them *before* the player ever
        /// learns that some of them shade the numbers. The tutorial and the trap are one content.
        /// </summary>
        public static readonly OnboardingLine[] Onboarding =
        {
            new OnboardingLine
            {
                Turn = 1, Domain = Domain.Maliye,
                Text = "Sayın Vali, tepedeki şeritte altı rakam var; en soldaki hazinedir. " +
                       "Altındaki küçük sayı o turda ne kazanıp ne harcadığınızdır. " +
                       "Birikimi değil, o küçük sayıyı izleyin.",
            },
            new OnboardingLine
            {
                Turn = 1, Domain = Domain.Tarim,
                Text = "Sağ üstteki GÜVENLİK PAYI kutusu, bir aksilik olursa şehrin kaç tur " +
                       "dayanacağını söyler. Dokuz turdayız. Bu sayı benim en çok önemsediğim şeydir.",
            },
            new OnboardingLine
            {
                Turn = 2, Domain = Domain.Imar,
                Text = "İnşaat parayla değil depodaki malzemeyle olur; alttaki yapı şeridinden " +
                       "seçtiğinizde ikisinin de fiyatı görünür. Depo boşsa para işe yaramaz.",
            },
            new OnboardingLine
            {
                Turn = 2, Domain = Domain.Halk,
                Text = "Haritadaki mahalle adlarının altında birer hoşnutsuzluk rakamı var. " +
                       "Yeşilse mesele yok, kızarırsa o mahalleye gitmenizi tavsiye ederim.",
            },
            new OnboardingLine
            {
                Turn = 3, Domain = Domain.Guvenlik,
                Text = "Sağ alttaki DIŞ DÜNYA kutusunda Mersa'nın tehdidi ve garnizonumuz yazar. " +
                       "Tehdit yükselirken garnizon yerinde sayıyorsa, o farkı ben kapatamam.",
            },
            new OnboardingLine
            {
                Turn = 3, Domain = Domain.Maliye,
                Text = "BÜTÇE düğmesinden vergi oranını ve beş kalemin ödeneğini ayarlarsınız. " +
                       "Vergiyi dörtte birin üstüne çıkarırsanız bunu her mahallede duyarsınız.",
            },
            new OnboardingLine
            {
                Turn = 4, Domain = Domain.Tarim,
                Text = "Sağdaki TEDARİK ZİNCİRLERİ kutusuna bakın: tarla, değirmen, fırın. " +
                       "Halk yalnızca fırındakini yer. Ambardaki toplam büyük görünebilir; " +
                       "önemli olan en sağdaki kutudur.",
            },
            new OnboardingLine
            {
                Turn = 5, Domain = Domain.Halk,
                Text = "Bugün anayasa yazılıyor. Seçeceğiniz üç madde bütün dönemi bağlar ve " +
                       "bazıları eksenlerinize duvar çeker. Sonradan değiştirilemez.",
            },
            new OnboardingLine
            {
                Turn = 6, Domain = Domain.Imar,
                Text = "Yol da bir yapıdır. Mahalle büyürken yol açmazsanız sokaklar tıkanır, " +
                       "ve tıkalı sokak atölyeye gidemeyen işçi demektir.",
            },
            new OnboardingLine
            {
                Turn = 7, Domain = Domain.Maliye,
                Text = "Şeridin en solunda MEŞRUİYET yazar. Kanunları zorla geçirirken ve " +
                       "skandal örtbas ederken oradan harcarsınız. Sıfırlanırsa gerisi konuşulmaz.",
            },
            new OnboardingLine
            {
                Turn = 8, Domain = Domain.Guvenlik,
                Text = "Bazı rakamların yanında kırmızı bir soru işareti göreceksiniz. " +
                       "O, size kesin bir sayı yerine bir aralık verildiği anlamına gelir. " +
                       "Kimin verdiğini de sağdaki BAKANLAR sırasından görürsünüz.",
            },
            new OnboardingLine
            {
                Turn = 9, Domain = Domain.Tarim,
                Text = "Meclisin fısıltıları benim telgraflarımdan bağımsızdır, efendim. " +
                       "Bir vekil ambarın boş olduğunu söylüyorsa, ben ne yazarsam yazayım, " +
                       "ambara bakın.",
            },
            new OnboardingLine
            {
                Turn = 10, Domain = Domain.Halk,
                Text = "Yarından itibaren olay kartları gelmeye başlar. Her birinin yavaş ve " +
                       "pahalı bir cevabı, bir de hızlı olanı vardır. Hangisini seçebileceğinizi " +
                       "bugün biriktirdiğiniz belirler.",
            },
            new OnboardingLine
            {
                Turn = 10, Domain = Domain.Imar,
                Text = "Bir bakanı görevden alabilirsiniz. Karşınıza iki aday çıkar: biri " +
                       "işini bilir ve sizi meclise şikâyet eder, diğeri hiç itiraz etmez. " +
                       "İkisi de aynı maaşı alır.",
            },
        };

        /// <summary>The onboarding line for this domain on this turn, or null.</summary>
        public static OnboardingLine LessonFor(int turn, Domain domain)
        {
            foreach (var l in Onboarding)
                if (l.Turn == turn && l.Domain == domain) return l;
            return null;
        }

        // ---------------------------------------------------------------- the candidates
        //
        // Two per domain per slot: one loyalist, one expert. The pool is deliberately small and
        // named — you are meant to remember who told you what, because the accountability
        // session at turn 60 reads their names back to you.

        public static readonly MinisterDef[] Pool =
        {
            // ---- MALİYE: inflates revenue, and names taxes after himself
            new MinisterDef
            {
                Name = "Nazif Bey", Profile = BotProfile.Sisirici, Domain = Domain.Maliye, Loyalist = true, StyleBias = 0.42f, Seed = 11,
                Trait = "Her tur yeni bir vergi önerir ve hepsine kendi adını verir.",
                Intro = "Sayın Vali, hazineyi ben tutuyorum. Rakamlar iyidir, daima iyi olacaktır. " +
                        "Zât-ı âliniz meşgul olmasın.",
            },
            new MinisterDef
            {
                Name = "Sabiha Hanım", Profile = BotProfile.Beceriksiz, Domain = Domain.Maliye, Loyalist = false, StyleBias = 0f, Seed = 12,
                Trait = "Defterleri kuruşuna kadar okur, ve okuduğunu meclise de okur.",
                Intro = "Sayın Vali, hazineyi ben tutuyorum. Size hoşunuza gitmeyecek rakamlar " +
                        "getireceğim; getirmezsem işimi yapmıyorum demektir.",
            },

            // ---- TARIM: the interlock that matters most — reports the total, not the blockage
            new MinisterDef
            {
                Name = "Rıza Efendi", Profile = BotProfile.Saklayici, Domain = Domain.Tarim, Loyalist = true, StyleBias = 0.55f, Seed = 21,
                Trait = "Ambarı sever, değirmeni hiç ziyaret etmemiştir.",
                Intro = "Sayın Vali, ambarı ben takip ediyorum. Zât-ı âliniz meşgul olmasın, " +
                        "toplam yerindedir.",
            },
            new MinisterDef
            {
                Name = "Müzeyyen Hanım", Profile = BotProfile.Beceriksiz, Domain = Domain.Tarim, Loyalist = false, StyleBias = 0f, Seed = 22,
                Trait = "Toplamı değil, zincirin en dar halkasını bildirir.",
                Intro = "Sayın Vali, ambarı ben takip ediyorum. Size toplamı değil, hangi " +
                        "aşamanın tıkalı olduğunu bildireceğim. Toplam yanıltır.",
            },

            // ---- GÜVENLİK: the one deadly blind spot — the army speaks only through him
            new MinisterDef
            {
                Name = "Albay Kadri", Profile = BotProfile.Alarmci, Domain = Domain.Guvenlik, Loyalist = true, StyleBias = 0.48f, Seed = 31,
                Trait = "Mersa ajanlarını fırında görmüştür. İki tabur daha şarttır.",
                Intro = "Sayın Vali, asayişi ben tutuyorum. Ordu memnundur. Ordu daima memnundur.",
            },
            new MinisterDef
            {
                Name = "Binbaşı Nesrin", Profile = BotProfile.Beceriksiz, Domain = Domain.Guvenlik, Loyalist = false, StyleBias = 0f, Seed = 32,
                Trait = "Garnizonun havasını olduğu gibi söyler, hoşunuza gitse de gitmese de.",
                Intro = "Sayın Vali, asayişi ben tutuyorum. Ordunun havasını size ben " +
                        "bildiriyorum — bu yüzden yalan söylersem hiçbir yerden duyamazsınız.",
            },

            // ---- İMAR: reports the yard total, never the depot
            new MinisterDef
            {
                Name = "Şevket Bey", Profile = BotProfile.Sisirici, Domain = Domain.Imar, Loyalist = true, StyleBias = 0.40f, Seed = 41,
                Trait = "Her rapora bir maket iliştirir. Maketler daima bitmiştir.",
                Intro = "Sayın Vali, inşaat benim işim. Malzeme boldur. Ham, işlenmiş, fark etmez " +
                        "— hepsi malzemedir efendim.",
            },
            new MinisterDef
            {
                Name = "Hüsniye Hanım", Profile = BotProfile.Beceriksiz, Domain = Domain.Imar, Loyalist = false, StyleBias = 0f, Seed = 42,
                Trait = "Depodaki işlenmiş malzemeyi ham taştan ayrı sayar.",
                Intro = "Sayın Vali, inşaat benim işim. Depoda ne varsa onu yazacağım; " +
                        "ocaktaki ham taşı malzeme diye saymam.",
            },

            // ---- HALK: rounds grievance down, and the districts feel it anyway
            new MinisterDef
            {
                Name = "Cevat Bey", Profile = BotProfile.Yalaka, Domain = Domain.Halk, Loyalist = true, StyleBias = 0.46f, Seed = 51,
                Trait = "Hoşnutsuzluğu aşağı yuvarlar. Her mahalle \"gayet sakin\"dir.",
                Intro = "Sayın Vali, halkın hâlini ben arz ederim. Mahalleler sakindir. " +
                        "Ufak tefek homurtu her şehirde olur.",
            },
            new MinisterDef
            {
                Name = "Perihan Hanım", Profile = BotProfile.Beceriksiz, Domain = Domain.Halk, Loyalist = false, StyleBias = 0f, Seed = 52,
                Trait = "Mahalleleri tek tek gezer ve duyduğunu olduğu gibi yazar.",
                Intro = "Sayın Vali, halkın hâlini ben arz ederim. Rakamı yuvarlamam; " +
                        "yuvarlanan rakam üç tur sonra sokakta düzelir.",
            },
        };

        /// <summary>The two candidates for a domain: the loyalist first, then the expert.</summary>
        public static MinisterDef Candidate(Domain d, bool loyalist)
        {
            foreach (var m in Pool)
                if (m.Domain == d && m.Loyalist == loyalist) return m;
            return null;
        }

        /// <summary>
        /// The founding cabinet you inherit. Deliberately mixed: Tarım starts honest so the
        /// granary number is worth trusting, which is precisely what makes replacing him later
        /// cost you something you did not know you had.
        /// </summary>
        public static readonly bool[] FoundingLoyalists =
        {
            true,    // Maliye  — inherited from the founding company's books
            false,   // Tarım   — honest, for now
            true,    // Güvenlik
            false,   // İmar
            true,    // Halk
        };
    }
}
