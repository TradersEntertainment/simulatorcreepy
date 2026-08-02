// How a term ends. Eleven of them, and none is a sudden death.
//
// Every collapse in §9 fails through its own mechanic and is legible in the city ten turns
// before it lands: the information degrading, the districts slipping away, the quarters
// emptying, the shortages turning chronic. By the time the card below appears the player has
// been watching it happen — the card only names it.
//
// The two survivals are deliberately hard, and DAYANIKLI is the one the design calls the most
// interesting: you went to RADİKAL, you saw what it cost, and you paid to come back.

namespace Mesruiyet.Core
{
    public enum Ending
    {
        None = 0,
        Diktatorluk,      // OTORİTE: you end up governing a fiction, and the army ends you
        Anarsi,           // ÖZGÜRLÜK: consent for everything, until you govern one district
        Plutokrasi,       // SERMAYE: an enormous treasury and a hollow city
        Kolektif,         // EŞİTLİK: everyone fed, nothing accumulates
        Darbe,            // your own garrison
        Isgal,            // Mersa overruns you
        BorcluSehir,      // every law slot is collateral
        Iflas,            // the treasury is empty and stays empty
        TerkEdilmis,      // population under 150
        Surdurulebilir,   // 60 turns, both axes under 70, no district lost
        Dayanikli,        // 60 turns having reached RADİKAL and pulled back
    }

    public sealed class EndingDef
    {
        public Ending Id;
        public string Title;
        public string Subtitle;
        /// <summary>One paragraph. No moralising — the design is explicit about that.</summary>
        public string Epilogue;
        public bool Survival;
    }

    public static class Endings
    {
        public static readonly EndingDef[] All =
        {
            new EndingDef
            {
                Id = Ending.Diktatorluk, Title = "DİKTATÖRLÜK", Subtitle = "otorite · dönüşsüz",
                Epilogue =
                    "Son iki yıl boyunca size getirilen her rakam doğruydu — kâğıt üstünde. " +
                    "Ambar dolu yazıyordu, mahalleler sakin yazıyordu, ordu memnun yazıyordu. " +
                    "Hiçbiri yalan değildi; hepsi sizin duymak istediğiniz şeydi ve bunu " +
                    "söyleyen insanları da siz seçmiştiniz. Şehri yönetmediniz; şehir hakkında " +
                    "yazılmış bir metni yönettiniz.",
            },
            new EndingDef
            {
                Id = Ending.Anarsi, Title = "ANARŞİ", Subtitle = "özgürlük · dönüşsüz",
                Epilogue =
                    "Hiçbir kararname artık kimseyi bağlamıyor. Mahalleler kendi işlerini " +
                    "kendileri görüyor, kendi vergilerini kendileri topluyor ve valiliğe " +
                    "nezaketen haber veriyorlar. Kimse size karşı çıkmadı — sadece sormayı " +
                    "bıraktılar.",
            },
            new EndingDef
            {
                Id = Ending.Plutokrasi, Title = "PLÜTOKRASİ", Subtitle = "sermaye · dönüşsüz",
                Epilogue =
                    "Hazine hiç bu kadar dolu olmamıştı. Liman boş, atölyeler kapalı, " +
                    "tezgâhların başında kimse yok; çünkü bu şehirde yaşamak, bu şehirde " +
                    "çalışarak kazanılandan pahalı. Parayla çözemeyeceğiniz ilk sorun bu oldu.",
            },
            new EndingDef
            {
                Id = Ending.Kolektif, Title = "KOLEKTİF", Subtitle = "eşitlik · dönüşsüz",
                Epilogue =
                    "Herkesin bir çatısı ve bir tayını var. Kimsenin bir şeyi birikmiyor. " +
                    "Yeni bir değirmen kurulamıyor, kurulan tamir edilemiyor, ve okumuş " +
                    "olanlar sessizce kuzeye gidiyor. Kıtlık bir felaket gibi değil, " +
                    "bir alışkanlık gibi geldi.",
            },
            new EndingDef
            {
                Id = Ending.Darbe, Title = "DARBE", Subtitle = "kendi garnizonunuz",
                Epilogue =
                    "Sabah kapıda nöbetçi yoktu. Albay valilik binasına kendi anahtarıyla " +
                    "girdi ve size oturmanızı söyledi. Sizi devirecek kadar büyük orduyu, " +
                    "sizi kurtarsın diye kurmuştunuz.",
            },
            new EndingDef
            {
                Id = Ending.Isgal, Title = "İŞGAL", Subtitle = "mersa",
                Epilogue =
                    "Mersa üç koldan girdi ve rıhtımda kimse karşı koymadı. Şehir zaten " +
                    "aylardır kendi içinde savaşıyordu; dışarıdan gelenlerin yapacak fazla " +
                    "işi kalmamıştı.",
            },
            new EndingDef
            {
                Id = Ending.BorcluSehir, Title = "BORÇLU ŞEHİR", Subtitle = "yasa kitabı sizin değil",
                Epilogue =
                    "Yasa kitabındaki her yuva bir alacaklının teminatı. Meclis toplanıyor, " +
                    "oylama yapıyor, karar veriyor — ve kararların hiçbiri yürürlüğe " +
                    "girmiyor, çünkü kanunlar başkasının. Bir başkasının şehrini yönetiyorsunuz.",
            },
            new EndingDef
            {
                Id = Ending.Iflas, Title = "İFLAS", Subtitle = "hazine boş",
                Epilogue =
                    "Maaşlar ödenmedi, bakım yapılmadı, ve bir sabah kâtipler gelmedi. " +
                    "Şehir durmadı — sadece valilikten bağımsız çalışmaya başladı.",
            },
            new EndingDef
            {
                Id = Ending.TerkEdilmis, Title = "TERK EDİLMİŞ ŞEHİR", Subtitle = "nüfus 150'nin altında",
                Epilogue =
                    "Delta'da hâlâ evler var, yollar var, bir de değirmen var. İnsan yok. " +
                    "Gidenler bir yere gitmedi; sadece burada kalmak için sebep bulamadılar.",
            },
            new EndingDef
            {
                Id = Ending.Surdurulebilir, Title = "SÜRDÜRÜLEBİLİR ŞEHİR", Subtitle = "en zor son",
                Survival = true,
                Epilogue =
                    "On beş yıl. Hiçbir mahalle kaybedilmedi, hiçbir eksen dönüşsüz banda " +
                    "girmedi, ve ambarda her kış biraz fazlası kaldı. Kimse size heykel " +
                    "dikmeyecek. Şehir duruyor ve içindekiler sizden sonra da duracak — " +
                    "bu oyunda kazanmak buydu.",
            },
            new EndingDef
            {
                Id = Ending.Dayanikli, Title = "DAYANIKLI ŞEHİR", Subtitle = "en ilginç son",
                Survival = true,
                Epilogue =
                    "Bir noktada radikal banda girdiniz. Bilgi bozuldu, sokakta kontrol " +
                    "noktaları belirdi, ve bir gün ne hâle geldiğinizi gördünüz. Geri " +
                    "dönmek bedava değildi: bir kanunu, bir bakanı ya da bir mahallenin " +
                    "idaresini vermek zorunda kaldınız. Verdiniz. Bu oyunda başarılabilecek " +
                    "en zor şey buydu ve bunu size burada söylüyoruz.",
            },
        };

        public static EndingDef Get(Ending id)
        {
            foreach (var e in All)
                if (e.Id == id) return e;
            return All[0];
        }
    }
}
